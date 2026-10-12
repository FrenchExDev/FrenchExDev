using FrenchExDev.Net.DockerCompose.Bundle;

namespace FrenchExDev.Net.GitLab.DockerCompose.Contributors;

/// <summary>
/// Contributes a GitLab Runner service.
/// Depends on the GitLab service being healthy.
/// </summary>
public sealed class GitLabRunnerComposeContributor : IComposeFileContributor
{
    private readonly string _tag;

    public GitLabRunnerComposeContributor(string tag = GitLabDockerImages.DefaultRunnerTag)
    {
        _tag = tag;
    }

    public void Contribute(ComposeFile composeFile)
    {
        composeFile.Services ??= new Dictionary<string, ComposeService>();
        composeFile.Volumes ??= new Dictionary<string, ComposeVolume?>();

        composeFile.Services["gitlab-runner"] = new ComposeService
        {
            Image = $"{GitLabDockerImages.GitLabRunner}:{_tag}",
            Restart = "always",
            Volumes = new List<ComposeServiceVolumesConfig>
            {
                new() { Type = "volume", Source = "runner-config", Target = "/etc/gitlab-runner" },
                new() { Type = "bind", Source = "/var/run/docker.sock", Target = "/var/run/docker.sock" },
            },
            DependsOn = new ComposeServiceDependsOnCondition
            {
                Condition = "service_healthy",
            },
        };

        composeFile.Volumes["runner-config"] = null;
    }
}
