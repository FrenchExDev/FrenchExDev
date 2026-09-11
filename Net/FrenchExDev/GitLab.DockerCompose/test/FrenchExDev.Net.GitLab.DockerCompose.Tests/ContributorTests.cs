using FrenchExDev.Net.DockerCompose.Bundle;
using FrenchExDev.Net.GitLab.DockerCompose;
using FrenchExDev.Net.GitLab.DockerCompose.Contributors;

namespace FrenchExDev.Net.GitLab.DockerCompose.Tests;

public class ContributorTests
{
    [Fact]
    public void GitLabContributor_AddsService()
    {
        var file = new ComposeFile();
        var contributor = new GitLabComposeContributor(hostname: "gitlab.lab");

        contributor.Contribute(file);

        file.Services.ShouldNotBeNull();
        file.Services.ShouldContainKey("gitlab");
        file.Services["gitlab"].Image.ShouldBe("gitlab/gitlab-ce:latest");
        file.Services["gitlab"].Hostname.ShouldBe("gitlab.lab");
        file.Services["gitlab"].Restart.ShouldBe("always");
    }

    [Fact]
    public void GitLabContributor_AddsThreeVolumes()
    {
        var file = new ComposeFile();
        new GitLabComposeContributor().Contribute(file);

        file.Volumes.ShouldNotBeNull();
        file.Volumes.ShouldContainKey("gitlab-config");
        file.Volumes.ShouldContainKey("gitlab-logs");
        file.Volumes.ShouldContainKey("gitlab-data");
    }

    [Fact]
    public void GitLabContributor_HasHealthcheck()
    {
        var file = new ComposeFile();
        new GitLabComposeContributor().Contribute(file);

        var hc = file.Services!["gitlab"].Healthcheck;
        hc.ShouldNotBeNull();
        hc.Test.ShouldContain("curl");
        hc.StartPeriod.ShouldBe("300s");
    }

    [Fact]
    public void GitLabContributor_RendersOmnibusConfig()
    {
        var omnibus = new GitLabOmnibusConfig
        {
            ExternalUrl = "https://gitlab.example.com",
        };
        var file = new ComposeFile();
        new GitLabComposeContributor(omnibusConfig: omnibus).Contribute(file);

        var env = file.Services!["gitlab"].Environment;
        env.ShouldNotBeNull();
        env.ShouldContainKey("GITLAB_OMNIBUS_CONFIG");
        env["GITLAB_OMNIBUS_CONFIG"].ShouldContain("external_url");
    }

    [Fact]
    public void PostgresqlContributor_AddsService()
    {
        var file = new ComposeFile();
        new PostgresqlComposeContributor().Contribute(file);

        file.Services.ShouldContainKey("postgresql");
        file.Services!["postgresql"].Image.ShouldBe("postgres:16-alpine");
        file.Volumes!.ShouldContainKey("postgresql-data");
    }

    [Fact]
    public void RedisContributor_AddsService()
    {
        var file = new ComposeFile();
        new RedisComposeContributor().Contribute(file);

        file.Services!.ShouldContainKey("redis");
        file.Services["redis"].Image.ShouldBe("redis:7-alpine");
        file.Volumes!.ShouldContainKey("redis-data");
    }

    [Fact]
    public void RunnerContributor_AddsServiceWithDependsOn()
    {
        var file = new ComposeFile();
        new GitLabRunnerComposeContributor().Contribute(file);

        file.Services!.ShouldContainKey("gitlab-runner");
        var runner = file.Services["gitlab-runner"];
        runner.Image.ShouldBe("gitlab/gitlab-runner:alpine");
        runner.DependsOn.ShouldNotBeNull();
        runner.DependsOn!.Condition.ShouldBe("service_healthy");
    }

    [Fact]
    public void MinioContributor_AddsService()
    {
        var file = new ComposeFile();
        new MinioComposeContributor().Contribute(file);

        file.Services!.ShouldContainKey("minio");
        file.Services["minio"].Image.ShouldBe("minio/minio:latest");
        file.Volumes!.ShouldContainKey("minio-data");
    }

    [Fact]
    public void FullPipeline_AllContributors()
    {
        var omnibus = new GitLabOmnibusConfig
        {
            ExternalUrl = "https://gitlab.example.com",
            Nginx = new NginxConfig
            {
                Enable = true,
                RedirectHttpToHttps = false,
            },
        };

        var file = new ComposeFile()
            .Apply(
                new GitLabComposeContributor(omnibusConfig: omnibus, hostname: "gitlab.lab"),
                new PostgresqlComposeContributor(),
                new RedisComposeContributor(),
                new GitLabRunnerComposeContributor(),
                new MinioComposeContributor());

        file.Services!.Count.ShouldBe(5);
        file.Volumes!.Count.ShouldBe(7); // 3 gitlab + postgresql + redis + runner + minio
    }
}
