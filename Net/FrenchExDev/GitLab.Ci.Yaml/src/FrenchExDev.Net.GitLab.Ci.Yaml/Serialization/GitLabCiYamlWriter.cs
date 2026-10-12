using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.Serialization;

/// <summary>
/// Serializes a <see cref="GitLabCiFile"/> to valid .gitlab-ci.yml YAML.
/// Root-level reserved keys and job entries are merged into a single flat YAML document.
/// </summary>
public static class GitLabCiYamlWriter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .DisableAliases()
        .Build();

    /// <summary>Serializes a CI file to a YAML string.</summary>
    public static string Serialize(GitLabCiFile ciFile)
    {
        var dict = BuildRootDictionary(ciFile);
        return Serializer.Serialize(dict);
    }

    /// <summary>Serializes a CI file to a <see cref="TextWriter"/>.</summary>
    public static void Serialize(GitLabCiFile ciFile, TextWriter writer)
    {
        var dict = BuildRootDictionary(ciFile);
        Serializer.Serialize(writer, dict);
    }

    /// <summary>
    /// Builds a flat dictionary that represents the root-level YAML mapping.
    /// Reserved keys (stages, variables, include, default, workflow, etc.) come first,
    /// then job entries are merged at root level.
    /// </summary>
    private static Dictionary<string, object?> BuildRootDictionary(GitLabCiFile ciFile)
    {
        var dict = new Dictionary<string, object?>();

        // Reserved root-level keys — use reflection-free explicit mapping
        if (ciFile.Stages is not null)
            dict["stages"] = ciFile.Stages;
        if (ciFile.Variables is not null)
            dict["variables"] = ciFile.Variables;
        if (ciFile.Include is not null)
            dict["include"] = ciFile.Include;
        if (ciFile.Default is not null)
            dict["default"] = ciFile.Default;
        if (ciFile.Workflow is not null)
            dict["workflow"] = ciFile.Workflow;

        // Emit any other root properties that exist via Extensions
        if (ciFile.Extensions is not null)
        {
            foreach (var kvp in ciFile.Extensions)
                dict[kvp.Key] = kvp.Value;
        }

        // Merge jobs at root level (not nested under "jobs:" key)
        if (ciFile.Jobs is not null)
        {
            foreach (var kvp in ciFile.Jobs)
                dict[kvp.Key] = kvp.Value;
        }

        return dict;
    }
}
