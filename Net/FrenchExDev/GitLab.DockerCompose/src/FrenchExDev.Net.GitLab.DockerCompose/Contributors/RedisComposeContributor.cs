using FrenchExDev.Net.DockerCompose.Bundle;

namespace FrenchExDev.Net.GitLab.DockerCompose.Contributors;

/// <summary>
/// Contributes an external Redis service for GitLab.
/// </summary>
public sealed class RedisComposeContributor : IComposeFileContributor
{
    private readonly string _tag;
    private readonly string _password;

    public RedisComposeContributor(
        string tag = GitLabDockerImages.DefaultRedisTag,
        string password = "${GITLAB_REDIS_PASSWORD}")
    {
        _tag = tag;
        _password = password;
    }

    public void Contribute(ComposeFile composeFile)
    {
        composeFile.Services ??= new Dictionary<string, ComposeService>();
        composeFile.Volumes ??= new Dictionary<string, ComposeVolume?>();

        composeFile.Services["redis"] = new ComposeService
        {
            Image = $"{GitLabDockerImages.Redis}:{_tag}",
            Restart = "always",
            Command = new List<string> { "redis-server", "--requirepass", _password },
            Volumes = new List<ComposeServiceVolumesConfig>
            {
                new() { Type = "volume", Source = "redis-data", Target = "/data" },
            },
            Healthcheck = new ComposeHealthcheck
            {
                Test = new List<string> { "CMD", "redis-cli", "ping" },
                Interval = "10s",
                Timeout = "5s",
                Retries = 5,
            },
        };

        composeFile.Volumes["redis-data"] = null;
    }
}
