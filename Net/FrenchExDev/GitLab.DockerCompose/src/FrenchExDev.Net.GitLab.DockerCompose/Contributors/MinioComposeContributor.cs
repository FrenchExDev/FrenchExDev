using FrenchExDev.Net.DockerCompose.Bundle;

namespace FrenchExDev.Net.GitLab.DockerCompose.Contributors;

/// <summary>
/// Contributes a MinIO S3-compatible object storage service for GitLab.
/// </summary>
public sealed class MinioComposeContributor : IComposeFileContributor
{
    private readonly string _tag;
    private readonly string _rootUser;
    private readonly string _rootPassword;

    public MinioComposeContributor(
        string tag = GitLabDockerImages.DefaultMinioTag,
        string rootUser = "${MINIO_ROOT_USER}",
        string rootPassword = "${MINIO_ROOT_PASSWORD}")
    {
        _tag = tag;
        _rootUser = rootUser;
        _rootPassword = rootPassword;
    }

    public void Contribute(ComposeFile composeFile)
    {
        composeFile.Services ??= new Dictionary<string, ComposeService>();
        composeFile.Volumes ??= new Dictionary<string, ComposeVolume?>();

        composeFile.Services["minio"] = new ComposeService
        {
            Image = $"{GitLabDockerImages.Minio}:{_tag}",
            Restart = "always",
            Command = new List<string> { "server", "/data", "--console-address", ":9001" },
            Environment = new Dictionary<string, string?>
            {
                ["MINIO_ROOT_USER"] = _rootUser,
                ["MINIO_ROOT_PASSWORD"] = _rootPassword,
            },
            Volumes = new List<ComposeServiceVolumesConfig>
            {
                new() { Type = "volume", Source = "minio-data", Target = "/data" },
            },
            Ports = new List<ComposeServicePortsConfig>
            {
                new() { Target = 9000, Published = 9000 },
                new() { Target = 9001, Published = 9001 },
            },
            Healthcheck = new ComposeHealthcheck
            {
                Test = new List<string> { "CMD", "curl", "-f", "http://localhost:9000/minio/health/live" },
                Interval = "30s",
                Timeout = "10s",
                Retries = 3,
            },
        };

        composeFile.Volumes["minio-data"] = null;
    }
}
