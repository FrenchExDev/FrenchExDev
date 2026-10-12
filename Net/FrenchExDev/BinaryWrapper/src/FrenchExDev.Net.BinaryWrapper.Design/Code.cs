using System.Text.Json;
using System.Text.Json.Serialization;
using FrenchExDev.Net.Wrapper.Versioning;

namespace FrenchExDev.Net.BinaryWrapper.Design;

// ── Option Value Kind ───────────────────────────────────────────────────────

public enum OptionValueKind
{
    Flag,
    Single,
    Multiple
}

// ── OptionDefinition ────────────────────────────────────────────────────────

public sealed record OptionDefinition
{
    public required string LongName { get; init; }
    public string? ShortName { get; init; }
    public string? Description { get; init; }
    public OptionValueKind ValueKind { get; init; } = OptionValueKind.Single;
    public string ClrType { get; init; } = "string";
    public string? DefaultValue { get; init; }
    public bool IsRequired { get; init; }
}

// ── ArgumentDefinition ──────────────────────────────────────────────────────

public sealed record ArgumentDefinition
{
    public required string Name { get; init; }
    public int Position { get; init; }
    public string? Description { get; init; }
    public string ClrType { get; init; } = "string";
    public bool IsRequired { get; init; } = true;
    public bool IsVariadic { get; init; }
    public string? DefaultValue { get; init; }
}

// ── CommandNode ─────────────────────────────────────────────────────────────

public sealed class CommandNode
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<OptionDefinition> Options { get; init; } = [];
    public IReadOnlyList<ArgumentDefinition> Arguments { get; init; } = [];
    public IReadOnlyList<CommandNode> SubCommands { get; init; } = [];

    [JsonIgnore]
    public bool IsLeaf => SubCommands.Count == 0;

    public IEnumerable<(string[] Path, CommandNode Node)> GetLeafCommands()
    {
        return GetLeafCommandsCore([Name]);
    }

    private IEnumerable<(string[] Path, CommandNode Node)> GetLeafCommandsCore(
        List<string> currentPath)
    {
        if (IsLeaf)
        {
            yield return (currentPath.ToArray(), this);
            yield break;
        }

        foreach (var sub in SubCommands)
        {
            var subPath = new List<string>(currentPath) { sub.Name };
            foreach (var leaf in sub.GetLeafCommandsCore(subPath))
                yield return leaf;
        }
    }

    public CommandNode? FindByPath(IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
            return this;

        var child = SubCommands.FirstOrDefault(c =>
            string.Equals(c.Name, segments[0], StringComparison.OrdinalIgnoreCase));
        return child?.FindByPath(segments.Skip(1).ToList());
    }
}

// ── CommandNodeBuilder ─────────────────────────────────────────────────────

public class CommandNodeBuilder
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public List<OptionDefinition> Options { get; set; } = [];
    public List<ArgumentDefinition> Arguments { get; set; } = [];
    public List<CommandNode> SubCommands { get; set; } = [];

    public CommandNodeBuilder(string name) => Name = name;

    public CommandNodeBuilder AddSubCommand(CommandNode subCommand)
    {
        SubCommands.Add(subCommand);
        return this;
    }

    public CommandNodeBuilder AddSubCommand(string name, string? description = null,
        Action<CommandNodeBuilder>? configure = null)
    {
        var child = new CommandNodeBuilder(name) { Description = description };
        configure?.Invoke(child);
        SubCommands.Add(child.Build());
        return this;
    }

    public CommandNodeBuilder AddOption(OptionDefinition option)
    {
        Options.Add(option);
        return this;
    }

    public CommandNodeBuilder AddOption(string longName, string? shortName = null,
        string? description = null, OptionValueKind valueKind = OptionValueKind.Single,
        string clrType = "string", string? defaultValue = null, bool isRequired = false)
    {
        Options.Add(new OptionDefinition
        {
            LongName = longName,
            ShortName = shortName,
            Description = description,
            ValueKind = valueKind,
            ClrType = clrType,
            DefaultValue = defaultValue,
            IsRequired = isRequired
        });
        return this;
    }

    public CommandNodeBuilder AddArgument(ArgumentDefinition argument)
    {
        Arguments.Add(argument);
        return this;
    }

    public CommandNodeBuilder AddArgument(string name, int position = 0,
        string? description = null, bool isRequired = true, bool isVariadic = false,
        string clrType = "string", string? defaultValue = null)
    {
        Arguments.Add(new ArgumentDefinition
        {
            Name = name,
            Position = position,
            Description = description,
            ClrType = clrType,
            IsRequired = isRequired,
            IsVariadic = isVariadic,
            DefaultValue = defaultValue
        });
        return this;
    }

    public static CommandNodeBuilder From(CommandNode node)
    {
        var builder = new CommandNodeBuilder(node.Name) { Description = node.Description };
        builder.Options.AddRange(node.Options);
        builder.Arguments.AddRange(node.Arguments);
        builder.SubCommands.AddRange(node.SubCommands);
        return builder;
    }

    public CommandNodeBuilder SetOptionType(string longName, string clrType)
    {
        for (var i = 0; i < Options.Count; i++)
        {
            if (string.Equals(Options[i].LongName, longName, StringComparison.OrdinalIgnoreCase))
            {
                Options[i] = Options[i] with { ClrType = clrType };
                return this;
            }
        }
        return this;
    }

    public CommandNodeBuilder SetOptionKind(string longName, OptionValueKind kind)
    {
        for (var i = 0; i < Options.Count; i++)
        {
            if (string.Equals(Options[i].LongName, longName, StringComparison.OrdinalIgnoreCase))
            {
                Options[i] = Options[i] with { ValueKind = kind };
                return this;
            }
        }
        return this;
    }

    public CommandNodeBuilder SetArgumentRequired(string name, bool isRequired = true)
    {
        for (var i = 0; i < Arguments.Count; i++)
        {
            if (string.Equals(Arguments[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                Arguments[i] = Arguments[i] with { IsRequired = isRequired };
                return this;
            }
        }
        return this;
    }

    public CommandNode Build() => new()
    {
        Name = Name,
        Description = Description,
        Options = Options.ToList(),
        Arguments = Arguments.ToList(),
        SubCommands = SubCommands.ToList()
    };
}

// ── CommandTree ─────────────────────────────────────────────────────────────

public sealed class CommandTree
{
    public required string BinaryName { get; init; }
    public string? Version { get; init; }
    public string? Description { get; init; }
    public required CommandNode Root { get; init; }
}

// ── JSON Serialization ──────────────────────────────────────────────────────

public static class CommandTreeJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(CommandTree tree)
        => JsonSerializer.Serialize(tree, Options);

    public static CommandTree? Deserialize(string json)
        => JsonSerializer.Deserialize<CommandTree>(json, Options);

    public static async Task SerializeToFileAsync(CommandTree tree, string path,
        CancellationToken ct = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, tree, Options, ct);
    }

    public static async Task<CommandTree?> DeserializeFromFileAsync(string path,
        CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CommandTree>(stream, Options, ct);
    }
}

// ── CLI Commands ────────────────────────────────────────────────────────────

internal interface IToolCommand
{
    string Name { get; }
    string Description { get; }
    Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default);
}

internal sealed class NewCommand : IToolCommand
{
    public string Name => "new";
    public string Description => "Create a new binary wrapper solution";

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: binary-wrapper new <binary-name>");
            return 1;
        }

        var binaryName = args[0];
        var outputDir = args.Length > 1
            ? args[1]
            : Path.Combine(Directory.GetCurrentDirectory(), binaryName);

        await GenerateSolutionAsync(binaryName, outputDir, cancellationToken);

        Console.WriteLine($"Created wrapper solution for '{binaryName}' at: {outputDir}");
        return 0;
    }

    internal static async Task GenerateSolutionAsync(string binaryName, string outputDir,
        CancellationToken ct = default)
    {
        var (dirs, files) = PrepareSolution(binaryName, outputDir);

        foreach (var dir in dirs)
            Directory.CreateDirectory(dir);

        foreach (var (path, content) in files)
            await File.WriteAllTextAsync(path, content, ct);
    }

    internal static (string[] Dirs, Dictionary<string, string> Files) PrepareSolution(
        string binaryName, string outputDir)
    {
        var pascal = ToPascalCase(binaryName);
        var wrapperNs = $"{pascal}.Wrapper";

        var dirs = new[]
        {
            Path.Combine(outputDir, "src", wrapperNs),
            Path.Combine(outputDir, "src", wrapperNs, "Commands"),
            Path.Combine(outputDir, "src", wrapperNs, "Events"),
            Path.Combine(outputDir, "src", wrapperNs, "Parsers"),
            Path.Combine(outputDir, "src", wrapperNs, "Collectors"),
            Path.Combine(outputDir, "test", $"{wrapperNs}.Tests"),
            Path.Combine(outputDir, "doc"),
            Path.Combine(outputDir, "scrape"),
        };

        var files = new Dictionary<string, string>
        {
            [Path.Combine(outputDir, $"{wrapperNs}.slnx")] = GenerateSlnx(wrapperNs),
            [Path.Combine(outputDir, "src", wrapperNs, $"{wrapperNs}.csproj")] = GenerateWrapperCsproj(wrapperNs),
            [Path.Combine(outputDir, "src", wrapperNs, $"{pascal}Client.cs")] = GenerateClientStub(pascal, wrapperNs, binaryName),
            [Path.Combine(outputDir, "test", $"{wrapperNs}.Tests", $"{wrapperNs}.Tests.csproj")] = GenerateTestCsproj(wrapperNs),
            [Path.Combine(outputDir, "test", $"{wrapperNs}.Tests", $"{pascal}ClientTests.cs")] = GenerateTestStub(pascal, wrapperNs),
            [Path.Combine(outputDir, "scrape", "Dockerfile")] = GenerateDockerfile(binaryName),
            [Path.Combine(outputDir, "scrape", "help-output.json")] = GenerateEmptyCommandTree(binaryName),
        };

        return (dirs, files);
    }

    internal static string ToPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var parts = input.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p =>
            char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private static string GenerateSlnx(string wrapperNs) =>
$"""
<Solution>
  <Folder Name="/src/">
    <Project Path="src/{wrapperNs}/{wrapperNs}.csproj" />
  </Folder>
  <Folder Name="/test/">
    <Project Path="test/{wrapperNs}.Tests/{wrapperNs}.Tests.csproj" />
  </Folder>
  <Folder Name="/doc/" />
  <Folder Name="/scrape/" />
</Solution>
""";

    private static string GenerateWrapperCsproj(string wrapperNs) =>
$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0;net11.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>{wrapperNs}</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference BinaryWrapper core library -->
    <!-- <PackageReference Include="FrenchExDev.Net.BinaryWrapper" /> -->
  </ItemGroup>

</Project>
""";

    private static string GenerateClientStub(string pascal, string wrapperNs, string binaryName) =>
$$"""
namespace {{wrapperNs}};

public static class {{pascal}}
{
    public static {{pascal}}Client Detect(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Run 'dotnet binary-wrapper generate' to scaffold this.");
    }
}

public class {{pascal}}Client
{
    // TODO: Generated command methods go here
}
""";

    private static string GenerateTestCsproj(string wrapperNs) =>
$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0;net11.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\{wrapperNs}\{wrapperNs}.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
""";

    private static string GenerateTestStub(string pascal, string wrapperNs) =>
$$"""
namespace {{wrapperNs}}.Tests;

public class {{pascal}}ClientTests
{
    [Fact]
    public void PlaceholderTest()
    {
        // TODO: Add tests after generating commands
        Assert.True(true);
    }
}
""";

    private static string GenerateDockerfile(string binaryName) =>
$"""
# Dockerfile for scraping {binaryName} --help output
# Customize the base image and installation steps as needed

FROM ubuntu:24.04

# Install the binary
# RUN apt-get update && apt-get install -y {binaryName}

# The scraper will execute commands inside this container
CMD ["bash"]
""";

    private static string GenerateEmptyCommandTree(string binaryName)
    {
        var tree = new CommandTree
        {
            BinaryName = binaryName,
            Root = new CommandNode { Name = binaryName }
        };
        return CommandTreeJsonSerializer.Serialize(tree);
    }
}

// ── Command Tree Transformer ────────────────────────────────────────────────

public interface ICommandTreeTransformer
{
    CommandNode TransformRoot(CommandNode root);
    CommandNode TransformCommand(string commandPath, CommandNode node);
}

// ── Help Parsing ────────────────────────────────────────────────────────────

public interface IHelpParser
{
    CommandNode? Parse(string helpText, string commandName);
}

public sealed class StandardHelpParser : IHelpParser
{
    private enum Section { None, Usage, Commands, Options, Arguments }

    public CommandNode? Parse(string helpText, string commandName)
    {
        if (string.IsNullOrWhiteSpace(helpText))
            return null;

        var builder = new CommandNodeBuilder(commandName);
        var lines = helpText.Split('\n');
        var section = Section.None;
        string? description = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (IsHeader(trimmed, "usage"))
            { section = Section.Usage; continue; }
            if (IsHeader(trimmed, "commands") || IsHeader(trimmed, "available commands"))
            { section = Section.Commands; continue; }
            if (IsHeader(trimmed, "options") || IsHeader(trimmed, "flags") || IsHeader(trimmed, "global options"))
            { section = Section.Options; continue; }
            if (IsHeader(trimmed, "arguments") || IsHeader(trimmed, "args"))
            { section = Section.Arguments; continue; }

            if (section == Section.None)
            {
                description ??= trimmed;
                continue;
            }

            if (!line.StartsWith(' ') && !line.StartsWith('\t'))
            {
                section = Section.None;
                continue;
            }

            switch (section)
            {
                case Section.Commands:
                    ParseCommandLine(trimmed, builder);
                    break;
                case Section.Options:
                    ParseOptionLine(trimmed, builder);
                    break;
                case Section.Arguments:
                    ParseArgumentLine(trimmed, builder);
                    break;
            }
        }

        builder.Description = description;
        return builder.Build();
    }

    private static bool IsHeader(string trimmed, string header)
    {
        return trimmed.StartsWith(header, StringComparison.OrdinalIgnoreCase) &&
               (trimmed.Length == header.Length || trimmed[header.Length] == ':');
    }

    public static void ParseCommandLine(string line, CommandNodeBuilder builder)
    {
        var parts = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var name = parts[0];
        var desc = parts.Length > 1 ? parts[1].Trim() : null;
        builder.SubCommands.Add(new CommandNode { Name = name, Description = desc });
    }

    public static void ParseOptionLine(string line, CommandNodeBuilder builder)
    {
        string? shortName = null;
        string? longName = null;
        string? description = null;
        var valueKind = OptionValueKind.Flag;

        var current = line.AsSpan().Trim();

        if (current.StartsWith("-") && !current.StartsWith("--"))
        {
            var commaIdx = current.IndexOf(',');
            if (commaIdx > 0)
            {
                shortName = current[1..commaIdx].Trim().ToString();
                current = current[(commaIdx + 1)..].Trim();
            }
            else
            {
                var spaceIdx = current.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    shortName = current[1..spaceIdx].ToString();
                    current = current[spaceIdx..].Trim();
                }
                else
                {
                    shortName = current[1..].ToString();
                    current = ReadOnlySpan<char>.Empty;
                }
            }
        }

        if (current.StartsWith("--"))
        {
            var end = current.Length;
            for (var i = 2; i < current.Length; i++)
            {
                if (!IsValidOptionNameChar(current[i]))
                {
                    end = i;
                    break;
                }
            }
            longName = current[2..end].ToString();

            if (end < current.Length && current[end] == '=')
            {
                valueKind = OptionValueKind.Single;
                var spaceAfter = current[end..].IndexOf(' ');
                current = spaceAfter > 0
                    ? current[(end + spaceAfter)..].Trim()
                    : ReadOnlySpan<char>.Empty;
            }
            else if (end < current.Length)
            {
                var rest = current[end..].Trim();
                var nextSpace = rest.IndexOf(' ');
                var token = nextSpace > 0 ? rest[..nextSpace] : rest;
                if (IsValuePlaceholder(token))
                {
                    valueKind = OptionValueKind.Single;
                    current = nextSpace > 0 ? rest[nextSpace..].Trim() : ReadOnlySpan<char>.Empty;
                }
                else
                {
                    current = rest;
                }
            }
            else
            {
                current = ReadOnlySpan<char>.Empty;
            }
        }

        if (current.Length > 0)
            description = current.ToString().Trim();

        if (longName is null && shortName is null)
            return;

        builder.Options.Add(new OptionDefinition
        {
            LongName = longName ?? shortName!,
            ShortName = shortName,
            Description = description,
            ValueKind = valueKind,
            ClrType = valueKind == OptionValueKind.Flag ? "bool" : "string"
        });
    }

    public static void ParseArgumentLine(string line, CommandNodeBuilder builder)
    {
        var parts = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var name = parts[0].Trim('<', '>', '[', ']');
        var desc = parts.Length > 1 ? parts[1].Trim() : null;
        var isVariadic = parts[0].Contains("...");
        var isRequired = !parts[0].StartsWith('[');

        builder.Arguments.Add(new ArgumentDefinition
        {
            Name = name.Replace("...", ""),
            Position = builder.Arguments.Count,
            Description = desc,
            IsRequired = isRequired,
            IsVariadic = isVariadic
        });
    }

    private static bool IsValuePlaceholder(ReadOnlySpan<char> token)
    {
        if (token.Length == 0) return false;
        if (token[0] == '<' || token[0] == '[') return true;
        foreach (var c in token)
        {
            if (char.IsLetter(c) && !char.IsUpper(c))
                return false;
        }
        return true;
    }

    internal static bool IsValidOptionNameChar(char c)
        => char.IsLetterOrDigit(c) || c is '-' or '_' or '[' or ']';
}

// ── Packer Help Parser ──────────────────────────────────────────────────────

public sealed class PackerHelpParser : IHelpParser
{
    private enum Section { None, Usage, Commands, Options }

    public CommandNode? Parse(string helpText, string commandName)
    {
        if (string.IsNullOrWhiteSpace(helpText))
            return null;

        var builder = new CommandNodeBuilder(commandName);
        var lines = helpText.Split('\n');
        var section = Section.None;
        string? description = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (IsCommandsHeader(trimmed))
            { section = Section.Commands; continue; }
            if (IsOptionsHeader(trimmed))
            { section = Section.Options; continue; }
            if (trimmed.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase))
            { section = Section.Usage; continue; }

            if (section == Section.None)
            {
                description ??= trimmed;
                continue;
            }

            if (!line.StartsWith(' ') && !line.StartsWith('\t'))
            {
                section = Section.None;
                continue;
            }

            switch (section)
            {
                case Section.Commands:
                    ParseCommandLine(trimmed, builder);
                    break;
                case Section.Options:
                    ParseGoOptionLine(trimmed, builder);
                    break;
            }
        }

        builder.Description = description;
        return builder.Build();
    }

    private static bool IsCommandsHeader(string trimmed)
    {
        return trimmed.StartsWith("Available commands", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Subcommands", StringComparison.OrdinalIgnoreCase)
            || (trimmed.StartsWith("Commands", StringComparison.OrdinalIgnoreCase)
                && (trimmed.Length == 8 || trimmed[8] == ':'));
    }

    private static bool IsOptionsHeader(string trimmed)
    {
        return trimmed.StartsWith("Options:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Flags:", StringComparison.OrdinalIgnoreCase);
    }

    public static void ParseCommandLine(string line, CommandNodeBuilder builder)
    {
        var parts = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var name = parts[0];
        var desc = parts.Length > 1 ? parts[1].Trim() : null;
        builder.SubCommands.Add(new CommandNode { Name = name, Description = desc });
    }

    public static void ParseGoOptionLine(string line, CommandNodeBuilder builder)
    {
        var current = line.AsSpan().Trim();
        if (!current.StartsWith("-"))
            return;

        current = current[1..];

        string longName;
        var valueKind = OptionValueKind.Flag;
        string? description = null;

        var nameEnd = current.Length;
        for (var i = 0; i < current.Length; i++)
        {
            if (current[i] == '=' || current[i] == ' ')
            {
                nameEnd = i;
                break;
            }
        }
        longName = current[..nameEnd].ToString();

        if (nameEnd < current.Length && current[nameEnd] == '=')
        {
            valueKind = OptionValueKind.Single;
            var rest = current[(nameEnd + 1)..];
            var spaceIdx = rest.IndexOf(' ');
            if (spaceIdx >= 0)
                description = rest[(spaceIdx + 1)..].Trim().ToString();
        }
        else if (nameEnd < current.Length)
        {
            var rest = current[nameEnd..].Trim();
            var nextSpace = rest.IndexOf(' ');
            var token = nextSpace > 0 ? rest[..nextSpace] : rest;
            if (IsGoValuePlaceholder(token))
            {
                valueKind = OptionValueKind.Single;
                description = nextSpace > 0 ? rest[(nextSpace + 1)..].Trim().ToString() : null;
            }
            else
            {
                description = rest.ToString().Trim();
            }
        }

        if (string.IsNullOrEmpty(longName))
            return;

        if (string.IsNullOrEmpty(description))
            description = null;

        builder.Options.Add(new OptionDefinition
        {
            LongName = longName,
            Description = description,
            ValueKind = valueKind,
            ClrType = valueKind == OptionValueKind.Flag ? "bool" : "string"
        });
    }

    private static bool IsGoValuePlaceholder(ReadOnlySpan<char> token)
    {
        if (token.Length == 0) return false;
        if (token[0] == '<' || token[0] == '[') return true;
        foreach (var c in token)
        {
            if (char.IsLetter(c) && !char.IsUpper(c))
                return false;
        }
        return true;
    }
}

// ── Cobra Help Parser ───────────────────────────────────────────────────────

/// <summary>
/// Parses help text produced by cobra (Go CLI framework). Cobra output uses
/// "Available Commands:", "Management Commands:", "Flags:", "Global Flags:" sections
/// and embeds Go type hints (<c>string</c>, <c>int</c>, <c>stringArray</c>, etc.)
/// after the flag name.
/// Used by Docker, Podman, Docker Compose, and other cobra-based CLIs.
/// </summary>
public sealed class CobraHelpParser : IHelpParser
{
    private enum Section { None, Usage, Commands, Options }

    private readonly HashSet<string> _skippedCommands;

    public CobraHelpParser(IEnumerable<string>? skippedCommands = null)
    {
        _skippedCommands = skippedCommands is not null
            ? new(skippedCommands, StringComparer.OrdinalIgnoreCase)
            : new(["help", "completion"], StringComparer.OrdinalIgnoreCase);
    }

    // Cobra type hints that indicate the flag takes a value (single).
    private static readonly HashSet<string> CobraSingleTypes = new(StringComparer.Ordinal)
    {
        "string", "int", "int8", "int16", "int32", "int64",
        "uint", "uint8", "uint16", "uint32", "uint64",
        "float", "float32", "float64",
        "duration", "count", "ip", "ipMask", "ipNet"
    };

    // Cobra type hints that indicate the flag takes multiple values.
    private static readonly HashSet<string> CobraMultipleTypes = new(StringComparer.Ordinal)
    {
        "strings", "stringArray", "stringSlice",
        "ipSlice", "intSlice", "uintSlice"
    };

    public CommandNode? Parse(string helpText, string commandName)
    {
        if (string.IsNullOrWhiteSpace(helpText))
            return null;

        var builder = new CommandNodeBuilder(commandName);
        var lines = helpText.Split('\n');
        var section = Section.None;
        string? description = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (IsCommandsHeader(trimmed))
            { section = Section.Commands; continue; }
            if (IsOptionsHeader(trimmed))
            { section = Section.Options; continue; }
            if (trimmed.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase))
            { section = Section.Usage; continue; }
            if (trimmed.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
            { section = Section.None; continue; }
            if (trimmed.StartsWith("Examples:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Aliases:", StringComparison.OrdinalIgnoreCase))
            { section = Section.None; continue; }

            // Non-indented line outside a section → description or section break
            if (section != Section.None && !line.StartsWith(' ') && !line.StartsWith('\t'))
            {
                section = Section.None;
                continue;
            }

            if (section == Section.None)
            {
                description ??= trimmed;
                continue;
            }

            switch (section)
            {
                case Section.Commands:
                    ParseCommandLine(trimmed, builder);
                    break;
                case Section.Options:
                    ParseOptionLine(trimmed, builder);
                    break;
            }
        }

        builder.Description = description;
        return builder.Build();
    }

    private static bool IsCommandsHeader(string trimmed)
    {
        return StartsWithHeader(trimmed, "Available Commands")
            || StartsWithHeader(trimmed, "Additional Commands")
            || StartsWithHeader(trimmed, "Management Commands")
            || StartsWithHeader(trimmed, "Commands");
    }

    private static bool IsOptionsHeader(string trimmed)
    {
        return StartsWithHeader(trimmed, "Flags")
            || StartsWithHeader(trimmed, "Global Flags")
            || StartsWithHeader(trimmed, "Options")
            || StartsWithHeader(trimmed, "Global Options");
    }

    private static bool StartsWithHeader(string trimmed, string header)
    {
        return trimmed.StartsWith(header, StringComparison.OrdinalIgnoreCase) &&
               (trimmed.Length == header.Length || trimmed[header.Length] == ':');
    }

    private void ParseCommandLine(string line, CommandNodeBuilder builder)
    {
        var parts = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var name = parts[0];
        if (_skippedCommands.Contains(name)) return;
        var desc = parts.Length > 1 ? parts[1].Trim() : null;
        builder.SubCommands.Add(new CommandNode { Name = name, Description = desc });
    }

    private static void ParseOptionLine(string line, CommandNodeBuilder builder)
    {
        string? shortName = null;
        string? longName = null;
        string? description = null;
        var valueKind = OptionValueKind.Flag;
        var clrType = "bool";

        var current = line.AsSpan().Trim();

        // Parse optional short flag: -X,
        if (current.StartsWith("-") && !current.StartsWith("--"))
        {
            var commaIdx = current.IndexOf(',');
            if (commaIdx > 0)
            {
                shortName = current[1..commaIdx].Trim().ToString();
                current = current[(commaIdx + 1)..].Trim();
            }
            else
            {
                var spaceIdx = current.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    shortName = current[1..spaceIdx].ToString();
                    current = current[spaceIdx..].Trim();
                }
                else
                {
                    shortName = current[1..].ToString();
                    current = ReadOnlySpan<char>.Empty;
                }
            }
        }

        // Parse --long-name
        if (current.StartsWith("--"))
        {
            var end = current.Length;
            for (var i = 2; i < current.Length; i++)
            {
                if (!StandardHelpParser.IsValidOptionNameChar(current[i]))
                {
                    end = i;
                    break;
                }
            }
            longName = current[2..end].ToString();

            if (end < current.Length)
            {
                current = current[end..].Trim();

                // Check for cobra type hint as the next token
                var nextSpace = current.IndexOf(' ');
                var token = nextSpace > 0 ? current[..nextSpace] : current;
                var tokenStr = token.ToString();

                if (CobraSingleTypes.Contains(tokenStr))
                {
                    valueKind = OptionValueKind.Single;
                    clrType = MapCobraType(tokenStr);
                    current = nextSpace > 0 ? current[nextSpace..].Trim() : ReadOnlySpan<char>.Empty;
                }
                else if (CobraMultipleTypes.Contains(tokenStr))
                {
                    valueKind = OptionValueKind.Multiple;
                    clrType = MapCobraType(tokenStr);
                    current = nextSpace > 0 ? current[nextSpace..].Trim() : ReadOnlySpan<char>.Empty;
                }
                else if (IsValuePlaceholder(token))
                {
                    valueKind = OptionValueKind.Single;
                    clrType = "string";
                    current = nextSpace > 0 ? current[nextSpace..].Trim() : ReadOnlySpan<char>.Empty;
                }
            }
            else
            {
                current = ReadOnlySpan<char>.Empty;
            }
        }

        if (current.Length > 0)
            description = current.ToString().Trim();

        if (longName is null && shortName is null)
            return;

        builder.Options.Add(new OptionDefinition
        {
            LongName = longName ?? shortName!,
            ShortName = shortName,
            Description = description,
            ValueKind = valueKind,
            ClrType = clrType
        });
    }

    private static string MapCobraType(string cobraType) => cobraType switch
    {
        "int" or "int8" or "int16" or "int32" or "int64" => "integer",
        "uint" or "uint8" or "uint16" or "uint32" or "uint64" => "integer",
        "float" or "float32" or "float64" => "string",
        "duration" => "string",
        "count" => "integer",
        _ => "string"
    };

    private static bool IsValuePlaceholder(ReadOnlySpan<char> token)
    {
        if (token.Length == 0) return false;
        if (token[0] == '<' || token[0] == '[') return true;
        foreach (var c in token)
        {
            if (char.IsLetter(c) && !char.IsUpper(c))
                return false;
        }
        return true;
    }
}

// ── Argparse Help Parser ────────────────────────────────────────────────────

/// <summary>
/// Parser for Python argparse-generated help output.
/// Recognises the <c>{cmd1,cmd2,...}</c> subcommand notation, the
/// <c>options:</c> / <c>optional arguments:</c> section headers, and
/// metavar-based value detection.
/// </summary>
public sealed class ArgparseHelpParser : IHelpParser
{
    private enum Section { None, Usage, Options, Commands, PositionalArguments }

    private readonly HashSet<string> _skippedCommands;

    public ArgparseHelpParser(IEnumerable<string>? skippedCommands = null)
    {
        _skippedCommands = skippedCommands is not null
            ? new(skippedCommands, StringComparer.OrdinalIgnoreCase)
            : new(["help"], StringComparer.OrdinalIgnoreCase);
    }

    public CommandNode? Parse(string helpText, string commandName)
    {
        if (string.IsNullOrWhiteSpace(helpText))
            return null;

        var builder = new CommandNodeBuilder(commandName);
        var lines = helpText.Split('\n');
        var section = Section.None;
        string? description = null;
        OptionDefinition? pendingOption = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushPendingOption(builder, ref pendingOption);
                continue;
            }

            // Detect section headers (unindented or minimal-indent labels ending with ':')
            if (TryMatchHeader(trimmed, out var newSection))
            {
                FlushPendingOption(builder, ref pendingOption);
                section = newSection;
                continue;
            }

            // Lines that don't start with whitespace reset the section (unless usage continuation)
            if (!line.StartsWith(' ') && !line.StartsWith('\t') && section != Section.Usage)
            {
                FlushPendingOption(builder, ref pendingOption);
                if (section == Section.None)
                    description ??= trimmed;
                section = Section.None;
                continue;
            }

            switch (section)
            {
                case Section.Commands:
                    FlushPendingOption(builder, ref pendingOption);
                    ParseCommandLine(trimmed, builder);
                    break;
                case Section.Options:
                    ParseOptionLine(trimmed, builder, ref pendingOption);
                    break;
                case Section.PositionalArguments:
                    FlushPendingOption(builder, ref pendingOption);
                    ParsePositionalLine(trimmed, builder);
                    break;
            }
        }

        FlushPendingOption(builder, ref pendingOption);
        builder.Description = description;
        return builder.Build();
    }

    private static bool TryMatchHeader(string trimmed, out Section section)
    {
        section = Section.None;

        if (MatchesHeader(trimmed, "options") ||
            MatchesHeader(trimmed, "optional arguments"))
        {
            section = Section.Options;
            return true;
        }

        if (MatchesHeader(trimmed, "command") ||
            MatchesHeader(trimmed, "commands") ||
            MatchesHeader(trimmed, "subcommands"))
        {
            section = Section.Commands;
            return true;
        }

        if (MatchesHeader(trimmed, "positional arguments"))
        {
            section = Section.PositionalArguments;
            return true;
        }

        if (MatchesHeader(trimmed, "usage"))
        {
            section = Section.Usage;
            return true;
        }

        return false;
    }

    private static bool MatchesHeader(string trimmed, string header)
    {
        return trimmed.StartsWith(header, StringComparison.OrdinalIgnoreCase) &&
               trimmed.Length > header.Length && trimmed[header.Length] == ':';
    }

    private void ParseCommandLine(string trimmed, CommandNodeBuilder builder)
    {
        // Skip {cmd1,cmd2,...} brace-list header lines
        if (trimmed.StartsWith('{') || trimmed.StartsWith("[{"))
            return;

        var parts = trimmed.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var name = parts[0];
        if (_skippedCommands.Contains(name))
            return;

        var desc = parts.Length > 1 ? parts[1].Trim() : null;
        builder.SubCommands.Add(new CommandNode { Name = name, Description = desc });
    }

    private static void ParseOptionLine(string trimmed, CommandNodeBuilder builder, ref OptionDefinition? pendingOption)
    {
        // Continuation line: doesn't start with '-'
        if (!trimmed.StartsWith('-'))
        {
            // Append to pending option's description
            if (pendingOption is not null && !string.IsNullOrWhiteSpace(trimmed))
            {
                pendingOption = pendingOption with
                {
                    Description = pendingOption.Description is null
                        ? trimmed
                        : pendingOption.Description + " " + trimmed
                };
            }
            return;
        }

        // Flush previous option before starting a new one
        FlushPendingOption(builder, ref pendingOption);

        // Split into definition part and description part using 2+ consecutive spaces
        string defPart;
        string? descPart;
        SplitDefinitionAndDescription(trimmed, out defPart, out descPart);

        string? shortName = null;
        string? longName = null;
        var valueKind = OptionValueKind.Flag;

        var tokens = defPart.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).ToList();
        var idx = 0;

        // Parse short flag: -X or -X METAVAR
        if (idx < tokens.Count && tokens[idx].StartsWith('-') && !tokens[idx].StartsWith("--"))
        {
            var shortToken = tokens[idx];
            // Remove trailing comma if present (e.g., "-f,")
            shortName = shortToken.TrimStart('-').TrimEnd(',');
            idx++;

            // Check for comma as separate token
            if (idx < tokens.Count && tokens[idx] == ",")
                idx++;
            // If next token is NOT a flag, it's a short metavar — skip it (long flag will have its own)
            else if (idx < tokens.Count && !tokens[idx].StartsWith('-') && tokens[idx] != ",")
            {
                // Short metavar — skip, but note this means the option takes a value
                idx++;
            }

            // Skip comma between short and long
            if (idx < tokens.Count && tokens[idx] == ",")
                idx++;
        }

        // Parse long flag: --long-name
        if (idx < tokens.Count && tokens[idx].StartsWith("--"))
        {
            longName = tokens[idx].TrimStart('-');
            idx++;

            // Check for metavar: next non-flag token
            if (idx < tokens.Count && !tokens[idx].StartsWith('-'))
            {
                // It's a metavar — this option takes a value
                valueKind = OptionValueKind.Single;
                // idx++; // consume metavar — already at end of defPart tokens
            }
        }

        // If only short flag found with no long flag, check if short metavar indicates a value
        if (longName is null && shortName is not null)
        {
            // Re-check: if defPart had tokens after the short flag that aren't flags
            if (idx < tokens.Count && !tokens[idx].StartsWith('-'))
                valueKind = OptionValueKind.Single;
        }

        if (longName is null && shortName is null)
            return;

        pendingOption = new OptionDefinition
        {
            LongName = longName ?? shortName!,
            ShortName = shortName,
            Description = descPart,
            ValueKind = valueKind,
            ClrType = valueKind == OptionValueKind.Flag ? "bool" : "string"
        };
    }

    private static void SplitDefinitionAndDescription(string line, out string defPart, out string? descPart)
    {
        // Find the first occurrence of 2+ consecutive spaces after the initial flag
        // This separates the flag+metavar definition from the description
        var i = 0;
        // Skip leading whitespace
        while (i < line.Length && line[i] == ' ') i++;
        // Skip the first token (flag)
        while (i < line.Length && line[i] != ' ') i++;

        // Now look for 2+ consecutive spaces
        while (i < line.Length)
        {
            if (i + 1 < line.Length && line[i] == ' ' && line[i + 1] == ' ')
            {
                defPart = line[..i].Trim();
                descPart = line[(i + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(descPart))
                    descPart = null;
                return;
            }
            i++;
        }

        defPart = line.Trim();
        descPart = null;
    }

    private static void ParsePositionalLine(string trimmed, CommandNodeBuilder builder)
    {
        // Skip {cmd1,cmd2,...} brace-list lines (subcommands, not real positional args)
        if (trimmed.StartsWith('{') || trimmed.StartsWith("[{"))
            return;

        // Split definition from description using 2+ spaces
        SplitDefinitionAndDescription(trimmed, out var defPart, out var desc);

        var isVariadic = defPart.Contains("...");
        var isOptional = defPart.StartsWith('[');
        var name = defPart.Trim('[', ']', '<', '>').Replace("...", "").Trim();

        if (string.IsNullOrWhiteSpace(name)) return;

        builder.Arguments.Add(new ArgumentDefinition
        {
            Name = name,
            Position = builder.Arguments.Count,
            Description = desc,
            IsRequired = !isOptional,
            IsVariadic = isVariadic
        });
    }

    private static void FlushPendingOption(CommandNodeBuilder builder, ref OptionDefinition? pendingOption)
    {
        if (pendingOption is not null)
        {
            builder.Options.Add(pendingOption);
            pendingOption = null;
        }
    }
}

// ── GitHub CLI-style Help Parser ──────────────────────────────────────────

/// <summary>
/// Parser for CLIs using the GitHub CLI (<c>gh</c>) help template. This custom Cobra
/// template was created by the GitHub CLI team and subsequently forked by glab and
/// potentially other CLIs. It differs from standard Cobra output in several ways:
/// <list type="bullet">
///   <item>ALL-CAPS section headers without colons: <c>COMMANDS</c>, <c>CORE COMMANDS</c>,
///     <c>FLAGS</c>, <c>INHERITED FLAGS</c>, <c>USAGE</c>, <c>EXAMPLES</c></item>
///   <item>Flags use space or comma as short/long separator: <c>-v --version</c> or
///     <c>-v, --version</c> (varies by tool/version)</item>
///   <item>No inline type hints — value kind is inferred from description heuristics:
///     <c>(default)</c> suffix, <c>&lt;placeholder&gt;</c> patterns, "comma-separated" keywords</item>
///   <item>Command entries may use colon suffix: <c>alias:</c> (older glab) or argument hints:
///     <c>alias [command] [--flags]</c> (newer glab, gh)</item>
/// </list>
/// </summary>
public sealed class GhStyleHelpParser : IHelpParser
{
    private enum Section { None, Usage, Commands, Flags, Examples, Aliases }

    private readonly HashSet<string> _skippedCommands;
    private readonly string? _binaryName;

    /// <param name="skippedCommands">Commands to skip during parsing. Defaults to <c>help</c>,
    /// <c>completion</c>, and <c>check-update</c>.</param>
    /// <param name="binaryName">Optional binary name to filter out example/continuation lines
    /// that start with the binary name (e.g., <c>"glab"</c> to skip lines like
    /// <c>glab repo clone -g &lt;group&gt;</c>). If null, no binary-name filtering is applied.</param>
    public GhStyleHelpParser(IEnumerable<string>? skippedCommands = null, string? binaryName = null)
    {
        _skippedCommands = skippedCommands is not null
            ? new(skippedCommands, StringComparer.OrdinalIgnoreCase)
            : new(["help", "completion", "check-update"], StringComparer.OrdinalIgnoreCase);
        _binaryName = binaryName;
    }

    // ── Description heuristic helpers (no Regex dependency) ─────────────────

    public CommandNode? Parse(string helpText, string commandName)
    {
        if (string.IsNullOrWhiteSpace(helpText))
            return null;

        var builder = new CommandNodeBuilder(commandName);
        var lines = helpText.Split('\n');
        var section = Section.None;
        string? description = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Detect ALL-CAPS section headers
            if (IsSectionHeader(trimmed, out var newSection))
            {
                section = newSection;
                continue;
            }

            // Content lines must be indented (gh-style uses leading spaces for all content)
            if (section != Section.None && !line.StartsWith(' ') && !line.StartsWith('\t'))
            {
                section = Section.None;
                // Fall through — might be a description line
            }

            if (section == Section.None)
            {
                // First non-empty, non-header line is the description
                description ??= trimmed;
                continue;
            }

            switch (section)
            {
                case Section.Commands:
                    ParseCommandLine(trimmed, builder);
                    break;
                case Section.Flags:
                    ParseFlagLine(trimmed, builder);
                    break;
                // Usage, Examples, Aliases — skip content
            }
        }

        builder.Description = description;
        return builder.Build();
    }

    private static bool IsSectionHeader(string trimmed, out Section section)
    {
        section = Section.None;

        // gh-style uses ALL-CAPS headers. The header name varies by tool:
        //   "CORE COMMANDS", "COMMANDS", "OTHER COMMANDS", "ADDITIONAL COMMANDS"
        if (trimmed.EndsWith("COMMANDS") && trimmed == trimmed.ToUpperInvariant())
        { section = Section.Commands; return true; }

        if (trimmed is "FLAGS" or "INHERITED FLAGS")
        { section = Section.Flags; return true; }

        if (trimmed is "USAGE")
        { section = Section.Usage; return true; }

        if (trimmed is "EXAMPLES")
        { section = Section.Examples; return true; }

        if (trimmed is "ALIASES")
        { section = Section.Aliases; return true; }

        // Skip known non-command/non-flag sections
        if (trimmed is "ENVIRONMENT VARIABLES" or "LEARN MORE" or "FEEDBACK"
            or "ARGUMENTS" or "JSON FIELDS" or "HELP TOPICS")
        { section = Section.None; return true; }

        return false;
    }

    private void ParseCommandLine(string line, CommandNodeBuilder builder)
    {
        // Two formats:
        //   Colon style:  "alias:       Create, list, and delete aliases."
        //   Args style:   "alias [command] [--flags]   Create, list, and delete aliases."
        // Split on first run of 2+ spaces to separate command from description
        var (commandPart, desc2) = SplitOnDoubleSpace(line);
        if (commandPart is null || desc2 is null) return;

        var commandName = commandPart.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

        // Strip trailing colon (older format: "alias:")
        commandName = commandName.TrimEnd(':');

        if (string.IsNullOrEmpty(commandName)) return;
        if (_skippedCommands.Contains(commandName)) return;

        // Skip lines starting with the binary name — these are example/continuation lines
        if (_binaryName is not null && string.Equals(commandName, _binaryName, StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrEmpty(desc2)) return;

        builder.SubCommands.Add(new CommandNode { Name = commandName, Description = desc2 });
    }

    private static void ParseFlagLine(string line, CommandNodeBuilder builder)
    {
        // Formats:
        //   "-v, --version   show version information"   (comma-separated)
        //   "-v --version    show version information"    (space-separated)
        //   "    --help      Show help for this command." (long-only)

        string? shortName = null;
        string? longName = null;

        var current = line.AsSpan().Trim();

        // Parse optional short flag: -X or -X, (with optional comma)
        if (current.StartsWith("-") && !current.StartsWith("--"))
        {
            var spaceIdx = current.IndexOf(' ');
            if (spaceIdx > 0)
            {
                shortName = current[1..spaceIdx].Trim().ToString();
                current = current[spaceIdx..].Trim();
            }
            else
            {
                shortName = current[1..].ToString();
                current = ReadOnlySpan<char>.Empty;
            }

            // Strip trailing comma: "v," → "v"
            shortName = shortName.TrimEnd(',');

            // Skip comma-only separator that might remain
            if (current.StartsWith(","))
                current = current[1..].Trim();
        }

        // Parse --long-name
        if (current.StartsWith("--"))
        {
            var end = current.Length;
            for (var i = 2; i < current.Length; i++)
            {
                if (!IsValidOptionNameChar(current[i]))
                {
                    end = i;
                    break;
                }
            }
            longName = current[2..end].ToString();
            current = end < current.Length ? current[end..].Trim() : ReadOnlySpan<char>.Empty;
        }

        if (longName is null && shortName is null)
            return;

        // Everything remaining is the description
        var description = current.Length > 0 ? current.ToString().Trim() : null;

        // Determine value kind from description heuristics
        var valueKind = OptionValueKind.Flag;
        var clrType = "bool";
        string? defaultValue = null;

        if (description is not null)
        {
            // Check for default value in parentheses at end: "(created_at)", "(30)"
            if (TryExtractTrailingDefault(description, out var defVal, out var trimmedDesc))
            {
                defaultValue = defVal;
                description = trimmedDesc;
                valueKind = OptionValueKind.Single;
                clrType = int.TryParse(defaultValue, out _) ? "integer" : "string";
            }

            // Check for multiple-value indicators
            if (ContainsMultipleValueHint(description))
            {
                valueKind = OptionValueKind.Multiple;
                clrType = "string";
            }
            // Check for placeholder indicating a value parameter
            else if (valueKind == OptionValueKind.Flag && ContainsPlaceholder(description))
            {
                valueKind = OptionValueKind.Single;
                clrType = "string";
            }
        }

        builder.Options.Add(new OptionDefinition
        {
            LongName = longName ?? shortName!,
            ShortName = shortName,
            Description = description,
            ValueKind = valueKind,
            ClrType = clrType,
            DefaultValue = defaultValue
        });
    }

    private static bool IsValidOptionNameChar(char c) =>
        char.IsLetterOrDigit(c) || c == '-' || c == '_';

    /// <summary>Splits a line on the first run of 2+ spaces. Returns (left, right) or (null, null).</summary>
    private static (string? Left, string? Right) SplitOnDoubleSpace(string line)
    {
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == ' ' && line[i + 1] == ' ')
            {
                var left = line[..i].Trim();
                var right = line[(i + 1)..].Trim();
                if (left.Length > 0 && right.Length > 0)
                    return (left, right);
            }
        }
        return (null, null);
    }

    /// <summary>Extracts a trailing "(value)" default from a description string.</summary>
    private static bool TryExtractTrailingDefault(string desc, out string value, out string trimmed)
    {
        var s = desc.AsSpan().TrimEnd();
        if (s.Length > 2 && s[^1] == ')')
        {
            var openIdx = s.LastIndexOf('(');
            if (openIdx > 0)
            {
                value = s[(openIdx + 1)..^1].ToString();
                trimmed = s[..openIdx].TrimEnd().ToString();
                return true;
            }
        }
        value = "";
        trimmed = desc;
        return false;
    }

    /// <summary>Checks if description contains &lt;placeholder&gt; patterns.</summary>
    private static bool ContainsPlaceholder(string desc)
    {
        var i = desc.IndexOf('<');
        while (i >= 0 && i < desc.Length - 2)
        {
            var j = desc.IndexOf('>', i + 1);
            if (j > i + 1)
            {
                var c = desc[i + 1];
                if (char.IsLetter(c))
                    return true;
            }
            i = desc.IndexOf('<', i + 1);
        }
        return false;
    }

    /// <summary>Checks if description contains multiple-value keywords.</summary>
    private static bool ContainsMultipleValueHint(string desc)
    {
        return desc.Contains("comma-separated", StringComparison.OrdinalIgnoreCase)
            || desc.Contains("comma separated", StringComparison.OrdinalIgnoreCase)
            || desc.Contains("repeating the flag", StringComparison.OrdinalIgnoreCase)
            || desc.Contains("multiple ", StringComparison.OrdinalIgnoreCase);
    }
}

// ── Help Parser Registry ───────────────────────────────────────────────────

/// <summary>
/// Declarative registry for help parser strategies. Resolves parsers by name
/// (e.g., <c>"cobra"</c>, <c>"standard"</c>, <c>"packer"</c>).
/// Custom parsers can be registered via <see cref="Register"/>.
/// </summary>
public static class HelpParsers
{
    private static readonly Dictionary<string, Func<IHelpParser>> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["standard"] = () => new StandardHelpParser(),
        ["packer"] = () => new PackerHelpParser(),
        ["cobra"] = () => new CobraHelpParser(),
        ["argparse"] = () => new ArgparseHelpParser(),
        ["gh"] = () => new GhStyleHelpParser(),
    };

    public static IHelpParser Create(string strategy)
    {
        if (Registry.TryGetValue(strategy, out var factory))
            return factory();
        throw new ArgumentException(
            $"Unknown help parser strategy: '{strategy}'. Known: {string.Join(", ", Registry.Keys)}");
    }

    public static void Register(string name, Func<IHelpParser> factory)
        => Registry[name] = factory;

    public static IReadOnlyCollection<string> KnownStrategies => Registry.Keys;
}

// ── Help Scraper ────────────────────────────────────────────────────────────

public sealed class HelpScraper
{
    private readonly IHelpParser _parser;
    private readonly Func<string[], Task<string>> _runHelp;
    private readonly int _maxDepth;
    private readonly string _helpFlag;
    private readonly string? _helpDumpDir;
    private readonly int _maxConcurrency;
    private readonly Action? _onCommandScraped;

    public HelpScraper(IHelpParser parser, Func<string[], Task<string>> runHelp,
        int maxDepth = 10, string helpFlag = "--help", string? helpDumpDir = null,
        int maxConcurrency = 4, Action? onCommandScraped = null)
    {
        _parser = parser;
        _runHelp = runHelp;
        _maxDepth = maxDepth;
        _helpFlag = helpFlag;
        _helpDumpDir = helpDumpDir;
        _maxConcurrency = Math.Max(1, maxConcurrency);
        _onCommandScraped = onCommandScraped;
    }

    public async Task<CommandTree> ScrapeAsync(string binaryName,
        CancellationToken ct = default)
    {
        try
        {
            // One limit for the whole traversal, independent of other ScrapeAsync calls.
            using var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
            var rootNode = await ScrapeNodeAsync([binaryName, _helpFlag], binaryName, 0, semaphore, ct);
            return new CommandTree
            {
                BinaryName = binaryName,
                Root = rootNode ?? new CommandNode { Name = binaryName }
            };
        }
        catch (Exception ex) when (ex is not ContainerRuntimeException)
        {
            return new CommandTree
            {
                BinaryName = binaryName,
                Root = new CommandNode { Name = binaryName }
            };
        }
    }

    private async Task<CommandNode?> ScrapeNodeAsync(string[] helpArgs, string commandName,
        int depth, SemaphoreSlim semaphore, CancellationToken ct)
    {
        if (depth > _maxDepth || ct.IsCancellationRequested)
            return null;

        string helpText;
        try
        {
            await semaphore.WaitAsync(ct);
            try
            {
                helpText = await _runHelp(helpArgs);
            }
            finally
            {
                // Release before parsing or awaiting descendants to avoid recursive deadlocks.
                semaphore.Release();
            }
        }
        catch (Exception ex) when (ex is not ContainerRuntimeException)
        {
            return null;
        }

        // Dump raw help text for debugging and test data
        if (_helpDumpDir is not null)
        {
            var commandPath = string.Join("_", helpArgs[..^1]); // all args except help flag
            var dumpPath = Path.Combine(_helpDumpDir, $"{commandPath}.help.txt");
            Directory.CreateDirectory(_helpDumpDir);
            await File.WriteAllTextAsync(dumpPath, helpText);
        }

        var node = _parser.Parse(helpText, commandName);
        if (node is null) return null;

        _onCommandScraped?.Invoke();

        if (node.SubCommands.Count == 0)
            return node;

        var scrapedSubs = new CommandNode?[node.SubCommands.Count];
        var tasks = node.SubCommands.Select(async (sub, i) =>
        {
            try
            {
                var subArgs = helpArgs[..^1].Append(sub.Name).Append(_helpFlag).ToArray();
                scrapedSubs[i] = await ScrapeNodeAsync(subArgs, sub.Name, depth + 1, semaphore, ct);
            }
            catch (Exception ex) when (ex is not ContainerRuntimeException)
            {
                scrapedSubs[i] = null;
            }
        }).ToArray();
        await Task.WhenAll(tasks);

        return ReconstructWithScrapedSubCommands(node, scrapedSubs);
    }

    public static CommandNode ReconstructWithScrapedSubCommands(
        CommandNode node, IReadOnlyList<CommandNode?> scrapedSubs)
    {
        var subCommands = new List<CommandNode>(node.SubCommands.Count);
        for (var i = 0; i < node.SubCommands.Count; i++)
            subCommands.Add(scrapedSubs[i] ?? node.SubCommands[i]);

        return new CommandNode
        {
            Name = node.Name,
            Description = node.Description,
            Options = node.Options,
            Arguments = node.Arguments,
            SubCommands = subCommands
        };
    }
}

// ── Container Runtime ───────────────────────────────────────────────────────

public interface IContainerRuntime
{
    Task<string> BuildAsync(string tag, string dockerfileContent, CancellationToken cancellationToken = default);
    Task<string> RunAsync(string tag, string[] command, CancellationToken cancellationToken = default);
    Task RemoveImageAsync(string tag, CancellationToken cancellationToken = default);
}

public class ProcessRunnerContainerRuntime : IContainerRuntime
{
    private readonly Func<string[], Task<string>> _runProcess;
    private readonly string _runtimeBinary;

    public ProcessRunnerContainerRuntime(Func<string[], Task<string>>? runProcess = null,
        string runtimeBinary = "podman")
    {
        _runProcess = runProcess ?? RunProcessAsync;
        _runtimeBinary = runtimeBinary;
    }

    public string RuntimeBinary => _runtimeBinary;

    public async Task<string> BuildAsync(string tag, string dockerfileContent,
        CancellationToken cancellationToken = default)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"bw-dockerfile-{Guid.NewGuid()}");
        try
        {
            await File.WriteAllTextAsync(tempFile, dockerfileContent, cancellationToken);
            return await _runProcess([_runtimeBinary, "build", "-t", tag, "-f", tempFile, "."]);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    public Task<string> RunAsync(string tag, string[] command,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { _runtimeBinary, "run", "--rm", tag };
        args.AddRange(command);
        return _runProcess(args.ToArray());
    }

    public Task RemoveImageAsync(string tag, CancellationToken cancellationToken = default)
        => _runProcess([_runtimeBinary, "rmi", "-f", tag]);

    public static Task<string> RunProcessAsync(string[] args)
        => RunProcessAsync(args, null, null);

    /// <summary>Streams complete output lines while retaining the captured process output.</summary>
    public static Task<string> RunProcessAsync(string[] args,
        Action<string>? onStandardOutput, Action<string>? onStandardError)
        => ContainerProcessThrottle.RunAsync(args[0], () => RunProcessCoreAsync(args, onStandardOutput, onStandardError));

    private static async Task<string> RunProcessCoreAsync(string[] args,
        Action<string>? onStandardOutput, Action<string>? onStandardError)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = args[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args[1..])
            psi.ArgumentList.Add(arg);

        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc is null) return "";
        // Drain both streams concurrently, even if an output observer fails.
        Exception? observerError = null;
        async Task<string> ReadAsync(StreamReader reader, Action<string>? observer)
        {
            if (observer is null) return await reader.ReadToEndAsync();
            var output = new System.Text.StringBuilder();
            var pendingLine = new System.Text.StringBuilder();
            var buffer = new char[4096];
            void NotifyLine()
            {
                var line = pendingLine.ToString().TrimEnd('\r');
                pendingLine.Clear();
                try { observer(line); }
                catch (Exception ex) { Interlocked.CompareExchange(ref observerError, ex, null); }
            }
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory())) != 0)
            {
                output.Append(buffer, 0, read);
                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] == '\n') NotifyLine();
                    else pendingLine.Append(buffer[i]);
                }
            }
            if (pendingLine.Length > 0) NotifyLine();
            return output.ToString();
        }
        var stdoutTask = ReadAsync(proc.StandardOutput, onStandardOutput);
        var stderrTask = ReadAsync(proc.StandardError, onStandardError);
        await proc.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (observerError is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(observerError).Throw();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"Process '{args[0]}' exited with code {proc.ExitCode}: {stderr}");
        return stdout;
    }
}

public sealed class PodmanContainerRuntime : ProcessRunnerContainerRuntime
{
    public PodmanContainerRuntime(Func<string[], Task<string>>? runProcess = null)
        : base(runProcess, "podman") { }
}

public sealed class DockerContainerRuntime : ProcessRunnerContainerRuntime
{
    public DockerContainerRuntime(Func<string[], Task<string>>? runProcess = null)
        : base(runProcess, "docker") { }
}

// ── Scrape Error Types ──────────────────────────────────────────────────────

public abstract class ScrapeError(string message)
{
    public string Message => message;
}

public sealed class ContainerBuildError(string message, string buildOutput) : ScrapeError(message)
{
    public string BuildOutput => buildOutput;
}

public sealed class ContainerRunError(string message) : ScrapeError(message);

public sealed class ParseError(string message) : ScrapeError(message);

// ── Multi-Version Scraper ───────────────────────────────────────────────────

public sealed record VersionScrapeResult(
    string Version, bool Success, CommandTree? Tree, string? ErrorMessage);

public delegate void ScrapeProgressHandler(VersionScrapeResult result, int completed, int total);

public sealed class MultiVersionScraper
{
    private readonly IVersionCollector _versionCollector;
    private readonly Func<string, ScrapePipeline> _pipelineFactory;
    private readonly int _maxParallelism;

    public MultiVersionScraper(
        IVersionCollector versionCollector,
        Func<string, ScrapePipeline> pipelineFactory,
        int maxParallelism = 4)
    {
        _versionCollector = versionCollector;
        _pipelineFactory = pipelineFactory;
        _maxParallelism = maxParallelism;
    }

    public event ScrapeProgressHandler? Progress;

    public Task<IReadOnlyList<VersionScrapeResult>> ScrapeAllAsync(CancellationToken ct = default)
        => ScrapeAsync(_ => true, ct);

    public async Task<IReadOnlyList<VersionScrapeResult>> ScrapeAsync(
        Func<string, bool> versionFilter, CancellationToken ct = default)
    {
        var allVersions = await _versionCollector.CollectVersionsAsync(ct);
        var versions = allVersions.Where(versionFilter).ToList();

        if (versions.Count == 0)
            return [];

        var channel = System.Threading.Channels.Channel.CreateBounded<string>(versions.Count);
        var results = new System.Collections.Concurrent.ConcurrentBag<VersionScrapeResult>();
        var completed = 0;
        var total = versions.Count;

        foreach (var v in versions)
            await channel.Writer.WriteAsync(v, ct);
        channel.Writer.Complete();

        var workers = new Task[Math.Min(_maxParallelism, versions.Count)];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = Task.Run(async () =>
            {
                await foreach (var version in channel.Reader.ReadAllAsync(ct))
                {
                    VersionScrapeResult result;
                    try
                    {
                        var pipeline = _pipelineFactory(version);
                        var tree = await pipeline.ExecuteAsync(ct);
                        result = new VersionScrapeResult(version, true, tree, null);
                    }
                    catch (Exception ex)
                    {
                        result = new VersionScrapeResult(version, false, null, ex.Message);
                    }

                    results.Add(result);
                    var done = Interlocked.Increment(ref completed);
                    Progress?.Invoke(result, done, total);
                }
            }, ct);
        }

        await Task.WhenAll(workers);

        var sorted = results.OrderBy(r => r.Version, Comparer<string>.Create(
            GitHubReleasesVersionCollector.CompareVersionStrings)).ToList();
        return sorted;
    }
}

// ── Scrape Pipeline ─────────────────────────────────────────────────────────

public sealed class ScrapePipeline
{
    private string? _binaryName;
    private string _helpFlag = "--help";
    private IHelpParser? _parser;
    private int _maxDepth = 10;
    private string? _outputPath;

    private IContainerRuntime? _runtime;
    private string? _image;
    private readonly List<string> _installCommands = [];
    private readonly List<(string Name, string Value)> _buildArgs = [];

    private readonly List<Func<CommandNode, CommandNode>> _rootTransforms = [];
    private readonly Dictionary<string, Func<CommandNode, CommandNode>> _commandTransforms = [];
    private readonly List<ICommandTreeTransformer> _transformers = [];

    private Func<string[], Task<string>>? _runHelp;
    private string? _helpDumpDir;
    private int _scrapeParallelism = 4;
    private Action? _onCommandScraped;

    public ScrapePipeline Binary(string name) { _binaryName = name; return this; }
    public ScrapePipeline HelpFlag(string flag) { _helpFlag = flag; return this; }
    public ScrapePipeline UseParser<T>() where T : IHelpParser, new() { _parser = new T(); return this; }
    public ScrapePipeline UseParser(IHelpParser parser) { _parser = parser; return this; }
    public ScrapePipeline UseParser(string strategy) { _parser = HelpParsers.Create(strategy); return this; }
    public ScrapePipeline MaxDepth(int depth) { _maxDepth = depth; return this; }
    public ScrapePipeline OutputTo(string path) { _outputPath = path; return this; }

    public ScrapePipeline WithRuntime(string runtimeBinary)
    { _runtime = new ProcessRunnerContainerRuntime(runtimeBinary: runtimeBinary); return this; }
    public ScrapePipeline WithRuntime(IContainerRuntime runtime)
    { _runtime = runtime; return this; }
    public ScrapePipeline FromImage(string image) { _image = image; return this; }
    public ScrapePipeline Install(string command) { _installCommands.Add(command); return this; }
    public ScrapePipeline BuildArg(string name, string value) { _buildArgs.Add((name, value)); return this; }

    public ScrapePipeline WithRunHelp(Func<string[], Task<string>> func)
    { _runHelp = func; return this; }

    public ScrapePipeline TransformRoot(Func<CommandNode, CommandNode> transform)
    { _rootTransforms.Add(transform); return this; }

    public ScrapePipeline TransformCommand(string path, Func<CommandNode, CommandNode> transform)
    { _commandTransforms[path] = transform; return this; }

    public ScrapePipeline UseTransformer(ICommandTreeTransformer transformer)
    { _transformers.Add(transformer); return this; }

    public ScrapePipeline DumpHelpTo(string dir)
    { _helpDumpDir = dir; return this; }

    public ScrapePipeline ScrapeParallelism(int n)
    { _scrapeParallelism = n; return this; }

    public ScrapePipeline OnCommandScraped(Action callback)
    { _onCommandScraped = callback; return this; }

    public string GenerateDockerfile()
    {
        if (_image is null)
            throw new InvalidOperationException("Base image not specified. Call .FromImage() first.");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"FROM {_image}");

        foreach (var (name, value) in _buildArgs)
            sb.AppendLine($"ARG {name}={value}");

        foreach (var cmd in _installCommands)
            sb.AppendLine($"RUN {cmd}");

        return sb.ToString();
    }

    public async Task<CommandTree> ExecuteAsync(CancellationToken ct = default)
    {
        if (_binaryName is null)
            throw new InvalidOperationException("Binary name not specified");

        var parser = _parser ?? new StandardHelpParser();
        Func<string[], Task<string>> runHelp;

        if (_runtime is not null && _image is not null)
        {
            var tag = $"bw-scrape-{_binaryName}:{Guid.NewGuid():N}";
            var dockerfile = GenerateDockerfile();

            await _runtime.BuildAsync(tag, dockerfile, ct);

            var runtime = _runtime;
            runHelp = async args => await runtime.RunAsync(tag, args, ct);

            try
            {
                return await ScrapeAndTransform(parser, runHelp, ct);
            }
            finally
            {
                try { await _runtime.RemoveImageAsync(tag, ct); } catch { }
            }
        }
        else if (_runHelp is not null)
        {
            runHelp = _runHelp;
        }
        else
        {
            runHelp = ProcessRunnerContainerRuntime.RunProcessAsync;
        }

        return await ScrapeAndTransform(parser, runHelp, ct);
    }

    private async Task<CommandTree> ScrapeAndTransform(
        IHelpParser parser, Func<string[], Task<string>> runHelp,
        CancellationToken ct)
    {
        var scraper = new HelpScraper(parser, runHelp, _maxDepth, _helpFlag, _helpDumpDir, _scrapeParallelism, _onCommandScraped);
        var tree = await scraper.ScrapeAsync(_binaryName!, ct);

        tree = ApplyTransforms(tree);

        if (_outputPath is not null)
            await CommandTreeJsonSerializer.SerializeToFileAsync(tree, _outputPath, ct);

        return tree;
    }

    public CommandTree ApplyTransforms(CommandTree tree)
    {
        var root = tree.Root;

        foreach (var transform in _rootTransforms)
            root = transform(root);

        if (_commandTransforms.Count > 0)
        {
            foreach (var (path, transform) in _commandTransforms)
                root = ApplyTransformAtPath(root, path, transform, "");
        }

        foreach (var transformer in _transformers)
        {
            root = transformer.TransformRoot(root);
            root = ApplyTransformerRecursive(transformer, root, "");
        }

        return new CommandTree
        {
            BinaryName = tree.BinaryName,
            Version = tree.Version,
            Description = tree.Description,
            Root = root
        };
    }

    private static CommandNode ApplyTransformerRecursive(
        ICommandTreeTransformer transformer, CommandNode node, string parentPath)
    {
        var currentPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath}.{node.Name}";

        node = transformer.TransformCommand(currentPath, node);

        if (node.SubCommands.Count > 0)
        {
            var newSubs = new List<CommandNode>(node.SubCommands.Count);
            foreach (var sub in node.SubCommands)
                newSubs.Add(ApplyTransformerRecursive(transformer, sub, currentPath));

            node = new CommandNode
            {
                Name = node.Name,
                Description = node.Description,
                Options = node.Options,
                Arguments = node.Arguments,
                SubCommands = newSubs
            };
        }

        return node;
    }

    private static CommandNode ApplyTransformAtPath(
        CommandNode node, string targetPath, Func<CommandNode, CommandNode> transform, string parentPath)
    {
        var currentPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath}.{node.Name}";

        if (string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.Name, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return transform(node);
        }

        if (node.SubCommands.Count == 0)
            return node;

        var newSubs = new List<CommandNode>(node.SubCommands.Count);
        foreach (var sub in node.SubCommands)
            newSubs.Add(ApplyTransformAtPath(sub, targetPath, transform, currentPath));

        return new CommandNode
        {
            Name = node.Name,
            Description = node.Description,
            Options = node.Options,
            Arguments = node.Arguments,
            SubCommands = newSubs
        };
    }
}

// ── CLI Commands (placeholder stubs) ────────────────────────────────────────

internal sealed class ScrapeCommand : IToolCommand
{
    public string Name => "scrape";
    public string Description => "Scrape help output from a binary";

    public Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        Console.Error.WriteLine("Not yet implemented");
        return Task.FromResult(1);
    }
}

internal sealed class GenerateCommand : IToolCommand
{
    public string Name => "generate";
    public string Description => "Generate wrapper code from scraped data";

    public Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        Console.Error.WriteLine("Not yet implemented");
        return Task.FromResult(1);
    }
}

internal sealed class VersionDiffCommand : IToolCommand
{
    public string Name => "version-diff";
    public string Description => "Compare command trees between versions";

    public Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        Console.Error.WriteLine("Not yet implemented");
        return Task.FromResult(1);
    }
}

internal sealed class ScrapeAllCommand : IToolCommand
{
    public string Name => "scrape-all";
    public string Description => "Scrape help from multiple versions of a binary";

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        string? binary = null;
        string? outputDir = null;
        var parallel = 4;
        string? versionsArg = null;
        string? collectorType = null;
        string? repo = null;
        string? parserType = null;
        string? helpFlag = null;
        string? minVersion = null;

        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--binary": binary = args[++i]; break;
                case "--output": outputDir = args[++i]; break;
                case "--parallel": parallel = int.Parse(args[++i]); break;
                case "--versions": versionsArg = args[++i]; break;
                case "--collector": collectorType = args[++i]; break;
                case "--repo": repo = args[++i]; break;
                case "--parser": parserType = args[++i]; break;
                case "--help-flag": helpFlag = args[++i]; break;
                case "--min-version": minVersion = args[++i]; break;
            }
        }

        if (binary is null)
        {
            Console.Error.WriteLine(
                "Usage: binary-wrapper scrape-all --binary <name> [--output <dir>] " +
                "[--parallel <N>] [--versions v1,v2,...] [--collector github --repo owner/repo]");
            return 1;
        }

        outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "scrape");

        IVersionCollector collector;
        if (versionsArg is not null)
        {
            var versions = versionsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            collector = new StaticVersionCollector(versions);
        }
        else if (collectorType == "github" && repo is not null)
        {
            var parts = repo.Split('/');
            if (parts.Length != 2)
            {
                Console.Error.WriteLine("--repo must be in format: owner/repo");
                return 1;
            }
            collector = new GitHubReleasesVersionCollector(parts[0], parts[1]);
        }
        else
        {
            Console.Error.WriteLine("Must specify either --versions or --collector github --repo owner/repo");
            return 1;
        }

        IHelpParser parser = parserType?.ToLowerInvariant() switch
        {
            "packer" => new PackerHelpParser(),
            _ => new StandardHelpParser()
        };

        var scraper = new MultiVersionScraper(
            collector,
            version =>
            {
                var pipeline = new ScrapePipeline()
                    .Binary(binary)
                    .UseParser(parser)
                    .WithRunHelp(ProcessRunnerContainerRuntime.RunProcessAsync)
                    .OutputTo(Path.Combine(outputDir, $"{binary}-{version}.json"));
                if (helpFlag is not null)
                    pipeline.HelpFlag(helpFlag);
                return pipeline;
            },
            parallel);

        scraper.Progress += (result, done, total) =>
        {
            var status = result.Success ? "OK" : $"FAILED: {result.ErrorMessage}";
            Console.WriteLine($"[{done}/{total}] {result.Version}: {status}");
        };

        Directory.CreateDirectory(outputDir);
        var results = await scraper.ScrapeAllAsync(cancellationToken);

        var succeeded = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);
        Console.WriteLine($"\nDone. {succeeded} succeeded, {failed} failed.");
        return 0;
    }
}