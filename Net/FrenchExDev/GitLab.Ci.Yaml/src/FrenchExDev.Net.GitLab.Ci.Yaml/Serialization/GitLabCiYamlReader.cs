using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.Serialization;

/// <summary>
/// Deserializes a .gitlab-ci.yml YAML file into a <see cref="GitLabCiFile"/>.
/// Separates reserved root-level keys from job definitions.
/// </summary>
public static class GitLabCiYamlReader
{
    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "stages", "variables", "include", "default", "workflow",
        "image", "services", "before_script", "after_script", "cache",
        "spec", "pages",
    };

    private static readonly IDeserializer JobDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new StringOrListConverter())
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Deserializes a YAML string into a CI file model.</summary>
    public static GitLabCiFile Deserialize(string yaml)
    {
        var rawDeserializer = new DeserializerBuilder().Build();
        var raw = rawDeserializer.Deserialize<Dictionary<string, object?>>(yaml);

        if (raw is null)
            return new GitLabCiFile();

        var ciFile = new GitLabCiFile();
        var serializer = new SerializerBuilder().Build();

        // Populate known root properties
        if (raw.TryGetValue("stages", out var stages) && stages is IList<object?> stageList)
            ciFile.Stages = stageList.Cast<object>().ToList();

        if (raw.TryGetValue("variables", out var vars) && vars is IDictionary<object, object?> varDict)
            ciFile.Variables = varDict.ToDictionary(k => k.Key?.ToString() ?? "", k => k.Value);

        // Collect job entries
        var jobs = new Dictionary<string, GitLabCiJob>();
        foreach (var kvp in raw)
        {
            if (ReservedKeys.Contains(kvp.Key) || kvp.Key.StartsWith("."))
                continue;

            // YamlDotNet raw deserialization returns nested mappings as various dictionary types
            if (kvp.Value is not null and not string and not IList<object?>)
            {
                try
                {
                    var jobYaml = serializer.Serialize(kvp.Value);
                    var job = JobDeserializer.Deserialize<GitLabCiJob>(jobYaml);
                    if (job is not null)
                        jobs[kvp.Key] = job;
                }
                catch
                {
                    // Job structure incompatible with typed deserialization
                    // Still create a minimal job entry
                    jobs[kvp.Key] = new GitLabCiJob();
                }
            }
        }

        if (jobs.Count > 0)
            ciFile.Jobs = jobs;

        return ciFile;
    }

    /// <summary>Deserializes from a <see cref="TextReader"/>.</summary>
    public static GitLabCiFile Deserialize(TextReader reader)
    {
        return Deserialize(reader.ReadToEnd());
    }
}
