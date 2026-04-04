using System.Text;
using YamlDotNet.Serialization;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.Serialization;

/// <summary>
/// Converts PascalCase C# property names to snake_case YAML keys.
/// </summary>
public sealed class GitLabCiNamingConvention : INamingConvention
{
    public static readonly GitLabCiNamingConvention Instance = new();

    public string Apply(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sb = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
            {
                sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    public string Reverse(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sb = new StringBuilder();
        var capitalizeNext = true;
        foreach (var c in value)
        {
            if (c == '_')
            {
                capitalizeNext = true;
                continue;
            }
            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }
        return sb.ToString();
    }
}
