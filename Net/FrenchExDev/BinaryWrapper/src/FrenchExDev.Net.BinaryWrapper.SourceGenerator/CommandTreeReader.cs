using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrenchExDev.Net.BinaryWrapper.SourceGenerator;

/// <summary>
/// Reads CommandTree JSON from AdditionalFiles at compile time.
/// Mirror of the Design project's models, needed because the source generator
/// is netstandard2.0 and cannot reference the Design assembly.
/// </summary>
internal static class CommandTreeReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Parses a CommandTree from JSON text.
    /// </summary>
    public static CommandTreeModel? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CommandTreeModel>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a semantic version from a filename like "packer-1.11.2.json".
    /// </summary>
    public static string? ExtractVersionFromFileName(string fileName, string binaryName)
    {
        // Expected: "{binaryName}-{version}.json"
        var prefix = binaryName + "-";
        var suffix = ".json";

        if (!fileName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
            return null;

        return fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length);
    }
}

// ── Mirror Models (compile-time only) ───────────────────────────────────────

internal sealed class CommandTreeModel
{
    public string BinaryName { get; set; } = "";
    public string? Version { get; set; }
    public string? Description { get; set; }
    public CommandNodeModel Root { get; set; } = new();
}

internal sealed class CommandNodeModel
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<OptionModel> Options { get; set; } = new();
    public List<ArgumentModel> Arguments { get; set; } = new();
    public List<CommandNodeModel> SubCommands { get; set; } = new();
}

internal sealed class OptionModel
{
    public string LongName { get; set; } = "";
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public string ValueKind { get; set; } = "single";
    public string ClrType { get; set; } = "string";
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
}

internal sealed class ArgumentModel
{
    public string Name { get; set; } = "";
    public int Position { get; set; }
    public string? Description { get; set; }
    public string ClrType { get; set; } = "string";
    public bool IsRequired { get; set; } = true;
    public bool IsVariadic { get; set; }
    public string? DefaultValue { get; set; }
}
