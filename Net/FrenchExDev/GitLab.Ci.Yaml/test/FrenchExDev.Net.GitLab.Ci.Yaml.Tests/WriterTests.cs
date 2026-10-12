using Shouldly;
using FrenchExDev.Net.GitLab.Ci.Yaml.Serialization;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.Tests;

public class WriterTests
{
    [Fact]
    public void Serialize_stages()
    {
        var file = new GitLabCiFile
        {
            Stages = new List<object> { "build", "test", "deploy" }
        };

        var yaml = GitLabCiYamlWriter.Serialize(file);

        yaml.ShouldContain("stages:");
        yaml.ShouldContain("- build");
        yaml.ShouldContain("- test");
        yaml.ShouldContain("- deploy");
    }

    [Fact]
    public void Serialize_jobs_at_root_level()
    {
        var file = new GitLabCiFile
        {
            Jobs = new Dictionary<string, GitLabCiJob>
            {
                ["build"] = new GitLabCiJob
                {
                    Script = new List<string> { "npm ci", "npm run build" }
                }
            }
        };

        var yaml = GitLabCiYamlWriter.Serialize(file);

        // Jobs should be at root level, not nested under "jobs:"
        yaml.ShouldContain("build:");
        yaml.ShouldNotContain("jobs:");
        yaml.ShouldContain("npm ci");
    }

    [Fact]
    public void Serialize_omits_null_properties()
    {
        var file = new GitLabCiFile
        {
            Stages = new List<object> { "build" }
        };

        var yaml = GitLabCiYamlWriter.Serialize(file);

        yaml.ShouldNotContain("variables:");
        yaml.ShouldNotContain("include:");
    }

    [Fact]
    public void Serialize_to_writer()
    {
        var file = new GitLabCiFile
        {
            Stages = new List<object> { "build" }
        };

        using var sw = new StringWriter();
        GitLabCiYamlWriter.Serialize(file, sw);

        var yaml = sw.ToString();
        yaml.ShouldContain("stages:");
    }
}
