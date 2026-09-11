using FrenchExDev.Net.BinaryWrapper.SourceGenerator;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.SourceGenerator.Tests;

// ── CommandTreeReader Tests ──────────────────────────────────────────────────

public sealed class CommandTreeReaderTests
{
    private const string SimpleJson = """
        {
            "binaryName": "packer",
            "version": "1.11.2",
            "description": "HashiCorp Packer",
            "root": {
                "name": "packer",
                "description": "HashiCorp Packer",
                "options": [],
                "arguments": [],
                "subCommands": [
                    {
                        "name": "build",
                        "description": "Build an image",
                        "options": [
                            {
                                "longName": "force",
                                "shortName": "f",
                                "description": "Force a build",
                                "valueKind": "flag",
                                "clrType": "bool",
                                "defaultValue": null,
                                "isRequired": false
                            },
                            {
                                "longName": "parallel-builds",
                                "shortName": null,
                                "description": "Number of parallel builds",
                                "valueKind": "single",
                                "clrType": "int",
                                "defaultValue": "0",
                                "isRequired": false
                            },
                            {
                                "longName": "var",
                                "shortName": null,
                                "description": "Set a variable",
                                "valueKind": "multiple",
                                "clrType": "string",
                                "defaultValue": null,
                                "isRequired": false
                            }
                        ],
                        "arguments": [
                            {
                                "name": "template",
                                "position": 0,
                                "description": "Path to template",
                                "clrType": "string",
                                "isRequired": true,
                                "isVariadic": false,
                                "defaultValue": null
                            }
                        ],
                        "subCommands": []
                    },
                    {
                        "name": "validate",
                        "description": "Validate a template",
                        "options": [
                            {
                                "longName": "syntax-only",
                                "shortName": null,
                                "description": "Only validate syntax",
                                "valueKind": "flag",
                                "clrType": "bool",
                                "defaultValue": null,
                                "isRequired": false
                            }
                        ],
                        "arguments": [
                            {
                                "name": "template",
                                "position": 0,
                                "description": "Path to template",
                                "clrType": "string",
                                "isRequired": true,
                                "isVariadic": false,
                                "defaultValue": null
                            }
                        ],
                        "subCommands": []
                    }
                ]
            }
        }
        """;

    [Fact]
    public void Parse_ValidJson_ReturnsCommandTree()
    {
        var tree = CommandTreeReader.Parse(SimpleJson);

        tree.ShouldNotBeNull();
        tree.BinaryName.ShouldBe("packer");
        tree.Version.ShouldBe("1.11.2");
        tree.Description.ShouldBe("HashiCorp Packer");
        tree.Root.Name.ShouldBe("packer");
    }

    [Fact]
    public void Parse_ValidJson_ParsesSubCommands()
    {
        var tree = CommandTreeReader.Parse(SimpleJson)!;

        tree.Root.SubCommands.Count.ShouldBe(2);
        tree.Root.SubCommands[0].Name.ShouldBe("build");
        tree.Root.SubCommands[1].Name.ShouldBe("validate");
    }

    [Fact]
    public void Parse_ValidJson_ParsesOptions()
    {
        var tree = CommandTreeReader.Parse(SimpleJson)!;
        var buildCmd = tree.Root.SubCommands[0];

        buildCmd.Options.Count.ShouldBe(3);
        buildCmd.Options[0].LongName.ShouldBe("force");
        buildCmd.Options[0].ShortName.ShouldBe("f");
        buildCmd.Options[0].ValueKind.ShouldBe("flag");
        buildCmd.Options[0].ClrType.ShouldBe("bool");
        buildCmd.Options[1].LongName.ShouldBe("parallel-builds");
        buildCmd.Options[1].ValueKind.ShouldBe("single");
        buildCmd.Options[1].ClrType.ShouldBe("int");
        buildCmd.Options[2].LongName.ShouldBe("var");
        buildCmd.Options[2].ValueKind.ShouldBe("multiple");
    }

    [Fact]
    public void Parse_ValidJson_ParsesArguments()
    {
        var tree = CommandTreeReader.Parse(SimpleJson)!;
        var buildCmd = tree.Root.SubCommands[0];

        buildCmd.Arguments.Count.ShouldBe(1);
        buildCmd.Arguments[0].Name.ShouldBe("template");
        buildCmd.Arguments[0].Position.ShouldBe(0);
        buildCmd.Arguments[0].IsRequired.ShouldBeTrue();
        buildCmd.Arguments[0].IsVariadic.ShouldBeFalse();
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        CommandTreeReader.Parse("not json at all").ShouldBeNull();
    }

    [Fact]
    public void Parse_EmptyJson_ReturnsNull()
    {
        CommandTreeReader.Parse("").ShouldBeNull();
    }

    [Fact]
    public void ExtractVersionFromFileName_ValidPattern_ReturnsVersion()
    {
        CommandTreeReader.ExtractVersionFromFileName("packer-1.11.2.json", "packer")
            .ShouldBe("1.11.2");
    }

    [Fact]
    public void ExtractVersionFromFileName_CaseInsensitive()
    {
        CommandTreeReader.ExtractVersionFromFileName("Packer-2.0.0.json", "packer")
            .ShouldBe("2.0.0");
    }

    [Fact]
    public void ExtractVersionFromFileName_NoMatch_ReturnsNull()
    {
        CommandTreeReader.ExtractVersionFromFileName("terraform-1.0.0.json", "packer")
            .ShouldBeNull();
    }

    [Fact]
    public void ExtractVersionFromFileName_WrongExtension_ReturnsNull()
    {
        CommandTreeReader.ExtractVersionFromFileName("packer-1.0.0.txt", "packer")
            .ShouldBeNull();
    }
}

// ── NamingHelper Tests ───────────────────────────────────────────────────────

public sealed class NamingHelperTests
{
    [Theory]
    [InlineData("parallel-builds", "ParallelBuilds")]
    [InlineData("force", "Force")]
    [InlineData("timestamp_ui", "TimestampUi")]
    [InlineData("syntax-only", "SyntaxOnly")]
    [InlineData("plugins.install", "PluginsInstall")]
    [InlineData("", "")]
    public void ToPascalCase_ConvertsCorrectly(string input, string expected)
    {
        NamingHelper.ToPascalCase(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("bool", "bool")]
    [InlineData("int", "int")]
    [InlineData("string", "string")]
    [InlineData("unknown_type", "string")]
    [InlineData("long", "long")]
    [InlineData("double", "double")]
    [InlineData("float", "float")]
    public void MapClrType_MapsCorrectly(string input, string expected)
    {
        NamingHelper.MapClrType(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("bool", "bool?")]
    [InlineData("int", "int?")]
    [InlineData("string", "string?")]
    public void NullableType_AddsNullable(string input, string expected)
    {
        NamingHelper.NullableType(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("bool", true)]
    [InlineData("int", true)]
    [InlineData("string", false)]
    public void IsValueType_ReturnsCorrectly(string input, bool expected)
    {
        NamingHelper.IsValueType(input).ShouldBe(expected);
    }

    [Fact]
    public void CommandClassName_BuildsFromPathSegments()
    {
        var cmd = new UnifiedCommand
        {
            PathSegments = new List<string> { "packer", "build" },
            Name = "build"
        };
        NamingHelper.CommandClassName("packer", cmd).ShouldBe("PackerBuildCommand");
    }

    [Fact]
    public void BuilderClassName_AppendBuilder()
    {
        var cmd = new UnifiedCommand
        {
            PathSegments = new List<string> { "packer", "build" },
            Name = "build"
        };
        NamingHelper.BuilderClassName("packer", cmd).ShouldBe("PackerBuildCommandBuilder");
    }

    [Fact]
    public void ClientClassName_ReturnsCorrect()
    {
        NamingHelper.ClientClassName("packer").ShouldBe("PackerClient");
    }

    [Fact]
    public void EntryClassName_ReturnsCorrect()
    {
        NamingHelper.EntryClassName("packer").ShouldBe("Packer");
    }

    [Fact]
    public void SemanticVersionCtor_Generates3Parts()
    {
        var result = NamingHelper.SemanticVersionCtor("1.11.2");
        result.ShouldBe("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(1, 11, 2)");
    }

    [Fact]
    public void SemanticVersionCtor_Handles2Parts()
    {
        var result = NamingHelper.SemanticVersionCtor("2.0");
        result.ShouldBe("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(2, 0, 0)");
    }

    [Fact]
    public void EscapeString_EscapesQuotesAndBackslashes()
    {
        NamingHelper.EscapeString("say \"hello\"").ShouldBe("say \\\"hello\\\"");
        NamingHelper.EscapeString("path\\to").ShouldBe("path\\\\to");
    }
}

// ── VersionDiffer Tests ──────────────────────────────────────────────────────

public sealed class VersionDifferTests
{
    private static CommandTreeModel MakeTree(string binaryName, params (string name, string[] opts)[] commands)
    {
        var root = new CommandNodeModel
        {
            Name = binaryName,
            SubCommands = commands.Select(c => new CommandNodeModel
            {
                Name = c.name,
                Description = $"{c.name} command",
                Options = c.opts.Select(o => new OptionModel
                {
                    LongName = o,
                    ValueKind = "single",
                    ClrType = "string"
                }).ToList()
            }).ToList()
        };
        return new CommandTreeModel { BinaryName = binaryName, Root = root };
    }

    [Fact]
    public void FromSingle_CreatesUnifiedTree_NoVersionAnnotations()
    {
        var tree = MakeTree("packer", ("build", new[] { "force", "color" }), ("validate", new[] { "syntax-only" }));
        var unified = VersionDiffer.FromSingle("1.0.0", tree);

        unified.BinaryName.ShouldBe("packer");
        unified.Commands.Count.ShouldBe(2);

        var build = unified.Commands.First(c => c.Name == "build");
        build.SinceVersion.ShouldBeNull();
        build.UntilVersion.ShouldBeNull();
        build.Options.Count.ShouldBe(2);
        build.Options[0].SinceVersion.ShouldBeNull();
        build.Options[0].UntilVersion.ShouldBeNull();
    }

    [Fact]
    public void FromSingle_SetsPathSegments()
    {
        var tree = MakeTree("packer", ("build", new[] { "force" }));
        var unified = VersionDiffer.FromSingle("1.0.0", tree);

        var build = unified.Commands[0];
        build.PathSegments.ShouldBe(new List<string> { "packer", "build" });
    }

    [Fact]
    public void Merge_EmptyList_ReturnsEmptyTree()
    {
        var result = VersionDiffer.Merge(new List<(string, CommandTreeModel)>());
        result.Commands.Count.ShouldBe(0);
    }

    [Fact]
    public void Merge_TwoVersions_CommandPresentInBoth_NoVersionAnnotation()
    {
        var v1 = MakeTree("packer", ("build", new[] { "force" }));
        var v2 = MakeTree("packer", ("build", new[] { "force" }));

        var unified = VersionDiffer.Merge(new List<(string, CommandTreeModel)>
        {
            ("1.0.0", v1),
            ("1.1.0", v2)
        });

        unified.Commands.Count.ShouldBe(1);
        var build = unified.Commands[0];
        build.SinceVersion.ShouldBe("1.0.0");
        build.UntilVersion.ShouldBeNull(); // present in latest
    }

    [Fact]
    public void Merge_CommandAddedInLaterVersion_HasSinceVersion()
    {
        var v1 = MakeTree("packer", ("build", new[] { "force" }));
        var v2 = MakeTree("packer", ("build", new[] { "force" }), ("validate", new[] { "syntax-only" }));

        var unified = VersionDiffer.Merge(new List<(string, CommandTreeModel)>
        {
            ("1.0.0", v1),
            ("1.1.0", v2)
        });

        unified.Commands.Count.ShouldBe(2);
        var validate = unified.Commands.First(c => c.Name == "validate");
        validate.SinceVersion.ShouldBe("1.1.0");
        validate.UntilVersion.ShouldBeNull();
    }

    [Fact]
    public void Merge_CommandRemovedInLaterVersion_HasUntilVersion()
    {
        var v1 = MakeTree("packer", ("build", new[] { "force" }), ("up", new string[0]));
        var v2 = MakeTree("packer", ("build", new[] { "force" }));

        var unified = VersionDiffer.Merge(new List<(string, CommandTreeModel)>
        {
            ("1.0.0", v1),
            ("1.1.0", v2)
        });

        var up = unified.Commands.First(c => c.Name == "up");
        up.SinceVersion.ShouldBe("1.0.0");
        up.UntilVersion.ShouldBe("1.1.0"); // removed in 1.1.0
    }

    [Fact]
    public void Merge_OptionAddedInLaterVersion_HasSinceVersion()
    {
        var v1 = MakeTree("packer", ("build", new[] { "force" }));
        var v2 = MakeTree("packer", ("build", new[] { "force", "timestamp-ui" }));

        var unified = VersionDiffer.Merge(new List<(string, CommandTreeModel)>
        {
            ("1.0.0", v1),
            ("1.1.0", v2)
        });

        var build = unified.Commands[0];
        var force = build.Options.First(o => o.LongName == "force");
        force.SinceVersion.ShouldBeNull(); // exists in all versions

        var ts = build.Options.First(o => o.LongName == "timestamp-ui");
        ts.SinceVersion.ShouldBe("1.1.0");
    }

    [Fact]
    public void Merge_ThreeVersions_CorrectBoundaries()
    {
        var v1 = MakeTree("packer", ("build", new[] { "force" }));
        var v2 = MakeTree("packer", ("build", new[] { "force", "color" }), ("init", new string[0]));
        var v3 = MakeTree("packer", ("build", new[] { "force", "color" }), ("init", new string[0]));

        var unified = VersionDiffer.Merge(new List<(string, CommandTreeModel)>
        {
            ("1.0.0", v1),
            ("1.5.0", v2),
            ("2.0.0", v3)
        });

        var init = unified.Commands.First(c => c.Name == "init");
        init.SinceVersion.ShouldBe("1.5.0");
        init.UntilVersion.ShouldBeNull();

        var color = unified.Commands.First(c => c.Name == "build")
            .Options.First(o => o.LongName == "color");
        color.SinceVersion.ShouldBe("1.5.0");
    }

    [Fact]
    public void Merge_SortsVersionsBeforeProcessing()
    {
        // Provide versions out of order
        var v2 = MakeTree("packer", ("build", new[] { "force" }));
        var v1 = MakeTree("packer", ("build", new[] { "force" }), ("up", new string[0]));

        var unified = VersionDiffer.Merge(new List<(string, CommandTreeModel)>
        {
            ("2.0.0", v2), // later version first
            ("1.0.0", v1)  // earlier version second
        });

        var up = unified.Commands.First(c => c.Name == "up");
        up.SinceVersion.ShouldBe("1.0.0");
        up.UntilVersion.ShouldBe("2.0.0");
    }
}

// ── CommandClassEmitter Tests ────────────────────────────────────────────────

public sealed class CommandClassEmitterTests
{
    private static DescriptorModel MakeDescriptor(
        string flagPrefix = "--", string flagValueSeparator = " ", bool useBoolEquals = false)
        => new("TestNs", "TestDescriptor", "packer", flagPrefix, flagValueSeparator, useBoolEquals, null);

    private static UnifiedCommand MakeBuildCommand() => new()
    {
        CommandPath = "packer.build",
        PathSegments = new List<string> { "packer", "build" },
        Name = "build",
        Description = "Build an image",
        Options = new List<UnifiedOption>
        {
            new() { LongName = "force", ValueKind = "flag", ClrType = "bool" },
            new() { LongName = "parallel-builds", ValueKind = "single", ClrType = "int" },
            new() { LongName = "var", ValueKind = "multiple", ClrType = "string" }
        },
        Arguments = new List<UnifiedArgument>
        {
            new() { Name = "template", Position = 0, ClrType = "string", IsRequired = true }
        }
    };

    [Fact]
    public void Emit_ContainsClassName()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("public sealed partial class PackerBuildCommand");
    }

    [Fact]
    public void Emit_ContainsNamespace()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("namespace TestNs;");
    }

    [Fact]
    public void Emit_ImplementsICliCommand()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain(": global::FrenchExDev.Net.BinaryWrapper.ICliCommand");
    }

    [Fact]
    public void Emit_ContainsFlagProperty()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("public bool? Force { get; init; }");
    }

    [Fact]
    public void Emit_ContainsSingleValueProperty()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("public int? ParallelBuilds { get; init; }");
    }

    [Fact]
    public void Emit_ContainsMultipleValueProperty()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("public global::System.Collections.Generic.IReadOnlyList<string>? Var { get; init; }");
    }

    [Fact]
    public void Emit_ContainsArgumentProperty()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("public string? Template { get; init; }");
    }

    [Fact]
    public void Emit_ContainsCommandPath()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("CommandPath");
        source.ShouldContain("\"build\"");
    }

    [Fact]
    public void Emit_ContainsToArguments()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("ToArguments()");
    }

    [Fact]
    public void Emit_FlagPrefix_AppliedToArguments()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(flagPrefix: "-"), MakeBuildCommand());
        source.ShouldContain("\"-force\"");
        source.ShouldContain("\"-parallel-builds\"");
    }

    [Fact]
    public void Emit_FlagValueSeparator_Equals_UsesEqualsSign()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(flagValueSeparator: "="), MakeBuildCommand());
        source.ShouldContain("\"--parallel-builds=\" +");
    }

    [Fact]
    public void Emit_UseBoolEqualsFormat_EmitsBoolValue()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(useBoolEquals: true), MakeBuildCommand());
        source.ShouldContain("\"--force=\" + (Force.Value ? \"true\" : \"false\")");
    }

    [Fact]
    public void Emit_Description_AddedAsXmlDoc()
    {
        var source = CommandClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("/// <summary>Build an image</summary>");
    }

    [Fact]
    public void Emit_SinceVersion_AddsAttribute()
    {
        var cmd = MakeBuildCommand();
        cmd.SinceVersion = "1.5.0";
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.SinceVersion(\"1.5.0\")]");
    }

    [Fact]
    public void Emit_UntilVersion_AddsAttribute()
    {
        var cmd = MakeBuildCommand();
        cmd.UntilVersion = "2.0.0";
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.UntilVersion(\"2.0.0\")]");
    }

    [Fact]
    public void Emit_OptionSinceVersion_AddsPropertyAttribute()
    {
        var cmd = MakeBuildCommand();
        cmd.Options[1].SinceVersion = "1.3.0"; // parallel-builds
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        // The SinceVersion attribute should appear before the property
        var idx1 = source.IndexOf("[global::FrenchExDev.Net.BinaryWrapper.SinceVersion(\"1.3.0\")]");
        var idx2 = source.IndexOf("ParallelBuilds { get; init; }");
        idx1.ShouldBeGreaterThan(-1);
        idx2.ShouldBeGreaterThan(idx1);
    }

    [Fact]
    public void Emit_NoNamespace_OmitsNamespaceLine()
    {
        var descriptor = new DescriptorModel("", "TestDescriptor", "packer", "--", " ", false, null);
        var source = CommandClassEmitter.Emit(descriptor, MakeBuildCommand());
        source.ShouldNotContain("namespace ");
    }

    [Fact]
    public void Emit_VariadicArgument_EmitsListType()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>(),
            Arguments = new List<UnifiedArgument>
            {
                new() { Name = "files", Position = 0, ClrType = "string", IsVariadic = true }
            }
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("IReadOnlyList<string>? Files { get; init; }");
    }
}

// ── BuilderClassEmitter Tests ────────────────────────────────────────────────

public sealed class BuilderClassEmitterTests
{
    private static DescriptorModel MakeDescriptor()
        => new("TestNs", "TestDescriptor", "packer", "--", " ", false, null);

    private static UnifiedCommand MakeBuildCommand() => new()
    {
        CommandPath = "packer.build",
        PathSegments = new List<string> { "packer", "build" },
        Name = "build",
        Options = new List<UnifiedOption>
        {
            new() { LongName = "force", ValueKind = "flag", ClrType = "bool" },
            new() { LongName = "parallel-builds", ValueKind = "single", ClrType = "int" },
            new() { LongName = "var", ValueKind = "multiple", ClrType = "string" }
        },
        Arguments = new List<UnifiedArgument>
        {
            new() { Name = "template", Position = 0, ClrType = "string", IsRequired = true }
        }
    };

    [Fact]
    public void Emit_ContainsBuilderClassName()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("public partial class PackerBuildCommandBuilder");
    }

    [Fact]
    public void Emit_ExtendsAbstractBuilder()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("global::FrenchExDev.Net.Builder.AbstractBuilder<");
        source.ShouldContain("global::TestNs.PackerBuildCommand>");
    }

    [Fact]
    public void Emit_HasDetectedVersionField()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("SemanticVersion? _detectedVersion");
    }

    [Fact]
    public void Emit_HasConstructorWithDetectedVersion()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("public PackerBuildCommandBuilder(global::FrenchExDev.Net.BinaryWrapper.SemanticVersion? detectedVersion = null)");
    }

    [Fact]
    public void Emit_HasInputProperties()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("protected bool? Force { get; private set; }");
        source.ShouldContain("protected int? ParallelBuilds { get; private set; }");
        source.ShouldContain("protected global::System.Collections.Generic.List<string>? Var { get; private set; }");
        source.ShouldContain("protected string? Template { get; private set; }");
    }

    [Fact]
    public void Emit_HasFluentWithMethods()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("public PackerBuildCommandBuilder WithForce(bool? value)");
        source.ShouldContain("public PackerBuildCommandBuilder WithParallelBuilds(int? value)");
        source.ShouldContain("public PackerBuildCommandBuilder WithVar(");
        source.ShouldContain("public PackerBuildCommandBuilder WithTemplate(string? value)");
    }

    [Fact]
    public void Emit_WithMethod_ReturnsSelf()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("Force = value;");
        source.ShouldContain("return this;");
    }

    [Fact]
    public void Emit_HasVirtualValidationMethods()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("protected virtual global::System.Collections.Generic.IEnumerable<global::System.Exception>? ValidateForce(");
        source.ShouldContain("protected virtual global::System.Collections.Generic.IEnumerable<global::System.Exception>? ValidateParallelBuilds(");
        source.ShouldContain("protected virtual global::System.Collections.Generic.IEnumerable<global::System.Exception>? ValidateVar(");
        source.ShouldContain("protected virtual global::System.Collections.Generic.IEnumerable<global::System.Exception>? ValidateTemplate(");
    }

    [Fact]
    public void Emit_MultipleOption_HasItemValidator()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("ValidateVarItem(string item, int index)");
    }

    [Fact]
    public void Emit_HasValidateAsync()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("protected override global::System.Threading.Tasks.Task<");
        source.ShouldContain("ValidationResult>> ValidateAsync(");
    }

    [Fact]
    public void Emit_HasBuildException()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("protected override global::System.Exception BuildException(");
    }

    [Fact]
    public void Emit_HasSealedInstantiate()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("protected sealed override async");
        source.ShouldContain("Instantiate(");
        source.ShouldContain("reference.Resolve(__value);");
    }

    [Fact]
    public void Emit_InstantiateConstructsCommand()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        source.ShouldContain("Force = Force,");
        source.ShouldContain("ParallelBuilds = ParallelBuilds,");
        source.ShouldContain("Var = Var?.AsReadOnly(),");
        source.ShouldContain("Template = Template,");
    }

    [Fact]
    public void Emit_VersionGuard_OnOptionWithSinceVersion()
    {
        var cmd = MakeBuildCommand();
        cmd.Options[1].SinceVersion = "1.5.0"; // parallel-builds
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("VersionGuard.EnsureOptionSupported(");
        source.ShouldContain("\"parallel-builds\"");
        source.ShouldContain("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(1, 5, 0)");
    }

    [Fact]
    public void Emit_NoVersionGuard_WhenNoVersionBounds()
    {
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), MakeBuildCommand());
        // Force option has no version bounds, should not have guard
        var forceMethodIdx = source.IndexOf("WithForce(bool?");
        var nextMethodIdx = source.IndexOf("WithParallelBuilds(");
        var forceSection = source.Substring(forceMethodIdx, nextMethodIdx - forceMethodIdx);
        forceSection.ShouldNotContain("VersionGuard");
    }
}

// ── ClientClassEmitter Tests ─────────────────────────────────────────────────

public sealed class ClientClassEmitterTests
{
    private static DescriptorModel MakeDescriptor()
        => new("TestNs", "TestDescriptor", "packer", "--", " ", false, null);

    private static UnifiedCommandTree MakeTree() => new()
    {
        BinaryName = "packer",
        Commands = new List<UnifiedCommand>
        {
            new()
            {
                CommandPath = "packer.build",
                PathSegments = new List<string> { "packer", "build" },
                Name = "build",
                Description = "Build an image",
                Options = new List<UnifiedOption>(),
                Arguments = new List<UnifiedArgument>()
            },
            new()
            {
                CommandPath = "packer.validate",
                PathSegments = new List<string> { "packer", "validate" },
                Name = "validate",
                Description = "Validate a template",
                Options = new List<UnifiedOption>(),
                Arguments = new List<UnifiedArgument>()
            }
        }
    };

    [Fact]
    public void Emit_ContainsEntryClass()
    {
        var source = ClientClassEmitter.Emit(MakeDescriptor(), MakeTree());
        source.ShouldContain("public static partial class Packer");
    }

    [Fact]
    public void Emit_ContainsCreateMethod()
    {
        var source = ClientClassEmitter.Emit(MakeDescriptor(), MakeTree());
        source.ShouldContain("public static PackerClient Create(global::FrenchExDev.Net.BinaryWrapper.BinaryBinding binding)");
    }

    [Fact]
    public void Emit_ContainsClientClass()
    {
        var source = ClientClassEmitter.Emit(MakeDescriptor(), MakeTree());
        source.ShouldContain("public partial class PackerClient");
    }

    [Fact]
    public void Emit_ClientHasBindingField()
    {
        var source = ClientClassEmitter.Emit(MakeDescriptor(), MakeTree());
        source.ShouldContain("private readonly global::FrenchExDev.Net.BinaryWrapper.BinaryBinding _binding;");
    }

    [Fact]
    public void Emit_ClientHasDetectedVersionField()
    {
        var source = ClientClassEmitter.Emit(MakeDescriptor(), MakeTree());
        source.ShouldContain("private readonly global::FrenchExDev.Net.BinaryWrapper.SemanticVersion? _detectedVersion;");
    }

    [Fact]
    public void Emit_HasPerCommandMethods()
    {
        var source = ClientClassEmitter.Emit(MakeDescriptor(), MakeTree());
        source.ShouldContain("public async global::System.Threading.Tasks.Task<PackerBuildCommand> BuildAsync(global::System.Action<PackerBuildCommandBuilder> configure)");
        source.ShouldContain("public async global::System.Threading.Tasks.Task<PackerValidateCommand> ValidateAsync(global::System.Action<PackerValidateCommandBuilder> configure)");
    }

    [Fact]
    public void Emit_MethodCreatesBuilderAndConfigures()
    {
        var source = ClientClassEmitter.Emit(MakeDescriptor(), MakeTree());
        source.ShouldContain("var __builder = new PackerBuildCommandBuilder(_detectedVersion);");
        source.ShouldContain("configure(__builder);");
    }

    [Fact]
    public void Emit_VersionGuardOnCommand_WhenHasSince()
    {
        var tree = MakeTree();
        tree.Commands[0].SinceVersion = "1.0.0";
        tree.Commands[0].UntilVersion = "2.0.0";
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);
        source.ShouldContain("VersionGuard.EnsureCommandSupported(");
        source.ShouldContain("\"build\"");
    }

    [Fact]
    public void Emit_NoVersionGuard_WhenNoVersionBounds()
    {
        var source = ClientClassEmitter.Emit(MakeDescriptor(), MakeTree());
        source.ShouldNotContain("VersionGuard");
    }

    [Fact]
    public void Emit_NestedCommandGroups()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "packer",
            Commands = new List<UnifiedCommand>
            {
                new()
                {
                    CommandPath = "packer.build",
                    PathSegments = new List<string> { "packer", "build" },
                    Name = "build",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                },
                new()
                {
                    CommandPath = "packer.plugins.install",
                    PathSegments = new List<string> { "packer", "plugins", "install" },
                    Name = "install",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                },
                new()
                {
                    CommandPath = "packer.plugins.remove",
                    PathSegments = new List<string> { "packer", "plugins", "remove" },
                    Name = "remove",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                }
            }
        };

        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        // Should have a nested group for plugins
        source.ShouldContain("public PackerClientPluginsGroup Plugins =>");
        source.ShouldContain("public partial class PackerClientPluginsGroup");

        // Root-level build method
        source.ShouldContain("BuildAsync(");

        // Nested install/remove methods
        source.ShouldContain("InstallAsync(");
        source.ShouldContain("RemoveAsync(");
    }

    [Fact]
    public void Emit_Namespace()
    {
        var source = ClientClassEmitter.Emit(MakeDescriptor(), MakeTree());
        source.ShouldContain("namespace TestNs;");
    }

    [Fact]
    public void Emit_AutoGenerated()
    {
        var source = ClientClassEmitter.Emit(MakeDescriptor(), MakeTree());
        source.ShouldContain("// <auto-generated/>");
    }
}

// ── Diagnostics Descriptor Tests ─────────────────────────────────────────────

public sealed class DiagnosticsTests
{
    [Fact]
    public void BW001_HasCorrectId()
    {
        Diagnostics.BW001_NoFilesFound.Id.ShouldBe("BW001");
    }

    [Fact]
    public void BW002_HasCorrectId()
    {
        Diagnostics.BW002_JsonParseError.Id.ShouldBe("BW002");
    }

    [Fact]
    public void BW004_HasCorrectId()
    {
        Diagnostics.BW004_NonPartialClass.Id.ShouldBe("BW004");
    }

    [Fact]
    public void BW001_IsWarning()
    {
        Diagnostics.BW001_NoFilesFound.DefaultSeverity.ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
    }

    [Fact]
    public void BW002_IsError()
    {
        Diagnostics.BW002_JsonParseError.DefaultSeverity.ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void BW004_IsError()
    {
        Diagnostics.BW004_NonPartialClass.DefaultSeverity.ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }
}
