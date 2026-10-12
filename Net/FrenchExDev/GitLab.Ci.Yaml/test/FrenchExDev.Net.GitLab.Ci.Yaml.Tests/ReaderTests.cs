using Shouldly;
using FrenchExDev.Net.GitLab.Ci.Yaml.Serialization;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.Tests;

public class ReaderTests
{
    [Fact]
    public void Deserialize_simple_pipeline()
    {
        var yaml = File.ReadAllText("Fixtures/simple-pipeline.yml");
        var file = GitLabCiYamlReader.Deserialize(yaml);

        file.ShouldNotBeNull();
        file.Stages.ShouldNotBeNull();
        file.Stages!.Count.ShouldBe(3);
    }

    [Fact]
    public void Deserialize_extracts_jobs()
    {
        var yaml = "build_job:\n  stage: build\n  script:\n    - echo building\ntest_job:\n  stage: test\n  script:\n    - echo testing\n";
        var file = GitLabCiYamlReader.Deserialize(yaml);

        file.Jobs.ShouldNotBeNull();
        file.Jobs!.ShouldContainKey("build_job");
        file.Jobs!.ShouldContainKey("test_job");
    }

    [Fact]
    public void Deserialize_job_script_as_list()
    {
        var yaml = "build:\n  script:\n    - npm ci\n    - npm run build\n";
        var file = GitLabCiYamlReader.Deserialize(yaml);

        file.Jobs.ShouldNotBeNull();
        var job = file.Jobs!["build"];
        job.Script.ShouldNotBeNull();
        job.Script!.Count.ShouldBe(2);
        job.Script[0].ShouldBe("npm ci");
        job.Script[1].ShouldBe("npm run build");
    }

    [Fact]
    public void Deserialize_job_script_as_single_string()
    {
        var yaml = "build:\n  script: echo hello\n";
        var file = GitLabCiYamlReader.Deserialize(yaml);

        file.Jobs.ShouldNotBeNull();
        var job = file.Jobs!["build"];
        job.Script.ShouldNotBeNull();
        job.Script!.Count.ShouldBe(1);
        job.Script[0].ShouldBe("echo hello");
    }

    [Fact]
    public void Deserialize_from_reader()
    {
        using var reader = new StreamReader("Fixtures/simple-pipeline.yml");
        var file = GitLabCiYamlReader.Deserialize(reader);

        file.ShouldNotBeNull();
        file.Stages.ShouldNotBeNull();
    }

    [Fact]
    public void Deserialize_empty_yaml_returns_empty_file()
    {
        var file = GitLabCiYamlReader.Deserialize("{}");
        file.ShouldNotBeNull();
    }

    [Fact]
    public void Deserialize_full_pipeline_from_fixture()
    {
        var yaml = File.ReadAllText("Fixtures/simple-pipeline.yml");
        var file = GitLabCiYamlReader.Deserialize(yaml);

        file.Jobs.ShouldNotBeNull();
        file.Jobs!.ShouldContainKey("build");
        file.Jobs!.ShouldContainKey("test");
        file.Jobs!.ShouldContainKey("deploy");

        file.Jobs["build"].Script.ShouldNotBeNull();
        file.Jobs["build"].Script!.ShouldContain("npm ci");
    }
}
