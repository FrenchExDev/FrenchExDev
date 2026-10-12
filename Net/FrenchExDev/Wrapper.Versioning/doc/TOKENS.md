# Token Management

Version collectors query the GitHub and GitLab APIs to discover available versions. These APIs enforce rate limits that make token authentication essential in practice.

## Rate Limits

| Platform | Without token | With token |
|---|---|---|
| GitHub API | 60 req/hour | 5,000 req/hour |
| GitLab API v4 | 300 req/hour | 600 req/hour |

Without a token, scraping a few dozen versions is enough to exhaust the GitHub quota, resulting in `403 Too Many Requests` errors.

## Supported Tokens

### GITHUB_TOKEN

Used by `GitHubTagsVersionCollector` and `GitHubReleasesVersionCollector`.

- HTTP header: `Authorization: Bearer <token>`
- Required scopes: none (reading tags/releases of public repositories)
- Create at: GitHub > Settings > Developer settings > Personal access tokens > Fine-grained tokens

Affected projects:

| Project | Collector |
|---|---|
| Docker | `GitHubTagsVersionCollector` |
| Git | `GitHubTagsVersionCollector` |
| Podman | `GitHubReleasesVersionCollector` |
| DockerCompose | `GitHubReleasesVersionCollector` |
| PodmanCompose | `GitHubReleasesVersionCollector` |

### GITLAB_TOKEN

Used by `GitLabReleasesVersionCollector`.

- HTTP header: `PRIVATE-TOKEN: <token>` (GitLab API v4 convention)
- Required scopes: `read_api` (for private repositories)
- Create at: GitLab > Preferences > Access Tokens > Personal access tokens

Affected projects:

| Project | Collector |
|---|---|
| GitLab.Cli | `GitLabReleasesVersionCollector` |

## Configuration

### `.env` file (recommended)

A single `.env` file placed in `Net/FrenchExDev/` is shared by all Design projects:

```
GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
GITLAB_TOKEN=glpat-xxxxxxxxxxxxxxxxxxxx
```

This file is git-ignored. Never commit it.

### Loading via DotEnvLoader

The `DotEnvLoader` class (`BinaryWrapper.Design.Lib`) locates the `.env` file by recursive upward traversal: it starts from the current directory and walks up the tree until it finds a `.env` file.

Each Design project loads the token at startup:

```csharp
var env = DotEnvLoader.Load();
env.TryGetValue("GITHUB_TOKEN", out var githubToken);

var runner = new DesignPipelineRunner<string>
{
    VersionCollector = new GitHubTagsVersionCollector("docker", "cli", token: githubToken),
    // ...
};
```

### Injection by the DesignPipelineRunner

The runner exposes an `AuthTokenEnvVar` property (default: `"GITHUB_TOKEN"`) that automatically reads the corresponding environment variable and injects it as an `Authorization: Bearer` header on the shared pipeline `HttpClient`.

To disable automatic authentication: `AuthTokenEnvVar = null`.

The `GitLabReleasesVersionCollector` manages its own token independently by reading the `GITLAB_TOKEN` environment variable directly.

## Troubleshooting

| Symptom | Likely cause | Solution |
|---|---|---|
| `403 Too Many Requests` | GitHub quota exhausted | Add `GITHUB_TOKEN` to `.env` |
| `401 Unauthorized` | Token expired or invalid | Regenerate the token on GitHub/GitLab |
| Token silently ignored | `.env` missing or misplaced | Verify `.env` is in `Net/FrenchExDev/` |
| No auth header sent | `AuthTokenEnvVar = null` | Remove the `null` or pass the token to the collector directly |
