using System;
using System.Linq;
using System.Text;

namespace FrenchExDev.Net.Packer.Bundle.SourceGenerator.Lib;

/// <summary>
/// Converts HCL/Go naming conventions to C# naming conventions.
/// </summary>
public static class NamingHelper
{
    /// <summary>Converts a snake_case or kebab-case name to PascalCase.</summary>
    public static string ToPascalCase(string name)
    {
        var sb = new StringBuilder();
        var capitalizeNext = true;

        foreach (var c in name)
        {
            if (c == '_' || c == '-')
            {
                capitalizeNext = true;
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts a Packer plugin type name to a C# class name.
    /// E.g. "virtualbox-iso" → "VirtualBoxIsoSource", "shell" → "ShellProvisioner".
    /// </summary>
    public static string ToClassName(string typeName, string pluginKind)
    {
        var pascal = ToPascalCase(typeName);
        return pluginKind switch
        {
            "builder" => pascal + "Source",
            "provisioner" => pascal + "Provisioner",
            "post-processor" => pascal + "PostProcessor",
            "datasource" => pascal + "DataSource",
            _ => pascal
        };
    }

    /// <summary>
    /// Maps a cty type string from .hcl2spec.go to a C# type string.
    /// </summary>
    public static string CtyTypeToCSharp(string ctyType)
    {
        var trimmed = ctyType.Trim();
        if (trimmed == "cty.String") return "string?";
        if (trimmed == "cty.Bool") return "bool?";
        if (trimmed == "cty.Number") return "int?";
        if (trimmed == "cty.List(cty.String)") return "List<string>?";
        if (trimmed == "cty.List(cty.Number)") return "List<int>?";
        if (trimmed == "cty.List(cty.Bool)") return "List<bool>?";
        if (trimmed == "cty.List(cty.List(cty.String))") return "List<List<string>>?";
        if (trimmed == "cty.Map(cty.String)") return "Dictionary<string, string>?";
        if (trimmed == "cty.Map(cty.Bool)") return "Dictionary<string, bool>?";
        if (trimmed == "cty.Set(cty.String)") return "List<string>?";
        if (trimmed == "cty.Set(cty.Number)") return "List<int>?";

        // For complex/unknown types, fall back to string
        return "string?";
    }

    /// <summary>
    /// Maps a cty type to the appropriate default value expression in C#.
    /// Returns null if no explicit default is needed.
    /// </summary>
    public static string? CtyTypeToDefault(string ctyType)
    {
        var trimmed = ctyType.Trim();
        if (trimmed.StartsWith("cty.List") || trimmed.StartsWith("cty.Set"))
            return null; // nullable, no default
        if (trimmed.StartsWith("cty.Map"))
            return null;
        return null; // all nullable by default
    }
}
