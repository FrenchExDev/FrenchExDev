using Shouldly;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.Tests;

public class ModelTests
{
    [Fact]
    public void GitLabCiFile_can_be_created()
    {
        var file = new GitLabCiFile();
        file.ShouldNotBeNull();
    }

    [Fact]
    public void GitLabCiFile_jobs_can_be_set()
    {
        var file = new GitLabCiFile
        {
            Jobs = new Dictionary<string, GitLabCiJob>
            {
                ["build"] = new GitLabCiJob()
            }
        };

        file.Jobs.ShouldContainKey("build");
    }

    [Fact]
    public void GitLabCiFile_stages_can_be_set()
    {
        var file = new GitLabCiFile
        {
            Stages = new List<object> { "build", "test", "deploy" }
        };

        file.Stages!.Count.ShouldBe(3);
    }

    [Fact]
    public void Contributor_can_modify_file()
    {
        var file = new GitLabCiFile();
        var contributor = new TestContributor();
        file.Apply(contributor);

        file.Stages.ShouldNotBeNull();
        file.Stages!.Count.ShouldBe(1);
    }

    private sealed class TestContributor : IGitLabCiContributor
    {
        public void Contribute(GitLabCiFile ciFile)
        {
            ciFile.Stages = new List<object> { "contributed" };
        }
    }
}
