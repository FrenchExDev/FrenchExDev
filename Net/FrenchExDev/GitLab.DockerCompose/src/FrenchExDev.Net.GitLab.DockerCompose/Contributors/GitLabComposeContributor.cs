using FrenchExDev.Net.DockerCompose.Bundle;
using FrenchExDev.Net.GitLab.DockerCompose.Rendering;

namespace FrenchExDev.Net.GitLab.DockerCompose.Contributors;

/// <summary>
/// Contributes a GitLab CE/EE service to a <see cref="ComposeFile"/>.
/// Renders <see cref="GitLabOmnibusConfig"/> to Ruby syntax and injects it
/// as the <c>GITLAB_OMNIBUS_CONFIG</c> environment variable.
/// </summary>
public sealed class GitLabComposeContributor : IComposeFileContributor
{
    private readonly GitLabOmnibusConfig? _omnibusConfig;
    private readonly string _image;
    private readonly string _tag;
    private readonly string _hostname;
    private readonly string? _rootPassword;
    private readonly string? _timezone;
    private readonly string _shmSize;
    private readonly string _memoryLimit;
    private readonly Dictionary<string, string?>? _labels;
    private readonly List<string>? _networks;

    public GitLabComposeContributor(
        GitLabOmnibusConfig? omnibusConfig = null,
        string image = GitLabDockerImages.GitLabCe,
        string tag = GitLabDockerImages.DefaultGitLabTag,
        string hostname = "gitlab.example.com",
        string? rootPassword = null,
        string? timezone = null,
        string shmSize = "256m",
        string memoryLimit = "8G",
        Dictionary<string, string?>? labels = null,
        List<string>? networks = null)
    {
        _omnibusConfig = omnibusConfig;
        _image = image;
        _tag = tag;
        _hostname = hostname;
        _rootPassword = rootPassword;
        _timezone = timezone;
        _shmSize = shmSize;
        _memoryLimit = memoryLimit;
        _labels = labels;
        _networks = networks;
    }

    public void Contribute(ComposeFile composeFile)
    {
        composeFile.Services ??= new Dictionary<string, ComposeService>();
        composeFile.Volumes ??= new Dictionary<string, ComposeVolume?>();

        // Environment
        var env = new Dictionary<string, string?>();
        if (_omnibusConfig is not null)
            env["GITLAB_OMNIBUS_CONFIG"] = GitLabRbRenderer.Render(_omnibusConfig);
        if (_rootPassword is not null)
            env["GITLAB_ROOT_PASSWORD"] = _rootPassword;
        if (_timezone is not null)
            env["TZ"] = _timezone;

        // Service
        var service = new ComposeService
        {
            Image = $"{_image}:{_tag}",
            Hostname = _hostname,
            Restart = "always",
            Environment = env.Count > 0 ? env : null,
            Volumes = new List<ComposeServiceVolumesConfig>
            {
                new() { Type = "volume", Source = "gitlab-config", Target = "/etc/gitlab" },
                new() { Type = "volume", Source = "gitlab-logs", Target = "/var/log/gitlab" },
                new() { Type = "volume", Source = "gitlab-data", Target = "/var/opt/gitlab" },
            },
            Ports = new List<ComposeServicePortsConfig>
            {
                new() { Target = 80, Published = 80 },
                new() { Target = 443, Published = 443 },
                new() { Target = 22, Published = 22 },
            },
            Healthcheck = new ComposeHealthcheck
            {
                Test = new List<string> { "CMD", "curl", "-f", "http://localhost/-/health" },
                Interval = "30s",
                Timeout = "10s",
                Retries = 5,
                StartPeriod = "300s",
            },
            Labels = _labels,
        };

        composeFile.Services["gitlab"] = service;

        // Volumes
        composeFile.Volumes["gitlab-config"] = null;
        composeFile.Volumes["gitlab-logs"] = null;
        composeFile.Volumes["gitlab-data"] = null;
    }
}
