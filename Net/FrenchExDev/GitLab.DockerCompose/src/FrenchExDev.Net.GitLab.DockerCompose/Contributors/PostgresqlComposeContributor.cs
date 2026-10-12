using FrenchExDev.Net.DockerCompose.Bundle;

namespace FrenchExDev.Net.GitLab.DockerCompose.Contributors;

/// <summary>
/// Contributes an external PostgreSQL service for GitLab.
/// </summary>
public sealed class PostgresqlComposeContributor : IComposeFileContributor
{
    private readonly string _tag;
    private readonly string _database;
    private readonly string _username;
    private readonly string _password;

    public PostgresqlComposeContributor(
        string tag = GitLabDockerImages.DefaultPostgresTag,
        string database = "gitlabhq_production",
        string username = "gitlab",
        string password = "${GITLAB_DB_PASSWORD}")
    {
        _tag = tag;
        _database = database;
        _username = username;
        _password = password;
    }

    public void Contribute(ComposeFile composeFile)
    {
        composeFile.Services ??= new Dictionary<string, ComposeService>();
        composeFile.Volumes ??= new Dictionary<string, ComposeVolume?>();

        composeFile.Services["postgresql"] = new ComposeService
        {
            Image = $"{GitLabDockerImages.Postgres}:{_tag}",
            Restart = "always",
            Environment = new Dictionary<string, string?>
            {
                ["POSTGRES_DB"] = _database,
                ["POSTGRES_USER"] = _username,
                ["POSTGRES_PASSWORD"] = _password,
            },
            Volumes = new List<ComposeServiceVolumesConfig>
            {
                new() { Type = "volume", Source = "postgresql-data", Target = "/var/lib/postgresql/data" },
            },
            Healthcheck = new ComposeHealthcheck
            {
                Test = new List<string> { "CMD-SHELL", $"pg_isready -U {_username}" },
                Interval = "10s",
                Timeout = "5s",
                Retries = 5,
            },
        };

        composeFile.Volumes["postgresql-data"] = null;
    }
}
