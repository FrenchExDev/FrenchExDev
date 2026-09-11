using System.Text;

namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// Models a .env file — actual key=value pairs.
/// Mutable — contributors add values.
/// </summary>
public sealed class EnvValues
{
    public Dictionary<string, string> Values { get; set; } = new();

    /// <summary>Renders as a .env file string.</summary>
    public string Render()
    {
        var sb = new StringBuilder();
        foreach (var kvp in Values)
            sb.AppendLine($"{kvp.Key}={kvp.Value}");
        return sb.ToString();
    }

    /// <summary>Parses a .env file string into key=value pairs.</summary>
    public static EnvValues Parse(string content)
    {
        var values = new EnvValues();
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;

            var eq = trimmed.IndexOf('=');
            if (eq <= 0) continue;

            var key = trimmed.Substring(0, eq).Trim();
            var value = trimmed.Substring(eq + 1).Trim();
            values.Values[key] = value;
        }
        return values;
    }
}
