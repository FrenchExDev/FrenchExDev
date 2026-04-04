namespace FrenchExDev.Net.GitLab.DockerCompose;

public static class GitLabDockerImages
{
    public const string GitLabCe = "gitlab/gitlab-ce";
    public const string GitLabEe = "gitlab/gitlab-ee";
    public const string GitLabRunner = "gitlab/gitlab-runner";
    public const string Postgres = "postgres";
    public const string Redis = "redis";
    public const string Minio = "minio/minio";

    public const string DefaultGitLabTag = "latest";
    public const string DefaultPostgresTag = "16-alpine";
    public const string DefaultRedisTag = "7-alpine";
    public const string DefaultRunnerTag = "alpine";
    public const string DefaultMinioTag = "latest";
}
