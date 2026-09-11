using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Tests;

// ── OptionDefinition Tests ──────────────────────────────────────────────────

public class OptionDefinitionTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opt = new OptionDefinition { LongName = "verbose" };
        opt.LongName.ShouldBe("verbose");
        opt.ShortName.ShouldBeNull();
        opt.Description.ShouldBeNull();
        opt.ValueKind.ShouldBe(OptionValueKind.Single);
        opt.ClrType.ShouldBe("string");
        opt.DefaultValue.ShouldBeNull();
        opt.IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var opt = new OptionDefinition
        {
            LongName = "output",
            ShortName = "o",
            Description = "Output path",
            ValueKind = OptionValueKind.Single,
            ClrType = "string",
            DefaultValue = "./out",
            IsRequired = true
        };
        opt.LongName.ShouldBe("output");
        opt.ShortName.ShouldBe("o");
        opt.IsRequired.ShouldBeTrue();
        opt.DefaultValue.ShouldBe("./out");
    }
}

// ── ArgumentDefinition Tests ────────────────────────────────────────────────

public class ArgumentDefinitionTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var arg = new ArgumentDefinition { Name = "image" };
        arg.Name.ShouldBe("image");
        arg.Position.ShouldBe(0);
        arg.ClrType.ShouldBe("string");
        arg.IsRequired.ShouldBeTrue();
        arg.IsVariadic.ShouldBeFalse();
        arg.DefaultValue.ShouldBeNull();
        arg.Description.ShouldBeNull();
    }

    [Fact]
    public void VariadicArgument_SetsCorrectly()
    {
        var arg = new ArgumentDefinition
        {
            Name = "files",
            Position = 1,
            IsVariadic = true,
            IsRequired = false,
            ClrType = "string[]"
        };
        arg.IsVariadic.ShouldBeTrue();
        arg.IsRequired.ShouldBeFalse();
        arg.Position.ShouldBe(1);
    }
}

// ── CommandNode Tests ───────────────────────────────────────────────────────

public class CommandNodeTests
{
    [Fact]
    public void IsLeaf_NoSubCommands_ReturnsTrue()
    {
        var node = new CommandNode { Name = "run" };
        node.IsLeaf.ShouldBeTrue();
    }

    [Fact]
    public void IsLeaf_WithSubCommands_ReturnsFalse()
    {
        var node = new CommandNode
        {
            Name = "container",
            SubCommands = [new CommandNode { Name = "run" }]
        };
        node.IsLeaf.ShouldBeFalse();
    }

    [Fact]
    public void GetLeafCommands_FlatList()
    {
        var node = new CommandNode { Name = "run" };
        var leaves = node.GetLeafCommands().ToList();
        leaves.Count.ShouldBe(1);
        leaves[0].Path.ShouldBe(new[] { "run" });
    }

    [Fact]
    public void GetLeafCommands_NestedTree()
    {
        var root = new CommandNodeBuilder("docker")
            .AddSubCommand("container", configure: c =>
            {
                c.AddSubCommand("run");
                c.AddSubCommand("stop");
            })
            .AddSubCommand("image", configure: c =>
            {
                c.AddSubCommand("pull");
            })
            .Build();

        var leaves = root.GetLeafCommands().ToList();
        leaves.Count.ShouldBe(3);
        leaves[0].Path.ShouldBe(new[] { "docker", "container", "run" });
        leaves[1].Path.ShouldBe(new[] { "docker", "container", "stop" });
        leaves[2].Path.ShouldBe(new[] { "docker", "image", "pull" });
    }

    [Fact]
    public void FindByPath_ExistingPath_ReturnsNode()
    {
        var root = new CommandNodeBuilder("docker")
            .AddSubCommand("container", configure: c =>
            {
                c.AddSubCommand("run");
            })
            .Build();

        var found = root.FindByPath(["container", "run"]);
        found.ShouldNotBeNull();
        found!.Name.ShouldBe("run");
    }

    [Fact]
    public void FindByPath_EmptyPath_ReturnsSelf()
    {
        var root = new CommandNode { Name = "docker" };
        root.FindByPath([]).ShouldBe(root);
    }

    [Fact]
    public void FindByPath_NonExistentPath_ReturnsNull()
    {
        var root = new CommandNode { Name = "docker" };
        root.FindByPath(["nonexistent"]).ShouldBeNull();
    }

    [Fact]
    public void FindByPath_CaseInsensitive()
    {
        var root = new CommandNodeBuilder("docker")
            .AddSubCommand("Container")
            .Build();

        root.FindByPath(["container"]).ShouldNotBeNull();
    }
}

// ── CommandNodeBuilder Tests ────────────────────────────────────────────────

public class CommandNodeBuilderTests
{
    [Fact]
    public void Build_SetsAllProperties()
    {
        var node = new CommandNodeBuilder("run")
        {
            Description = "Run a command"
        }
        .AddOption("detach", "d", "Detached mode", OptionValueKind.Flag, "bool")
        .AddArgument("image", 0, "Image name")
        .Build();

        node.Name.ShouldBe("run");
        node.Description.ShouldBe("Run a command");
        node.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("detach");
        node.Options[0].ShortName.ShouldBe("d");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
        node.Arguments.Count.ShouldBe(1);
        node.Arguments[0].Name.ShouldBe("image");
    }

    [Fact]
    public void AddSubCommand_WithConfigure_BuildsNestedTree()
    {
        var node = new CommandNodeBuilder("docker")
            .AddSubCommand("container", "Container commands", c =>
            {
                c.AddSubCommand("run", "Run a container");
                c.AddSubCommand("stop", "Stop a container");
            })
            .Build();

        node.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("container");
        node.SubCommands[0].SubCommands.Count.ShouldBe(2);
    }

    [Fact]
    public void AddSubCommand_NoConfigure_AddsLeafNode()
    {
        var node = new CommandNodeBuilder("docker")
            .AddSubCommand("version")
            .Build();

        node.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("version");
        node.SubCommands[0].SubCommands.ShouldBeEmpty();
    }

    [Fact]
    public void AddSubCommand_ExistingNode_AddsDirectly()
    {
        var child = new CommandNode { Name = "child", Description = "A child" };
        var node = new CommandNodeBuilder("parent")
            .AddSubCommand(child)
            .Build();

        node.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].ShouldBe(child);
    }

    [Fact]
    public void AddOption_Record_AddsDirectly()
    {
        var opt = new OptionDefinition { LongName = "verbose", ValueKind = OptionValueKind.Flag };
        var node = new CommandNodeBuilder("run")
            .AddOption(opt)
            .Build();

        node.Options.Count.ShouldBe(1);
        node.Options[0].ShouldBe(opt);
    }

    [Fact]
    public void AddOption_WithAllParams()
    {
        var node = new CommandNodeBuilder("run")
            .AddOption("env", "e", "Environment variable",
                OptionValueKind.Multiple, "string", null, true)
            .Build();

        var opt = node.Options[0];
        opt.ValueKind.ShouldBe(OptionValueKind.Multiple);
        opt.IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void Build_ProducesDefensiveCopies()
    {
        var builder = new CommandNodeBuilder("root");
        builder.SubCommands.Add(new CommandNode { Name = "a" });
        var node = builder.Build();

        // Mutating the builder after Build() should not affect the built node
        builder.SubCommands.Add(new CommandNode { Name = "b" });
        node.SubCommands.Count.ShouldBe(1);
    }
}

// ── CommandTree Tests ───────────────────────────────────────────────────────

public class CommandTreeTests
{
    [Fact]
    public void Properties_SetCorrectly()
    {
        var tree = new CommandTree
        {
            BinaryName = "docker",
            Version = "24.0.7",
            Description = "Docker CLI",
            Root = new CommandNode { Name = "docker" }
        };
        tree.BinaryName.ShouldBe("docker");
        tree.Version.ShouldBe("24.0.7");
        tree.Root.Name.ShouldBe("docker");
    }
}

// ── CommandTreeJsonSerializer Tests ─────────────────────────────────────────

public class CommandTreeJsonSerializerTests
{
    private static CommandTree CreateSampleTree()
    {
        var root = new CommandNodeBuilder("docker")
            .AddSubCommand("container", "Container commands", c =>
            {
                c.AddSubCommand("run", "Run a container", run =>
                {
                    run.AddOption("detach", "d", "Detached mode", OptionValueKind.Flag, "bool");
                    run.AddArgument("image", 0, "Image name");
                });
            })
            .Build();

        return new CommandTree
        {
            BinaryName = "docker",
            Version = "24.0.7",
            Root = root
        };
    }

    [Fact]
    public void RoundTrip_PreservesTree()
    {
        var original = CreateSampleTree();
        var json = CommandTreeJsonSerializer.Serialize(original);
        var deserialized = CommandTreeJsonSerializer.Deserialize(json);

        deserialized.ShouldNotBeNull();
        deserialized!.BinaryName.ShouldBe("docker");
        deserialized.Version.ShouldBe("24.0.7");
        deserialized.Root.Name.ShouldBe("docker");
        deserialized.Root.SubCommands.Count.ShouldBe(1);

        var container = deserialized.Root.SubCommands[0];
        container.Name.ShouldBe("container");
        container.SubCommands.Count.ShouldBe(1);

        var run = container.SubCommands[0];
        run.Name.ShouldBe("run");
        run.Options.Count.ShouldBe(1);
        run.Options[0].LongName.ShouldBe("detach");
        run.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
        run.Arguments.Count.ShouldBe(1);
        run.Arguments[0].Name.ShouldBe("image");
    }

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var tree = new CommandTree
        {
            BinaryName = "test",
            Root = new CommandNode { Name = "test" }
        };
        var json = CommandTreeJsonSerializer.Serialize(tree);
        json.ShouldContain("\"binaryName\"");
        json.ShouldContain("\"test\"");
    }

    [Fact]
    public void Deserialize_NullVersion_OmittedInJson()
    {
        var tree = new CommandTree
        {
            BinaryName = "test",
            Root = new CommandNode { Name = "test" }
        };
        var json = CommandTreeJsonSerializer.Serialize(tree);
        json.ShouldNotContain("\"version\"");
    }

    [Fact]
    public async Task FileRoundTrip_Works()
    {
        var original = CreateSampleTree();
        var path = Path.Combine(Path.GetTempPath(), $"test-tree-{Guid.NewGuid()}.json");

        try
        {
            await CommandTreeJsonSerializer.SerializeToFileAsync(original, path);
            File.Exists(path).ShouldBeTrue();

            var deserialized = await CommandTreeJsonSerializer.DeserializeFromFileAsync(path);
            deserialized.ShouldNotBeNull();
            deserialized!.BinaryName.ShouldBe("docker");
            deserialized.Root.SubCommands.Count.ShouldBe(1);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void EnumValues_SerializeAsCamelCase()
    {
        var root = new CommandNodeBuilder("test")
            .AddOption("verbose", valueKind: OptionValueKind.Flag)
            .Build();
        var tree = new CommandTree
        {
            BinaryName = "test",
            Root = root
        };
        var json = CommandTreeJsonSerializer.Serialize(tree);
        json.ShouldContain("\"flag\"");
    }
}

// ── NewCommand Tests ────────────────────────────────────────────────────────

public class NewCommandTests
{
    [Theory]
    [InlineData("vagrant", "Vagrant")]
    [InlineData("docker", "Docker")]
    [InlineData("container-runtime", "ContainerRuntime")]
    [InlineData("my_tool", "MyTool")]
    [InlineData("simple", "Simple")]
    public void ToPascalCase_ConvertsCorrectly(string input, string expected)
    {
        NewCommand.ToPascalCase(input).ShouldBe(expected);
    }

    [Fact]
    public void ToPascalCase_EmptyOrWhitespace_ReturnsAsIs()
    {
        NewCommand.ToPascalCase("").ShouldBe("");
        NewCommand.ToPascalCase("  ").ShouldBe("  ");
    }

    [Fact]
    public void PrepareSolution_ReturnsCorrectDirsAndFiles()
    {
        var (dirs, files) = NewCommand.PrepareSolution("vagrant", "/out");

        dirs.Length.ShouldBe(8);
        dirs.ShouldContain(d => d.Contains("Vagrant.Wrapper"));
        dirs.ShouldContain(d => d.Contains("Commands"));
        dirs.ShouldContain(d => d.Contains("Events"));
        dirs.ShouldContain(d => d.Contains("Parsers"));
        dirs.ShouldContain(d => d.Contains("Collectors"));
        dirs.ShouldContain(d => d.Contains("doc"));
        dirs.ShouldContain(d => d.Contains("scrape"));

        files.Count.ShouldBe(7);
        files.Keys.ShouldContain(k => k.Contains("Vagrant.Wrapper.slnx"));
        files.Keys.ShouldContain(k => k.Contains("Vagrant.Wrapper.csproj"));
        files.Keys.ShouldContain(k => k.Contains("VagrantClient.cs"));
        files.Keys.ShouldContain(k => k.Contains("Dockerfile"));
        files.Keys.ShouldContain(k => k.Contains("help-output.json"));

        // Verify content correctness (sync, no I/O)
        var clientContent = files.First(f => f.Key.Contains("VagrantClient.cs")).Value;
        clientContent.ShouldContain("namespace Vagrant.Wrapper;");
        clientContent.ShouldContain("public static class Vagrant");

        var jsonContent = files.First(f => f.Key.Contains("help-output.json")).Value;
        var tree = CommandTreeJsonSerializer.Deserialize(jsonContent);
        tree.ShouldNotBeNull();
        tree!.BinaryName.ShouldBe("vagrant");
    }

    [Fact]
    public void PrepareSolution_DifferentBinary_UsesCorrectPascalCase()
    {
        var (_, files) = NewCommand.PrepareSolution("container-runtime", "/out");

        files.Keys.ShouldContain(k => k.Contains("ContainerRuntimeClient.cs"));
        var client = files.First(f => f.Key.Contains("ContainerRuntimeClient.cs")).Value;
        client.ShouldContain("namespace ContainerRuntime.Wrapper;");
        client.ShouldContain("public static class ContainerRuntime");
    }

    [Fact]
    public async Task GenerateSolution_CreatesExpectedStructure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bw-test-{Guid.NewGuid()}");

        try
        {
            await NewCommand.GenerateSolutionAsync("vagrant", tempDir);

            // Verify directory structure
            Directory.Exists(Path.Combine(tempDir, "src", "Vagrant.Wrapper")).ShouldBeTrue();
            Directory.Exists(Path.Combine(tempDir, "src", "Vagrant.Wrapper", "Commands")).ShouldBeTrue();
            Directory.Exists(Path.Combine(tempDir, "src", "Vagrant.Wrapper", "Events")).ShouldBeTrue();
            Directory.Exists(Path.Combine(tempDir, "src", "Vagrant.Wrapper", "Parsers")).ShouldBeTrue();
            Directory.Exists(Path.Combine(tempDir, "src", "Vagrant.Wrapper", "Collectors")).ShouldBeTrue();
            Directory.Exists(Path.Combine(tempDir, "test", "Vagrant.Wrapper.Tests")).ShouldBeTrue();
            Directory.Exists(Path.Combine(tempDir, "doc")).ShouldBeTrue();
            Directory.Exists(Path.Combine(tempDir, "scrape")).ShouldBeTrue();

            // Verify key files exist
            File.Exists(Path.Combine(tempDir, "Vagrant.Wrapper.slnx")).ShouldBeTrue();
            File.Exists(Path.Combine(tempDir, "src", "Vagrant.Wrapper", "Vagrant.Wrapper.csproj")).ShouldBeTrue();
            File.Exists(Path.Combine(tempDir, "src", "Vagrant.Wrapper", "VagrantClient.cs")).ShouldBeTrue();
            File.Exists(Path.Combine(tempDir, "test", "Vagrant.Wrapper.Tests", "Vagrant.Wrapper.Tests.csproj")).ShouldBeTrue();
            File.Exists(Path.Combine(tempDir, "scrape", "Dockerfile")).ShouldBeTrue();
            File.Exists(Path.Combine(tempDir, "scrape", "help-output.json")).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GenerateSolution_ClientFile_ContainsCorrectNamespace()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bw-test-{Guid.NewGuid()}");

        try
        {
            await NewCommand.GenerateSolutionAsync("container-runtime", tempDir);

            var clientContent = await File.ReadAllTextAsync(
                Path.Combine(tempDir, "src", "ContainerRuntime.Wrapper", "ContainerRuntimeClient.cs"));
            clientContent.ShouldContain("namespace ContainerRuntime.Wrapper;");
            clientContent.ShouldContain("public static class ContainerRuntime");
            clientContent.ShouldContain("public class ContainerRuntimeClient");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GenerateSolution_HelpJson_IsValidCommandTree()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bw-test-{Guid.NewGuid()}");

        try
        {
            await NewCommand.GenerateSolutionAsync("packer", tempDir);

            var json = await File.ReadAllTextAsync(
                Path.Combine(tempDir, "scrape", "help-output.json"));
            var tree = CommandTreeJsonSerializer.Deserialize(json);
            tree.ShouldNotBeNull();
            tree!.BinaryName.ShouldBe("packer");
            tree.Root.Name.ShouldBe("packer");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NoArgs_ReturnsError()
    {
        var cmd = new NewCommand();
        var result = await cmd.ExecuteAsync([]);
        result.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithName_ReturnsSuccess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bw-test-{Guid.NewGuid()}");

        try
        {
            var cmd = new NewCommand();
            var result = await cmd.ExecuteAsync(["test-binary", tempDir]);
            result.ShouldBe(0);
            Directory.Exists(tempDir).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NameOnly_UsesCurrentDirDefault()
    {
        // Test the default output dir branch (args.Length == 1)
        var originalDir = Directory.GetCurrentDirectory();
        var tempDir = Path.Combine(Path.GetTempPath(), $"bw-workdir-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            Directory.SetCurrentDirectory(tempDir);
            var cmd = new NewCommand();
            var result = await cmd.ExecuteAsync(["my-tool"]);
            result.ShouldBe(0);

            var expectedDir = Path.Combine(tempDir, "my-tool");
            Directory.Exists(expectedDir).ShouldBeTrue();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}

// ── Placeholder Command Tests ───────────────────────────────────────────────

public class PlaceholderCommandTests
{
    [Fact]
    public async Task ScrapeCommand_ReturnsError()
    {
        var cmd = new ScrapeCommand();
        cmd.Name.ShouldBe("scrape");
        cmd.Description.ShouldNotBeNullOrEmpty();
        var result = await cmd.ExecuteAsync([]);
        result.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateCommand_ReturnsError()
    {
        var cmd = new GenerateCommand();
        cmd.Name.ShouldBe("generate");
        cmd.Description.ShouldNotBeNullOrEmpty();
        var result = await cmd.ExecuteAsync([]);
        result.ShouldBe(1);
    }

    [Fact]
    public async Task VersionDiffCommand_ReturnsError()
    {
        var cmd = new VersionDiffCommand();
        cmd.Name.ShouldBe("version-diff");
        cmd.Description.ShouldNotBeNullOrEmpty();
        var result = await cmd.ExecuteAsync([]);
        result.ShouldBe(1);
    }
}

// ── OptionValueKind Tests ───────────────────────────────────────────────────

public class OptionValueKindTests
{
    [Fact]
    public void AllValues_Exist()
    {
        Enum.GetValues<OptionValueKind>().Length.ShouldBe(3);
        Enum.IsDefined(OptionValueKind.Flag).ShouldBeTrue();
        Enum.IsDefined(OptionValueKind.Single).ShouldBeTrue();
        Enum.IsDefined(OptionValueKind.Multiple).ShouldBeTrue();
    }
}

// ── StandardHelpParser Tests ────────────────────────────────────────────────

public class StandardHelpParserTests
{
    private readonly StandardHelpParser _parser = new();

    [Fact]
    public void Parse_NullOrEmpty_ReturnsNull()
    {
        _parser.Parse(null!, "cmd").ShouldBeNull();
        _parser.Parse("", "cmd").ShouldBeNull();
        _parser.Parse("   ", "cmd").ShouldBeNull();
    }

    [Fact]
    public void Parse_DescriptionBeforeSections()
    {
        var help = """
            A tool for managing containers

            Usage:
              docker [command]

            Commands:
              run     Run a container
              stop    Stop a container
            """;

        var node = _parser.Parse(help, "docker");
        node.ShouldNotBeNull();
        node!.Description.ShouldBe("A tool for managing containers");
    }

    [Fact]
    public void Parse_CommandsSection()
    {
        var help = """
            Commands:
              run     Run a container
              stop    Stop a running container
              build   Build an image
            """;

        var node = _parser.Parse(help, "docker");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(3);
        node.SubCommands[0].Name.ShouldBe("run");
        node.SubCommands[0].Description.ShouldBe("Run a container");
        node.SubCommands[1].Name.ShouldBe("stop");
        node.SubCommands[2].Name.ShouldBe("build");
    }

    [Fact]
    public void Parse_AvailableCommandsHeader()
    {
        var help = """
            Available Commands:
              run     Run a container
            """;

        var node = _parser.Parse(help, "docker");
        node!.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_OptionsSection_LongFlag()
    {
        var help = """
            Options:
              --verbose    Enable verbose output
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("verbose");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
        node.Options[0].Description.ShouldBe("Enable verbose output");
    }

    [Fact]
    public void Parse_OptionsSection_ShortAndLong()
    {
        var help = """
            Options:
              -v, --verbose    Enable verbose output
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("verbose");
        node.Options[0].ShortName.ShouldBe("v");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
    }

    [Fact]
    public void Parse_OptionsSection_WithEqualsValue()
    {
        var help = """
            Options:
              --output=PATH    Output path
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("output");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].Description.ShouldBe("Output path");
    }

    [Fact]
    public void Parse_OptionsSection_WithSpacedValue()
    {
        var help = """
            Options:
              --output PATH    Output path
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("output");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Parse_OptionsSection_WithBracketedValue()
    {
        var help = """
            Options:
              --format <FORMAT>    Output format
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("format");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Parse_OptionsSection_StandaloneShort_WithDescription()
    {
        var help = """
            Options:
              -v Enable verbose
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("v");
        node.Options[0].ShortName.ShouldBe("v");
    }

    [Fact]
    public void Parse_OptionsSection_StandaloneShort_NoDescription()
    {
        var help = """
            Options:
              -v
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ShortName.ShouldBe("v");
    }

    [Fact]
    public void Parse_OptionsSection_LongOnly_NoValueNoDescription()
    {
        var help = """
            Options:
              --help
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("help");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
        node.Options[0].Description.ShouldBeNull();
    }

    [Fact]
    public void Parse_ArgumentsSection()
    {
        var help = """
            Arguments:
              <image>        Image to run
              [extra...]     Extra arguments
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Arguments.Count.ShouldBe(2);
        node.Arguments[0].Name.ShouldBe("image");
        node.Arguments[0].IsRequired.ShouldBeTrue();
        node.Arguments[0].IsVariadic.ShouldBeFalse();
        node.Arguments[0].Position.ShouldBe(0);
        node.Arguments[1].Name.ShouldBe("extra");
        node.Arguments[1].IsRequired.ShouldBeFalse();
        node.Arguments[1].IsVariadic.ShouldBeTrue();
        node.Arguments[1].Position.ShouldBe(1);
    }

    [Fact]
    public void Parse_FlagsHeader()
    {
        var help = """
            Flags:
              --verbose    Be verbose
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_GlobalOptionsHeader()
    {
        var help = """
            Global Options:
              --debug    Enable debug
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_ArgsHeader()
    {
        var help = """
            Args:
              <file>    Input file
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Arguments.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_NonIndentedLine_EndsSection()
    {
        var help = """
            Commands:
              run     Run it
            Some other text
              stop    This should not be parsed as a command
            """;

        var node = _parser.Parse(help, "cmd");
        // "stop" comes after a non-indented line, which resets section to None
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("run");
    }

    [Fact]
    public void Parse_EmptyLine_ParseCommandLine_NoContent()
    {
        var builder = new CommandNodeBuilder("test");
        StandardHelpParser.ParseCommandLine("", builder);
        builder.SubCommands.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_CommandLine_NameOnly()
    {
        var builder = new CommandNodeBuilder("test");
        StandardHelpParser.ParseCommandLine("version", builder);
        builder.SubCommands.Count.ShouldBe(1);
        builder.SubCommands[0].Name.ShouldBe("version");
        builder.SubCommands[0].Description.ShouldBeNull();
    }

    [Fact]
    public void Parse_OptionLine_NeitherShortNorLong_Skipped()
    {
        var builder = new CommandNodeBuilder("test");
        StandardHelpParser.ParseOptionLine("not an option", builder);
        builder.Options.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_ArgumentLine_Empty_Skipped()
    {
        var builder = new CommandNodeBuilder("test");
        StandardHelpParser.ParseArgumentLine("", builder);
        builder.Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_HeaderWithoutColon_MatchesExactLength()
    {
        // "Commands" (no colon) — exact length match branch in IsHeader
        var help = "Commands\n  run    Run it\n";
        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_OptionWithEquals_NoDescription()
    {
        var help = """
            Options:
              --output=PATH
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("output");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].Description.ShouldBeNull();
    }

    [Fact]
    public void Parse_OptionWithSpacedPlaceholder_NoDescription()
    {
        // --name VALUE (no description after placeholder)
        var help = """
            Options:
              --name VALUE
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("name");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].Description.ShouldBeNull();
    }

    [Fact]
    public void Parse_ArgumentLine_NameOnly_NoDescription()
    {
        var builder = new CommandNodeBuilder("test");
        StandardHelpParser.ParseArgumentLine("file", builder);
        builder.Arguments.Count.ShouldBe(1);
        builder.Arguments[0].Name.ShouldBe("file");
        builder.Arguments[0].Description.ShouldBeNull();
    }

    [Fact]
    public void Parse_OptionWithSquareBracketPlaceholder()
    {
        // [VALUE] is also recognized as a value placeholder
        var help = """
            Options:
              --name [VALUE]    The name
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Parse_OptionPlaceholder_WithUnderscoresAndDigits()
    {
        // ALL_CAPS_2 is still a value placeholder (non-letter chars like _ and digits pass)
        var help = """
            Options:
              --key VAL_2    A key
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Parse_FullGnuHelp()
    {
        var help = """
            vagrant - Development environments made easy

            Usage:
              vagrant [options] [command] [args]

            Commands:
              up          Start the machine
              halt        Stop the machine
              destroy     Destroy the machine
              ssh         SSH into the machine

            Options:
              -v, --version          Print version
              -h, --help             Print help
              --debug                Enable debug
              --machine-readable     Machine readable output
              --color                Enable color output
              --timestamp            Enable timestamps

            Arguments:
              <machine>     Machine name
            """;

        var node = _parser.Parse(help, "vagrant");
        node.ShouldNotBeNull();
        node!.Name.ShouldBe("vagrant");
        node.Description.ShouldBe("vagrant - Development environments made easy");
        node.SubCommands.Count.ShouldBe(4);
        node.Options.Count.ShouldBe(6);
        node.Arguments.Count.ShouldBe(1);
    }
}

// ── HelpScraper Tests ───────────────────────────────────────────────────────

public class HelpScraperTests
{
    [Fact]
    public async Task ScrapeAsync_SimpleTree()
    {
        var helpTexts = new Dictionary<string, string>
        {
            ["tool --help"] = """
                A test tool

                Commands:
                  sub1    First subcommand
                  sub2    Second subcommand
                """,
            ["tool sub1 --help"] = """
                First subcommand

                Options:
                  --flag    A flag
                """,
            ["tool sub2 --help"] = """
                Second subcommand

                Arguments:
                  <input>    Input file
                """
        };

        var scraper = new HelpScraper(
            new StandardHelpParser(),
            args => Task.FromResult(helpTexts.GetValueOrDefault(string.Join(" ", args), "")));

        var tree = await scraper.ScrapeAsync("tool");

        tree.BinaryName.ShouldBe("tool");
        tree.Root.Name.ShouldBe("tool");
        tree.Root.SubCommands.Count.ShouldBe(2);
        tree.Root.SubCommands[0].Name.ShouldBe("sub1");
        tree.Root.SubCommands[0].Options.Count.ShouldBe(1);
        tree.Root.SubCommands[1].Name.ShouldBe("sub2");
        tree.Root.SubCommands[1].Arguments.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ScrapeAsync_RecursiveSubCommands()
    {
        var helpTexts = new Dictionary<string, string>
        {
            ["tool --help"] = """
                Commands:
                  group    A group
                """,
            ["tool group --help"] = """
                Commands:
                  leaf    A leaf command
                """,
            ["tool group leaf --help"] = """
                A leaf command

                Options:
                  --verbose    Be verbose
                """
        };

        var scraper = new HelpScraper(
            new StandardHelpParser(),
            args => Task.FromResult(helpTexts.GetValueOrDefault(string.Join(" ", args), "")));

        var tree = await scraper.ScrapeAsync("tool");

        tree.Root.SubCommands.Count.ShouldBe(1);
        var group = tree.Root.SubCommands[0];
        group.Name.ShouldBe("group");
        group.SubCommands.Count.ShouldBe(1);
        group.SubCommands[0].Name.ShouldBe("leaf");
        group.SubCommands[0].Options.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ScrapeAsync_MaxDepth_StopsRecursion()
    {
        var callCount = 0;
        var scraper = new HelpScraper(
            new StandardHelpParser(),
            args =>
            {
                callCount++;
                return Task.FromResult("""
                    Commands:
                      deeper    Goes deeper
                    """);
            },
            maxDepth: 2);

        var tree = await scraper.ScrapeAsync("tool");

        // Depth 0: tool, depth 1: deeper, depth 2: deeper, depth 3: stopped
        callCount.ShouldBeGreaterThan(1);
        callCount.ShouldBeLessThanOrEqualTo(4); // tool + deeper + deeper + one more attempt
    }

    [Fact]
    public async Task ScrapeAsync_HelpThrows_ReturnsNull()
    {
        var scraper = new HelpScraper(
            new StandardHelpParser(),
            _ => throw new InvalidOperationException("binary not found"));

        var tree = await scraper.ScrapeAsync("nonexistent");

        tree.BinaryName.ShouldBe("nonexistent");
        tree.Root.Name.ShouldBe("nonexistent");
        tree.Root.SubCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task ScrapeAsync_SubCommandHelpThrows_UsesParsedStub()
    {
        var scraper = new HelpScraper(
            new StandardHelpParser(),
            args =>
            {
                if (args.Length > 2) throw new Exception("sub help failed");
                return Task.FromResult("""
                    Commands:
                      sub1    A subcommand
                    """);
            });

        var tree = await scraper.ScrapeAsync("tool");

        tree.Root.SubCommands.Count.ShouldBe(1);
        tree.Root.SubCommands[0].Name.ShouldBe("sub1");
        tree.Root.SubCommands[0].Description.ShouldBe("A subcommand");
    }

    [Fact]
    public async Task ScrapeAsync_Cancellation_Stops()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var scraper = new HelpScraper(
            new StandardHelpParser(),
            _ => Task.FromResult("Commands:\n  sub    A sub"));

        var tree = await scraper.ScrapeAsync("tool", cts.Token);

        // Cancelled before scraping, returns fallback
        tree.Root.Name.ShouldBe("tool");
    }

    [Fact]
    public async Task ScrapeAsync_LeafCommand_NoSubCommands_ReturnsDirectly()
    {
        var helpTexts = new Dictionary<string, string>
        {
            ["tool --help"] = """
                A simple tool

                Options:
                  --help    Print help
                """
        };

        var scraper = new HelpScraper(
            new StandardHelpParser(),
            args => Task.FromResult(helpTexts.GetValueOrDefault(string.Join(" ", args), "")));

        var tree = await scraper.ScrapeAsync("tool");

        tree.Root.SubCommands.ShouldBeEmpty();
        tree.Root.Options.Count.ShouldBe(1);
    }

    [Fact]
    public void ReconstructWithScrapedSubCommands_AllScraped_ReplacesAll()
    {
        var original = new CommandNode
        {
            Name = "root",
            Description = "Root",
            Options = [new OptionDefinition { LongName = "verbose" }],
            SubCommands =
            [
                new CommandNode { Name = "sub1" },
                new CommandNode { Name = "sub2" }
            ]
        };

        var scraped1 = new CommandNode { Name = "sub1", Description = "Detailed sub1" };
        var scraped2 = new CommandNode { Name = "sub2", Description = "Detailed sub2" };

        var result = HelpScraper.ReconstructWithScrapedSubCommands(original, [scraped1, scraped2]);

        result.Name.ShouldBe("root");
        result.Description.ShouldBe("Root");
        result.Options.Count.ShouldBe(1);
        result.SubCommands.Count.ShouldBe(2);
        result.SubCommands[0].Description.ShouldBe("Detailed sub1");
        result.SubCommands[1].Description.ShouldBe("Detailed sub2");
    }

    [Fact]
    public void ReconstructWithScrapedSubCommands_NullFallsBackToOriginal()
    {
        var original = new CommandNode
        {
            Name = "root",
            SubCommands =
            [
                new CommandNode { Name = "sub1", Description = "Stub" },
                new CommandNode { Name = "sub2", Description = "Stub2" }
            ]
        };

        var scraped1 = new CommandNode { Name = "sub1", Description = "Detailed" };

        var result = HelpScraper.ReconstructWithScrapedSubCommands(original, [scraped1, null]);

        result.SubCommands[0].Description.ShouldBe("Detailed");
        result.SubCommands[1].Description.ShouldBe("Stub2"); // fallback
    }

    [Fact]
    public void ReconstructWithScrapedSubCommands_PreservesOptionsAndArguments()
    {
        var original = new CommandNode
        {
            Name = "root",
            Description = "D",
            Options = [new OptionDefinition { LongName = "flag" }],
            Arguments = [new ArgumentDefinition { Name = "arg" }],
            SubCommands = [new CommandNode { Name = "child" }]
        };

        var result = HelpScraper.ReconstructWithScrapedSubCommands(original, [null]);

        result.Options.Count.ShouldBe(1);
        result.Options[0].LongName.ShouldBe("flag");
        result.Arguments.Count.ShouldBe(1);
        result.Arguments[0].Name.ShouldBe("arg");
    }

    [Fact]
    public async Task ScrapeAsync_CustomHelpFlag_UsesFlag()
    {
        var receivedArgs = new List<string[]>();
        var scraper = new HelpScraper(
            new StandardHelpParser(),
            args =>
            {
                receivedArgs.Add(args);
                return Task.FromResult("A simple tool\n\nOptions:\n  --help    Print help\n");
            },
            helpFlag: "-h");

        await scraper.ScrapeAsync("packer");

        receivedArgs.Count.ShouldBe(1);
        receivedArgs[0].ShouldBe(new[] { "packer", "-h" });
    }

    [Fact]
    public async Task ScrapeAsync_CustomHelpFlag_PropagatedToSubCommands()
    {
        var receivedArgs = new List<string[]>();
        var helpTexts = new Dictionary<string, string>
        {
            ["packer -h"] = "Commands:\n  build    Build images\n",
            ["packer build -h"] = "Build images\n\nOptions:\n  -force    Force build\n"
        };

        var scraper = new HelpScraper(
            new StandardHelpParser(),
            args =>
            {
                receivedArgs.Add(args);
                return Task.FromResult(helpTexts.GetValueOrDefault(string.Join(" ", args), ""));
            },
            helpFlag: "-h");

        await scraper.ScrapeAsync("packer");

        receivedArgs.Count.ShouldBe(2);
        receivedArgs[1].ShouldBe(new[] { "packer", "build", "-h" });
    }
}

// ── CommandNodeBuilder.From + Set Helpers Tests ─────────────────────────────

public class CommandNodeBuilderFromTests
{
    [Fact]
    public void From_CopiesAllProperties()
    {
        var original = new CommandNodeBuilder("run")
        {
            Description = "Run a command"
        }
        .AddOption("detach", "d", "Detached mode", OptionValueKind.Flag, "bool")
        .AddArgument("image", 0, "Image name")
        .AddSubCommand("sub1")
        .Build();

        var builder = CommandNodeBuilder.From(original);
        var copy = builder.Build();

        copy.Name.ShouldBe("run");
        copy.Description.ShouldBe("Run a command");
        copy.Options.Count.ShouldBe(1);
        copy.Options[0].LongName.ShouldBe("detach");
        copy.Arguments.Count.ShouldBe(1);
        copy.Arguments[0].Name.ShouldBe("image");
        copy.SubCommands.Count.ShouldBe(1);
        copy.SubCommands[0].Name.ShouldBe("sub1");
    }

    [Fact]
    public void SetOptionType_ExistingOption_ChangesType()
    {
        var node = new CommandNodeBuilder("cmd")
            .AddOption("parallel-builds", valueKind: OptionValueKind.Single)
            .Build();

        var builder = CommandNodeBuilder.From(node);
        builder.SetOptionType("parallel-builds", "int");
        var result = builder.Build();

        result.Options[0].ClrType.ShouldBe("int");
    }

    [Fact]
    public void SetOptionType_NonExistentOption_NoOp()
    {
        var node = new CommandNodeBuilder("cmd")
            .AddOption("flag")
            .Build();

        var builder = CommandNodeBuilder.From(node);
        builder.SetOptionType("nonexistent", "int"); // should not throw
        builder.Build().Options.Count.ShouldBe(1);
    }

    [Fact]
    public void SetOptionType_CaseInsensitive()
    {
        var node = new CommandNodeBuilder("cmd")
            .AddOption("Parallel-Builds")
            .Build();

        var builder = CommandNodeBuilder.From(node);
        builder.SetOptionType("parallel-builds", "int");
        builder.Build().Options[0].ClrType.ShouldBe("int");
    }

    [Fact]
    public void SetOptionKind_ExistingOption_ChangesKind()
    {
        var node = new CommandNodeBuilder("cmd")
            .AddOption("var", valueKind: OptionValueKind.Single)
            .Build();

        var builder = CommandNodeBuilder.From(node);
        builder.SetOptionKind("var", OptionValueKind.Multiple);
        builder.Build().Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
    }

    [Fact]
    public void SetOptionKind_NonExistent_NoOp()
    {
        var node = new CommandNodeBuilder("cmd")
            .AddOption("flag")
            .Build();

        var builder = CommandNodeBuilder.From(node);
        builder.SetOptionKind("nonexistent", OptionValueKind.Multiple);
        builder.Build().Options[0].ValueKind.ShouldBe(OptionValueKind.Single); // unchanged
    }

    [Fact]
    public void SetArgumentRequired_ExistingArgument_ChangesRequired()
    {
        var node = new CommandNodeBuilder("cmd")
            .AddArgument("template", isRequired: false)
            .Build();

        var builder = CommandNodeBuilder.From(node);
        builder.SetArgumentRequired("template", true);
        builder.Build().Arguments[0].IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void SetArgumentRequired_NonExistent_NoOp()
    {
        var node = new CommandNodeBuilder("cmd")
            .AddArgument("template")
            .Build();

        var builder = CommandNodeBuilder.From(node);
        builder.SetArgumentRequired("nonexistent", false);
        builder.Build().Arguments[0].IsRequired.ShouldBeTrue(); // unchanged
    }

    [Fact]
    public void SetArgumentRequired_CaseInsensitive()
    {
        var node = new CommandNodeBuilder("cmd")
            .AddArgument("Template", isRequired: false)
            .Build();

        var builder = CommandNodeBuilder.From(node);
        builder.SetArgumentRequired("template");
        builder.Build().Arguments[0].IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void FluentChaining_Works()
    {
        var node = new CommandNodeBuilder("build")
            .AddOption("parallel-builds", valueKind: OptionValueKind.Single)
            .AddOption("color", valueKind: OptionValueKind.Flag)
            .AddOption("var", valueKind: OptionValueKind.Single)
            .AddArgument("template", isRequired: false)
            .Build();

        var result = CommandNodeBuilder.From(node)
            .SetOptionType("parallel-builds", "int")
            .SetOptionType("color", "bool")
            .SetOptionKind("var", OptionValueKind.Multiple)
            .SetArgumentRequired("template")
            .Build();

        result.Options[0].ClrType.ShouldBe("int");
        result.Options[1].ClrType.ShouldBe("bool");
        result.Options[2].ValueKind.ShouldBe(OptionValueKind.Multiple);
        result.Arguments[0].IsRequired.ShouldBeTrue();
    }
}

// ── PackerHelpParser Tests ──────────────────────────────────────────────────

public class PackerHelpParserTests
{
    private readonly PackerHelpParser _parser = new();

    [Fact]
    public void Parse_NullOrEmpty_ReturnsNull()
    {
        _parser.Parse(null!, "cmd").ShouldBeNull();
        _parser.Parse("", "cmd").ShouldBeNull();
        _parser.Parse("   ", "cmd").ShouldBeNull();
    }

    [Fact]
    public void Parse_GoStyleFlags()
    {
        var help = """
            Build images from a template

            Options:
              -color             Enable color output
              -force             Force a build to continue
              -parallel-builds=1 Number of builds to run in parallel
            """;

        var node = _parser.Parse(help, "build");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(3);
        node.Options[0].LongName.ShouldBe("color");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
        node.Options[1].LongName.ShouldBe("force");
        node.Options[1].ValueKind.ShouldBe(OptionValueKind.Flag);
        node.Options[2].LongName.ShouldBe("parallel-builds");
        node.Options[2].ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Parse_AvailableCommandsHeader()
    {
        var help = """
            Usage: packer [--version] [--help] <command> [<args>]

            Available commands are:
              build       Build image(s) from template
              validate    Validate a template
              init        Install missing plugins
            """;

        var node = _parser.Parse(help, "packer");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(3);
        node.SubCommands[0].Name.ShouldBe("build");
        node.SubCommands[1].Name.ShouldBe("validate");
        node.SubCommands[2].Name.ShouldBe("init");
    }

    [Fact]
    public void Parse_SubcommandsHeader()
    {
        var help = """
            Subcommands:
              install    Install a plugin
              remove     Remove a plugin
            """;

        var node = _parser.Parse(help, "plugins");
        node!.SubCommands.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_DescriptionBeforeSections()
    {
        var help = """
            HashiCorp Packer v1.11.2

            Usage: packer [--version] [--help] <command> [<args>]

            Available commands are:
              build    Build images
            """;

        var node = _parser.Parse(help, "packer");
        node!.Description.ShouldBe("HashiCorp Packer v1.11.2");
    }

    [Fact]
    public void Parse_GoFlag_WithEqualsAndDescription()
    {
        var help = """
            Options:
              -parallel-builds=1 Number of builds to run in parallel. Disable: 0
            """;

        var node = _parser.Parse(help, "build");
        node!.Options[0].LongName.ShouldBe("parallel-builds");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].Description.ShouldBe("Number of builds to run in parallel. Disable: 0");
    }

    [Fact]
    public void Parse_GoFlag_WithEqualsNoDescription()
    {
        var help = """
            Options:
              -output=PATH
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].LongName.ShouldBe("output");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].Description.ShouldBeNull();
    }

    [Fact]
    public void Parse_GoFlag_FlagOnly_NoDescription()
    {
        var help = """
            Options:
              -force
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].LongName.ShouldBe("force");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
        node.Options[0].Description.ShouldBeNull();
    }

    [Fact]
    public void Parse_NonIndentedLine_EndsSection()
    {
        var help = """
            Available commands are:
              build    Build images
            For more help:
              validate    This should not be parsed
            """;

        var node = _parser.Parse(help, "packer");
        node!.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_EmptyCommandLine_NoOp()
    {
        var builder = new CommandNodeBuilder("test");
        PackerHelpParser.ParseCommandLine("", builder);
        builder.SubCommands.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_GoOptionLine_NotDash_Skipped()
    {
        var builder = new CommandNodeBuilder("test");
        PackerHelpParser.ParseGoOptionLine("not an option", builder);
        builder.Options.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_GoOptionLine_WithSpacedValuePlaceholder()
    {
        var builder = new CommandNodeBuilder("test");
        PackerHelpParser.ParseGoOptionLine("-output PATH The output", builder);
        builder.Options.Count.ShouldBe(1);
        builder.Options[0].LongName.ShouldBe("output");
        builder.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        builder.Options[0].Description.ShouldBe("The output");
    }

    [Fact]
    public void Parse_GoOptionLine_WithBracketPlaceholder()
    {
        var builder = new CommandNodeBuilder("test");
        PackerHelpParser.ParseGoOptionLine("-format <FMT> Output format", builder);
        builder.Options.Count.ShouldBe(1);
        builder.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Parse_GoOptionLine_DashOnly_Skipped()
    {
        var builder = new CommandNodeBuilder("test");
        PackerHelpParser.ParseGoOptionLine("-", builder);
        builder.Options.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_FlagsHeader()
    {
        var help = """
            Flags:
              -verbose    Be verbose
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_FullPackerHelp()
    {
        var help = """
            Usage: packer [--version] [--help] <command> [<args>]

            Available commands are:
              build         Build image(s) from template
              console       Creates a console for testing
              fix           Fixes templates from old versions
              fmt           Rewrites HCL2 config files to canonical format
              hcl2_upgrade  Transform a JSON template into an HCL2 config
              init          Install missing plugins or upgrade plugins
              inspect       See components of a template
              plugins       Interact with Packer plugins and catalog
              validate      Check that a template is valid

            Flags:
              -machine-readable    Produce machine-readable output
            """;

        var node = _parser.Parse(help, "packer");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(9);
        node.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("machine-readable");
    }
}

// ── CobraHelpParser Tests ───────────────────────────────────────────────────

public class CobraHelpParserTests
{
    private readonly CobraHelpParser _parser = new();

    [Fact]
    public void Parse_NullOrEmpty_ReturnsNull()
    {
        _parser.Parse(null!, "cmd").ShouldBeNull();
        _parser.Parse("", "cmd").ShouldBeNull();
        _parser.Parse("   ", "cmd").ShouldBeNull();
    }

    [Fact]
    public void Parse_CobraStyleHelp_ParsesCommandsAndFlags()
    {
        var help = """
            Manage containers

            Usage:
              podman [options] [command]

            Available Commands:
              attach      Attach to a running container
              build       Build an image using instructions from Containerfiles
              run         Run a command in a new container

            Flags:
              -c, --connection string   Connection to use for remote Podman service
                  --help                Help for podman
                  --storage-opt strings Used to pass an option to the storage driver

            Global Flags:
                  --log-level string   Log messages above specified level (default "warn")
            """;

        var node = _parser.Parse(help, "podman");
        node.ShouldNotBeNull();
        node!.Description.ShouldBe("Manage containers");
        node.SubCommands.Count.ShouldBe(3);
        node.SubCommands[0].Name.ShouldBe("attach");
        node.SubCommands[1].Name.ShouldBe("build");
        node.SubCommands[2].Name.ShouldBe("run");

        // Flags + Global Flags merged
        node.Options.Count.ShouldBe(4);

        var connection = node.Options.First(o => o.LongName == "connection");
        connection.ShortName.ShouldBe("c");
        connection.ValueKind.ShouldBe(OptionValueKind.Single);
        connection.ClrType.ShouldBe("string");

        var help2 = node.Options.First(o => o.LongName == "help");
        help2.ValueKind.ShouldBe(OptionValueKind.Flag);
        help2.ClrType.ShouldBe("bool");

        var storageOpt = node.Options.First(o => o.LongName == "storage-opt");
        storageOpt.ValueKind.ShouldBe(OptionValueKind.Multiple);

        var logLevel = node.Options.First(o => o.LongName == "log-level");
        logLevel.ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Parse_SkipsHelpAndCompletionCommands()
    {
        var help = """
            Available Commands:
              help        Help about any command
              completion  Generate the autocompletion script
              run         Run a container
            """;

        var node = _parser.Parse(help, "docker");
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("run");
    }

    [Fact]
    public void Parse_CustomSkippedCommands()
    {
        var parser = new CobraHelpParser(["help", "serve"]);
        var help = """
            Available Commands:
              help        Help about any command
              completion  Generate the autocompletion script
              serve       Start GRPC server
              run         Run a container
            """;

        var node = parser.Parse(help, "tool");
        node!.SubCommands.Count.ShouldBe(2);
        node.SubCommands[0].Name.ShouldBe("completion");
        node.SubCommands[1].Name.ShouldBe("run");
    }

    [Fact]
    public void Parse_CobraTypeHints_IntegerTypes()
    {
        var help = """
            Flags:
                  --timeout int         Timeout in seconds
                  --retries uint        Number of retries
                  --count count         Event count
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(3);
        node.Options[0].ClrType.ShouldBe("integer");
        node.Options[1].ClrType.ShouldBe("integer");
        node.Options[2].ClrType.ShouldBe("integer");
    }

    [Fact]
    public void Parse_CobraTypeHints_MultipleTypes()
    {
        var help = """
            Flags:
                  --env stringArray     Environment variables
                  --volumes strings     Volume mounts
                  --ports intSlice      Port mappings
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(3);
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
        node.Options[1].ValueKind.ShouldBe(OptionValueKind.Multiple);
        node.Options[2].ValueKind.ShouldBe(OptionValueKind.Multiple);
    }

    [Fact]
    public void Parse_UppercasePlaceholder_TreatedAsSingleValue()
    {
        var help = """
            Flags:
                  --platform ARCH       Target platform
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].ClrType.ShouldBe("string");
    }

    [Fact]
    public void Parse_ManagementCommandsHeader()
    {
        var help = """
            Management Commands:
              container   Manage containers
              image       Manage images
            """;

        var node = _parser.Parse(help, "docker");
        node!.SubCommands.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_DescriptionAndExamplesSections()
    {
        var help = """
            Run a command in a new container

            Description:
              Run a process in a new container. docker run starts a process...

            Usage:
              docker run [OPTIONS] IMAGE [COMMAND] [ARG...]

            Examples:
              docker run -it ubuntu bash

            Flags:
              -d, --detach    Detached mode
            """;

        var node = _parser.Parse(help, "run");
        node!.Description.ShouldBe("Run a command in a new container");
        node.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("detach");
    }
}

// ── ArgparseHelpParser Tests ────────────────────────────────────────────────

public class ArgparseHelpParserTests
{
    private readonly ArgparseHelpParser _parser = new();

    [Fact]
    public void Parse_NullOrEmpty_ReturnsNull()
    {
        _parser.Parse(null!, "cmd").ShouldBeNull();
        _parser.Parse("", "cmd").ShouldBeNull();
        _parser.Parse("   ", "cmd").ShouldBeNull();
    }

    [Fact]
    public void Parse_RootCommandWithSubcommands()
    {
        var help = """
            Manage compose workloads

            options:
              -h, --help            show this help message and exit
              --verbose             Print debugging output

            command:
              {up,down,ps,run,build,logs}
                up                  Create and start the entire stack
                down                tear down entire stack
                ps                  show status of containers
                run                 create a container
                build               build stack images
                logs                show logs from services
            """;

        var node = _parser.Parse(help, "podman-compose");
        node.ShouldNotBeNull();
        node!.Description.ShouldBe("Manage compose workloads");
        node.SubCommands.Count.ShouldBe(6);
        node.SubCommands[0].Name.ShouldBe("up");
        node.SubCommands[0].Description.ShouldBe("Create and start the entire stack");
        node.SubCommands[5].Name.ShouldBe("logs");
    }

    [Fact]
    public void Parse_OptionsWithMetavars()
    {
        var help = """
            options:
              --in-pod in_pod       Specify pod usage
              --project-name PROJECT_NAME
                                    Specify an alternate project name
              --no-ansi             Do not print ANSI control characters
              --parallel PARALLEL
              --verbose             Print debugging output
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();

        var inPod = node!.Options.First(o => o.LongName == "in-pod");
        inPod.ValueKind.ShouldBe(OptionValueKind.Single);
        inPod.ClrType.ShouldBe("string");
        inPod.Description.ShouldBe("Specify pod usage");

        var projectName = node.Options.First(o => o.LongName == "project-name");
        projectName.ValueKind.ShouldBe(OptionValueKind.Single);
        projectName.ClrType.ShouldBe("string");
        projectName.Description.ShouldBe("Specify an alternate project name");

        var noAnsi = node.Options.First(o => o.LongName == "no-ansi");
        noAnsi.ValueKind.ShouldBe(OptionValueKind.Flag);
        noAnsi.ClrType.ShouldBe("bool");

        var parallel = node.Options.First(o => o.LongName == "parallel");
        parallel.ValueKind.ShouldBe(OptionValueKind.Single);

        var verbose = node.Options.First(o => o.LongName == "verbose");
        verbose.ValueKind.ShouldBe(OptionValueKind.Flag);
    }

    [Fact]
    public void Parse_ShortAndLongWithMetavar()
    {
        var help = """
            options:
              -f file, --file file  Specify an compose file
              -p PROJECT_NAME, --project-name PROJECT_NAME
                                    Specify an alternate project name
              -d, --detach          Detached mode
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();

        var file = node!.Options.First(o => o.LongName == "file");
        file.ShortName.ShouldBe("f");
        file.ValueKind.ShouldBe(OptionValueKind.Single);

        var project = node.Options.First(o => o.LongName == "project-name");
        project.ShortName.ShouldBe("p");
        project.ValueKind.ShouldBe(OptionValueKind.Single);

        var detach = node.Options.First(o => o.LongName == "detach");
        detach.ShortName.ShouldBe("d");
        detach.ValueKind.ShouldBe(OptionValueKind.Flag);
    }

    [Fact]
    public void Parse_SubcommandHelp_ParsesOptions()
    {
        var help = """
            positional arguments:
              services              affected services

            options:
              -h, --help            show this help message and exit
              -d, --detach          Detached mode
              --force-recreate      Recreate containers
              -t TIMEOUT, --timeout TIMEOUT
                                    Use this timeout in seconds
              --scale SERVICE=NUM   Scale SERVICE to NUM instances
              --build-arg key=val   Set build-time variables
              --no-cache            Do not use cache
            """;

        var node = _parser.Parse(help, "up");
        node.ShouldNotBeNull();

        node!.Options.Count.ShouldBe(7);

        var detach = node.Options.First(o => o.LongName == "detach");
        detach.ValueKind.ShouldBe(OptionValueKind.Flag);

        var timeout = node.Options.First(o => o.LongName == "timeout");
        timeout.ShortName.ShouldBe("t");
        timeout.ValueKind.ShouldBe(OptionValueKind.Single);

        var scale = node.Options.First(o => o.LongName == "scale");
        scale.ValueKind.ShouldBe(OptionValueKind.Single);

        var buildArg = node.Options.First(o => o.LongName == "build-arg");
        buildArg.ValueKind.ShouldBe(OptionValueKind.Single);

        node.Arguments.Count.ShouldBe(1);
        node.Arguments[0].Name.ShouldBe("services");
        node.Arguments[0].IsVariadic.ShouldBeFalse();
    }

    [Fact]
    public void Parse_SkipsDefaultCommands()
    {
        var help = """
            command:
              {help,up,down}
                help                show help
                up                  start
                down                stop
            """;

        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(2);
        node.SubCommands[0].Name.ShouldBe("up");
        node.SubCommands[1].Name.ShouldBe("down");
    }

    [Fact]
    public void Parse_CustomSkippedCommands()
    {
        var parser = new ArgparseHelpParser(["help", "version"]);
        var help = """
            command:
              {help,version,up,down}
                help                show help
                version             show version
                up                  start
                down                stop
            """;

        var node = parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(2);
        node.SubCommands[0].Name.ShouldBe("up");
        node.SubCommands[1].Name.ShouldBe("down");
    }

    [Fact]
    public void Parse_PositionalArguments()
    {
        var help = """
            positional arguments:
              [services ...]        affected services

            options:
              -h, --help            show this help message and exit
            """;

        var node = _parser.Parse(help, "up");
        node.ShouldNotBeNull();
        node!.Arguments.Count.ShouldBe(1);
        node.Arguments[0].Name.ShouldBe("services");
        node.Arguments[0].IsVariadic.ShouldBeTrue();
        node.Arguments[0].IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void Parse_OlderPythonOptionsHeader()
    {
        var help = """
            optional arguments:
              -h, --help            show this help message and exit
              --verbose             Print debugging output
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(2);
        node.Options[0].LongName.ShouldBe("help");
        node.Options[1].LongName.ShouldBe("verbose");
    }

    [Fact]
    public void Parse_ContinuationLines()
    {
        var help = """
            options:
              --force-recreate      Recreate containers even if their
                                    configuration and image haven't changed.
              --no-build            Don't build an image.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(2);
        node.Options[0].LongName.ShouldBe("force-recreate");
        node.Options[0].Description!.ShouldContain("configuration and image");
        node.Options[1].LongName.ShouldBe("no-build");
    }

    [Fact]
    public void Create_Argparse_ReturnsArgparseHelpParser()
    {
        var parser = HelpParsers.Create("argparse");
        parser.ShouldBeOfType<ArgparseHelpParser>();
    }
}

// ── HelpParsers Registry Tests ─────────────────────────────────────────────

public class HelpParsersTests
{
    [Theory]
    [InlineData("standard")]
    [InlineData("packer")]
    [InlineData("cobra")]
    [InlineData("COBRA")]
    [InlineData("Standard")]
    [InlineData("argparse")]
    public void Create_KnownStrategy_ReturnsParser(string strategy)
    {
        var parser = HelpParsers.Create(strategy);
        parser.ShouldNotBeNull();
        parser.ShouldBeAssignableTo<IHelpParser>();
    }

    [Fact]
    public void Create_UnknownStrategy_Throws()
    {
        Should.Throw<ArgumentException>(() => HelpParsers.Create("unknown"));
    }

    [Fact]
    public void Create_Cobra_ReturnsCobraHelpParser()
    {
        var parser = HelpParsers.Create("cobra");
        parser.ShouldBeOfType<CobraHelpParser>();
    }

    [Fact]
    public void KnownStrategies_ContainsExpected()
    {
        var strategies = HelpParsers.KnownStrategies;
        strategies.ShouldContain("standard");
        strategies.ShouldContain("packer");
        strategies.ShouldContain("cobra");
        strategies.ShouldContain("argparse");
    }

    [Fact]
    public void Register_CustomStrategy_CanBeResolved()
    {
        HelpParsers.Register("test-custom", () => new StandardHelpParser());
        var parser = HelpParsers.Create("test-custom");
        parser.ShouldBeOfType<StandardHelpParser>();
    }
}

// ── ICommandTreeTransformer Tests ───────────────────────────────────────────

public class CommandTreeTransformerTests
{
    private sealed class TestTransformer : ICommandTreeTransformer
    {
        public CommandNode TransformRoot(CommandNode root)
            => new()
            {
                Name = root.Name,
                Description = "Transformed: " + root.Description,
                Options = root.Options,
                Arguments = root.Arguments,
                SubCommands = root.SubCommands
            };

        public CommandNode TransformCommand(string commandPath, CommandNode node)
        {
            if (node.Name == "build")
            {
                var b = CommandNodeBuilder.From(node);
                b.SetOptionType("parallel-builds", "int");
                return b.Build();
            }
            return node;
        }
    }

    [Fact]
    public void TransformCommand_MatchingName_AppliesTransform()
    {
        var transformer = new TestTransformer();
        var node = new CommandNodeBuilder("build")
            .AddOption("parallel-builds", valueKind: OptionValueKind.Single)
            .Build();

        var result = transformer.TransformCommand("packer.build", node);
        result.Options[0].ClrType.ShouldBe("int");
    }

    [Fact]
    public void TransformCommand_NonMatching_ReturnsUnchanged()
    {
        var transformer = new TestTransformer();
        var node = new CommandNodeBuilder("validate")
            .AddOption("flag")
            .Build();

        var result = transformer.TransformCommand("packer.validate", node);
        result.Options[0].ClrType.ShouldBe("string"); // default, unchanged
    }
}

// ── ScrapePipeline Tests ────────────────────────────────────────────────────

public class ScrapePipelineTests
{
    [Fact]
    public void GenerateDockerfile_WithConfig_ProducesCorrectOutput()
    {
        var pipeline = new ScrapePipeline()
            .Binary("packer")
            .FromImage("alpine:3.19")
            .BuildArg("PACKER_VERSION", "1.11.2")
            .Install("apk add --no-cache curl unzip")
            .Install("curl -fsSL https://example.com/packer.zip -o /tmp/packer.zip");

        var dockerfile = pipeline.GenerateDockerfile();

        dockerfile.ShouldContain("FROM alpine:3.19");
        dockerfile.ShouldContain("ARG PACKER_VERSION=1.11.2");
        dockerfile.ShouldContain("RUN apk add --no-cache curl unzip");
        dockerfile.ShouldContain("RUN curl -fsSL https://example.com/packer.zip -o /tmp/packer.zip");
    }

    [Fact]
    public void GenerateDockerfile_NoBaseImage_Throws()
    {
        var pipeline = new ScrapePipeline().Binary("packer");
        Should.Throw<InvalidOperationException>(() => pipeline.GenerateDockerfile());
    }

    [Fact]
    public async Task ExecuteAsync_NoBinary_Throws()
    {
        var pipeline = new ScrapePipeline();
        await Should.ThrowAsync<InvalidOperationException>(() => pipeline.ExecuteAsync());
    }

    [Fact]
    public async Task ExecuteAsync_WithRunHelp_ScrapesLocally()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .UseParser<StandardHelpParser>()
            .WithRunHelp(args => Task.FromResult(
                "A test tool\n\nCommands:\n  sub1    First subcommand\n"));

        var tree = await pipeline.ExecuteAsync();

        tree.BinaryName.ShouldBe("tool");
        tree.Root.SubCommands.Count.ShouldBe(1);
        tree.Root.SubCommands[0].Name.ShouldBe("sub1");
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomHelpFlag()
    {
        var receivedArgs = new List<string[]>();
        var pipeline = new ScrapePipeline()
            .Binary("packer")
            .HelpFlag("-h")
            .UseParser(new StandardHelpParser())
            .WithRunHelp(args =>
            {
                receivedArgs.Add(args);
                return Task.FromResult("A tool\n\nOptions:\n  --help    Print help\n");
            });

        await pipeline.ExecuteAsync();

        receivedArgs[0].ShouldBe(new[] { "packer", "-h" });
    }

    [Fact]
    public async Task ExecuteAsync_OutputTo_WritesFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bw-test-{Guid.NewGuid()}.json");
        try
        {
            var pipeline = new ScrapePipeline()
                .Binary("tool")
                .UseParser<StandardHelpParser>()
                .WithRunHelp(_ => Task.FromResult("A tool\n"))
                .OutputTo(path);

            await pipeline.ExecuteAsync();

            File.Exists(path).ShouldBeTrue();
            var tree = CommandTreeJsonSerializer.Deserialize(await File.ReadAllTextAsync(path));
            tree.ShouldNotBeNull();
            tree!.BinaryName.ShouldBe("tool");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_TransformRoot_Applied()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .WithRunHelp(_ => Task.FromResult("Original desc\n"))
            .TransformRoot(root => new CommandNode
            {
                Name = root.Name,
                Description = "Overridden",
                Options = root.Options,
                Arguments = root.Arguments,
                SubCommands = root.SubCommands
            });

        var tree = await pipeline.ExecuteAsync();
        tree.Root.Description.ShouldBe("Overridden");
    }

    [Fact]
    public async Task ExecuteAsync_TransformCommand_Applied()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .WithRunHelp(args =>
            {
                var key = string.Join(" ", args);
                return Task.FromResult(key switch
                {
                    "tool --help" => "Commands:\n  build    Build stuff\n",
                    "tool build --help" => "Build it\n\nOptions:\n  --parallel    Parallel\n",
                    _ => ""
                });
            })
            .TransformCommand("build", node =>
            {
                var b = CommandNodeBuilder.From(node);
                b.SetOptionType("parallel", "int");
                return b.Build();
            });

        var tree = await pipeline.ExecuteAsync();
        tree.Root.SubCommands[0].Options[0].ClrType.ShouldBe("int");
    }

    [Fact]
    public async Task ExecuteAsync_WithTransformer_Applied()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .WithRunHelp(args =>
            {
                var key = string.Join(" ", args);
                return Task.FromResult(key switch
                {
                    "tool --help" => "Commands:\n  sub    A sub\n",
                    "tool sub --help" => "A sub\n\nOptions:\n  --flag    A flag\n",
                    _ => ""
                });
            })
            .UseTransformer(new TestTransformerForPipeline());

        var tree = await pipeline.ExecuteAsync();
        // TestTransformerForPipeline uppercases descriptions
        tree.Root.SubCommands[0].Options[0].ClrType.ShouldBe("bool");
    }

    [Fact]
    public async Task ExecuteAsync_MaxDepth_Respected()
    {
        var callCount = 0;
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .MaxDepth(1)
            .WithRunHelp(_ =>
            {
                callCount++;
                return Task.FromResult("Commands:\n  deeper    Goes deeper\n");
            });

        await pipeline.ExecuteAsync();
        callCount.ShouldBeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithContainerRuntime_BuildsAndScrapes()
    {
        var buildCalled = false;
        var runResults = new Queue<string>();
        runResults.Enqueue("A tool\n\nOptions:\n  --help    Help\n");

        var mockRuntime = new MockContainerRuntime(
            onBuild: (tag, dockerfile) => { buildCalled = true; return Task.FromResult("ok"); },
            onRun: (tag, cmd) => Task.FromResult(runResults.Dequeue()),
            onRemove: tag => Task.CompletedTask);

        var pipeline = new ScrapePipeline()
            .Binary("packer")
            .FromImage("alpine:3.19")
            .Install("apk add packer")
            .WithRuntime(mockRuntime);

        var tree = await pipeline.ExecuteAsync();

        buildCalled.ShouldBeTrue();
        tree.BinaryName.ShouldBe("packer");
    }

    [Fact]
    public void ApplyTransforms_PureFunctionPreservesMetadata()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .TransformRoot(root => new CommandNode
            {
                Name = root.Name,
                Description = "new desc",
                Options = root.Options,
                Arguments = root.Arguments,
                SubCommands = root.SubCommands
            });

        var tree = new CommandTree
        {
            BinaryName = "tool",
            Version = "1.0.0",
            Description = "Tool desc",
            Root = new CommandNode { Name = "tool", Description = "old desc" }
        };

        var result = pipeline.ApplyTransforms(tree);
        result.BinaryName.ShouldBe("tool");
        result.Version.ShouldBe("1.0.0");
        result.Description.ShouldBe("Tool desc");
        result.Root.Description.ShouldBe("new desc");
    }

    private sealed class TestTransformerForPipeline : ICommandTreeTransformer
    {
        public CommandNode TransformRoot(CommandNode root) => root;

        public CommandNode TransformCommand(string commandPath, CommandNode node)
        {
            if (node.Name == "sub")
            {
                var b = CommandNodeBuilder.From(node);
                b.SetOptionType("flag", "bool");
                return b.Build();
            }
            return node;
        }
    }

}

// ── ProcessRunnerContainerRuntime Tests ──────────────────────────────────────

public class ProcessRunnerContainerRuntimeTests
{
    [Fact]
    public async Task RunAsync_PassesCorrectArgs()
    {
        string[]? receivedArgs = null;
        var runtime = new ProcessRunnerContainerRuntime(
            args => { receivedArgs = args; return Task.FromResult("output"); },
            "podman");

        await runtime.RunAsync("my-image:latest", ["packer", "-h"]);

        receivedArgs.ShouldNotBeNull();
        receivedArgs![0].ShouldBe("podman");
        receivedArgs[1].ShouldBe("run");
        receivedArgs[2].ShouldBe("--rm");
        receivedArgs[3].ShouldBe("my-image:latest");
        receivedArgs[4].ShouldBe("packer");
        receivedArgs[5].ShouldBe("-h");
    }

    [Fact]
    public async Task RemoveImageAsync_PassesCorrectArgs()
    {
        string[]? receivedArgs = null;
        var runtime = new ProcessRunnerContainerRuntime(
            args => { receivedArgs = args; return Task.FromResult(""); },
            "docker");

        await runtime.RemoveImageAsync("my-image:latest");

        receivedArgs.ShouldNotBeNull();
        receivedArgs!.ShouldBe(new[] { "docker", "rmi", "-f", "my-image:latest" });
    }

    [Fact]
    public async Task BuildAsync_WritesDockerfileAndBuilds()
    {
        string[]? receivedArgs = null;
        var runtime = new ProcessRunnerContainerRuntime(
            args => { receivedArgs = args; return Task.FromResult("build output"); },
            "podman");

        var result = await runtime.BuildAsync("test-tag", "FROM alpine:3.19\nRUN echo hello");

        receivedArgs.ShouldNotBeNull();
        receivedArgs![0].ShouldBe("podman");
        receivedArgs[1].ShouldBe("build");
        receivedArgs[2].ShouldBe("-t");
        receivedArgs[3].ShouldBe("test-tag");
        result.ShouldBe("build output");
    }
}

// ── ScrapeError Tests ───────────────────────────────────────────────────────

public class ScrapeErrorTests
{
    [Fact]
    public void ContainerBuildError_Properties()
    {
        var err = new ContainerBuildError("Build failed", "error output");
        err.Message.ShouldBe("Build failed");
        err.BuildOutput.ShouldBe("error output");
    }

    [Fact]
    public void ContainerRunError_Properties()
    {
        var err = new ContainerRunError("Run failed");
        err.Message.ShouldBe("Run failed");
    }

    [Fact]
    public void ParseError_Properties()
    {
        var err = new ParseError("Parse failed");
        err.Message.ShouldBe("Parse failed");
    }
}

// ── PodmanContainerRuntime Tests ────────────────────────────────────────────

public class PodmanContainerRuntimeTests
{
    [Fact]
    public async Task UsesPodmanBinary()
    {
        string[]? receivedArgs = null;
        var runtime = new PodmanContainerRuntime(
            args => { receivedArgs = args; return Task.FromResult(""); });

        await runtime.RunAsync("img:1", ["echo", "hi"]);

        receivedArgs.ShouldNotBeNull();
        receivedArgs![0].ShouldBe("podman");
        runtime.RuntimeBinary.ShouldBe("podman");
    }
}

// ── DockerContainerRuntime Tests ────────────────────────────────────────────

public class DockerContainerRuntimeTests
{
    [Fact]
    public async Task UsesDockerBinary()
    {
        string[]? receivedArgs = null;
        var runtime = new DockerContainerRuntime(
            args => { receivedArgs = args; return Task.FromResult(""); });

        await runtime.RunAsync("img:1", ["echo", "hi"]);

        receivedArgs.ShouldNotBeNull();
        receivedArgs![0].ShouldBe("docker");
        runtime.RuntimeBinary.ShouldBe("docker");
    }
}

// ── StaticVersionCollector Tests ────────────────────────────────────────────

public class StaticVersionCollectorTests
{
    [Fact]
    public async Task ReturnsExactVersions()
    {
        var collector = new StaticVersionCollector(["1.0.0", "2.0.0"]);
        var versions = await collector.CollectVersionsAsync();
        versions.ShouldBe(["1.0.0", "2.0.0"]);
    }
}

// ── GitHubReleasesVersionCollector Tests ─────────────────────────────────────

public class GitHubReleasesVersionCollectorTests
{
    private static HttpClient CreateMockHttpClient(string jsonResponse, string? linkHeader = null)
    {
        var handler = new FakeHttpHandler(jsonResponse, linkHeader);
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "test");
        return client;
    }

    [Fact]
    public async Task ParsesGitHubReleasesJson()
    {
        var json = """
        [
            {"tag_name": "v1.0.0", "prerelease": false},
            {"tag_name": "v2.0.0", "prerelease": false},
            {"tag_name": "v1.5.0", "prerelease": false}
        ]
        """;
        var collector = new GitHubReleasesVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json));

        var versions = await collector.CollectVersionsAsync();

        versions.Count.ShouldBe(3);
        versions[0].ShouldBe("1.0.0");
        versions[1].ShouldBe("1.5.0");
        versions[2].ShouldBe("2.0.0");
    }

    [Fact]
    public async Task FiltersOutPrereleases()
    {
        var json = """
        [
            {"tag_name": "v1.0.0", "prerelease": false},
            {"tag_name": "v2.0.0-rc1", "prerelease": true},
            {"tag_name": "v1.5.0", "prerelease": false}
        ]
        """;
        var collector = new GitHubReleasesVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json));

        var versions = await collector.CollectVersionsAsync();

        versions.Count.ShouldBe(2);
        versions.ShouldNotContain("2.0.0-rc1");
    }

    [Fact]
    public async Task StripsVPrefix()
    {
        var json = """[{"tag_name": "v3.2.1", "prerelease": false}]""";
        var collector = new GitHubReleasesVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json));

        var versions = await collector.CollectVersionsAsync();
        versions[0].ShouldBe("3.2.1");
    }

    [Fact]
    public async Task CustomTagToVersion()
    {
        var json = """[{"tag_name": "release-1.0.0", "prerelease": false}]""";
        var collector = new GitHubReleasesVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json),
            tagToVersion: tag => tag.Replace("release-", ""));

        var versions = await collector.CollectVersionsAsync();
        versions[0].ShouldBe("1.0.0");
    }

    [Fact]
    public void CompareVersionStrings_SortsSemantically()
    {
        var versions = new List<string> { "1.10.0", "1.9.0", "2.0.0", "1.2.3" };
        versions.Sort(GitHubReleasesVersionCollector.CompareVersionStrings);

        versions.ShouldBe(["1.2.3", "1.9.0", "1.10.0", "2.0.0"]);
    }

}

// ── VersionScrapeResult Tests ───────────────────────────────────────────────

public class VersionScrapeResultTests
{
    [Fact]
    public void Success_HasCorrectProperties()
    {
        var tree = new CommandTree
        {
            BinaryName = "test",
            Root = new CommandNode { Name = "test" }
        };
        var result = new VersionScrapeResult("1.0.0", true, tree, null);

        result.Version.ShouldBe("1.0.0");
        result.Success.ShouldBeTrue();
        result.Tree.ShouldNotBeNull();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Failure_HasCorrectProperties()
    {
        var result = new VersionScrapeResult("2.0.0", false, null, "timeout");

        result.Version.ShouldBe("2.0.0");
        result.Success.ShouldBeFalse();
        result.Tree.ShouldBeNull();
        result.ErrorMessage.ShouldBe("timeout");
    }

    [Fact]
    public void RecordEquality()
    {
        var r1 = new VersionScrapeResult("1.0.0", true, null, null);
        var r2 = new VersionScrapeResult("1.0.0", true, null, null);
        r1.ShouldBe(r2);
    }
}

// ── MultiVersionScraper Tests ───────────────────────────────────────────────

public class MultiVersionScraperTests
{
    [Fact]
    public async Task ScrapesAllVersions_WithMockCollectorAndPipeline()
    {
        var collector = new StaticVersionCollector(["1.0.0", "2.0.0"]);
        var scraper = new MultiVersionScraper(
            collector,
            version => new ScrapePipeline()
                .Binary("test")
                .WithRunHelp(_ => Task.FromResult($"Usage: test {version}\n\nCommands:\n  hello   Say hello")),
            maxParallelism: 2);

        var results = await scraper.ScrapeAllAsync();

        results.Count.ShouldBe(2);
        results.All(r => r.Success).ShouldBeTrue();
        results[0].Version.ShouldBe("1.0.0");
        results[1].Version.ShouldBe("2.0.0");
    }

    [Fact]
    public async Task FiltersVersions()
    {
        var collector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]);
        var scraper = new MultiVersionScraper(
            collector,
            version => new ScrapePipeline()
                .Binary("test")
                .WithRunHelp(_ => Task.FromResult("Usage: test\n")),
            maxParallelism: 2);

        var results = await scraper.ScrapeAsync(v => v.StartsWith("1.") || v.StartsWith("3."));

        results.Count.ShouldBe(2);
        results[0].Version.ShouldBe("1.0.0");
        results[1].Version.ShouldBe("3.0.0");
    }

    [Fact]
    public async Task ReportsProgress()
    {
        var collector = new StaticVersionCollector(["1.0.0", "2.0.0"]);
        var progressEvents = new List<(string Version, int Done, int Total)>();

        var scraper = new MultiVersionScraper(
            collector,
            version => new ScrapePipeline()
                .Binary("test")
                .WithRunHelp(_ => Task.FromResult("Usage: test\n")),
            maxParallelism: 1);

        scraper.Progress += (r, done, total) =>
            progressEvents.Add((r.Version, done, total));

        await scraper.ScrapeAllAsync();

        progressEvents.Count.ShouldBe(2);
        progressEvents.ShouldAllBe(e => e.Total == 2);
        progressEvents.Max(e => e.Done).ShouldBe(2);
    }

    [Fact]
    public async Task HandlesFailuresGracefully()
    {
        var collector = new StaticVersionCollector(["1.0.0", "2.0.0"]);

        var scraper = new MultiVersionScraper(
            collector,
            version =>
            {
                if (version == "1.0.0")
                {
                    // Missing .Binary() will throw InvalidOperationException
                    return new ScrapePipeline()
                        .WithRunHelp(_ => Task.FromResult(""));
                }
                return new ScrapePipeline()
                    .Binary("test")
                    .WithRunHelp(_ => Task.FromResult("Usage: test\n"));
            },
            maxParallelism: 1);

        var results = await scraper.ScrapeAllAsync();

        results.Count.ShouldBe(2);
        var failed = results.Single(r => r.Version == "1.0.0");
        failed.Success.ShouldBeFalse();
        failed.ErrorMessage!.ShouldContain("Binary name not specified");

        var succeeded = results.Single(r => r.Version == "2.0.0");
        succeeded.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyVersionList_ReturnsEmpty()
    {
        var collector = new StaticVersionCollector([]);
        var scraper = new MultiVersionScraper(
            collector,
            _ => new ScrapePipeline().Binary("test"),
            maxParallelism: 2);

        var results = await scraper.ScrapeAllAsync();
        results.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RespectsParallelismLimit()
    {
        var collector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0", "4.0.0"]);
        var maxConcurrent = 0;
        var currentConcurrent = 0;

        var scraper = new MultiVersionScraper(
            collector,
            version => new ScrapePipeline()
                .Binary("test")
                .WithRunHelp(async _ =>
                {
                    var c = Interlocked.Increment(ref currentConcurrent);
                    var snapshot = c;
                    while (snapshot > Volatile.Read(ref maxConcurrent))
                        Interlocked.CompareExchange(ref maxConcurrent, snapshot, Volatile.Read(ref maxConcurrent));
                    await Task.Delay(50);
                    Interlocked.Decrement(ref currentConcurrent);
                    return "Usage: test\n";
                }),
            maxParallelism: 2);

        await scraper.ScrapeAllAsync();

        maxConcurrent.ShouldBeInRange(1, 2);
    }
}
