# BinaryWrapper Scripts

## Find-Missing.ps1

PowerShell script to list and scrape missing versions of a wrapper.

The source script lives in `BinaryWrapper/resources/scripts/Find-Missing.ps1` and must be copied into the `scripts/` directory of each consumer solution (Docker, Podman, Git, etc.).

### Usage

From the root directory of a consumer solution (e.g. `Net/FrenchExDev/Docker/`):

```powershell
# List all available versions
./scripts/Find-Missing.ps1 -List

# List only missing versions (not yet scraped)
./scripts/Find-Missing.ps1 -Missing -List

# Scrape missing versions
./scripts/Find-Missing.ps1 -Missing
```

### How it works

The script reads the `.binary-wrapper.yaml` file at the consumer solution root to determine the Design project path:

```yaml
binary: src/FrenchExDev.Net.Docker/FrenchExDev.Net.Docker.csproj
design: src/FrenchExDev.Net.Docker.Design/FrenchExDev.Net.Docker.Design.csproj
```

It then runs `dotnet run --project <design> -- --missing --list` based on the switches provided.

### Consumer solutions

The script is present in each BinaryWrapper consumer:

- `Docker/scripts/Find-Missing.ps1`
- `DockerCompose/scripts/Find-Missing.ps1`
- `Git/scripts/Find-Missing.ps1`
- `GitLab.Cli/scripts/Find-Missing.ps1`
- `Packer/scripts/Find-Missing.ps1`
- `Podman/scripts/Find-Missing.ps1`
- `PodmanCompose/scripts/Find-Missing.ps1`
- `Vagrant/scripts/Find-Missing.ps1`

## GitHub Authentication (avoiding 403 Too Many Requests)

The GitHub API enforces a rate limit of **60 requests/hour** without authentication (5000 with a token). When scraping many versions, this limit is quickly exhausted.

### Setting up the token

Create a `.env` file in `Net/FrenchExDev/`:

```
GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
```

This file is git-ignored (`.gitignore` contains `.env`). Never commit it.

To obtain a token: GitHub > Settings > Developer settings > Personal access tokens > Fine-grained tokens. No scopes are required to read tags and releases of public repositories.

### How it works

#### DotEnvLoader

The `DotEnvLoader` class (`BinaryWrapper.Design.Lib`) loads the `.env` file at startup of each Design project.

It works by **recursive upward traversal**: starting from the current directory, it looks for a `.env` file in each parent directory until it finds one (or reaches the filesystem root).

This means a single `.env` file placed in `Net/FrenchExDev/` is sufficient for all Design projects, regardless of which directory `dotnet run` is launched from.

Parsing rules:
- Format `KEY=VALUE` (one per line)
- Empty lines and comments (`#`) are ignored
- Whitespace around keys and values is trimmed
- `=` signs within the value are preserved (`KEY=val=ue` yields `val=ue`)

#### Token injection into collectors

Each GitHub-consuming Design project's `Program.cs` loads the token and passes it to the collector:

```csharp
var env = DotEnvLoader.Load();
env.TryGetValue("GITHUB_TOKEN", out var githubToken);

// ...
VersionCollector = new GitHubTagsVersionCollector("docker", "cli", token: githubToken),
```

The constructors of `GitHubTagsVersionCollector` and `GitHubReleasesVersionCollector` accept an optional `string? token` parameter. When provided, the `Authorization: Bearer <token>` header is added to all requests to the GitHub API.

The `token` parameter is only used when the collector creates its own `HttpClient` (default behavior). If an explicit `HttpClient` is provided, the caller manages authentication themselves.

Affected Design projects:
- **Docker** and **Git**: `GitHubTagsVersionCollector`
- **Podman**, **DockerCompose** and **PodmanCompose**: `GitHubReleasesVersionCollector`

The GitLab collector (`GitLabReleasesVersionCollector`) uses a different mechanism: it reads the `GITLAB_TOKEN` environment variable directly and sends it via the `PRIVATE-TOKEN` header.
