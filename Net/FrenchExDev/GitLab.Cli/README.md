# FrenchExDev.Net.GitLab.Cli

A strongly-typed C# wrapper for [glab](https://gitlab.com/gitlab-org/cli), the official GitLab CLI, built on the **BinaryWrapper** source-generation infrastructure.

---

## Overview

`FrenchExDev.Net.GitLab.Cli` provides a fully typed, version-aware .NET client for the `glab` binary. Like the existing Podman and Vagrant wrappers, it follows the standard BinaryWrapper pattern:

1. **Design-time** — scrape `glab --help` across multiple versions, producing JSON command-tree files.
2. **Compile-time** — the `BinaryWrapperGenerator` source generator reads those JSON files and emits command classes, builders, and a fluent `GlabClient`.
3. **Runtime** — execute glab commands through the generated client with full IntelliSense, version guards, and streaming output support.

```
Design-time (scrape)          Compile-time (SG)              Runtime
┌─────────────────────┐      ┌────────────────────┐      ┌──────────────────────┐
│ GitLab.Cli.Design   │      │ BinaryWrapper.SG   │      │ GlabClient           │
│                     │      │                    │      │                      │
│ GitLabReleases      │─JSON─▶ CommandTreeReader │─emit─▶ .Issue.List(b => …)  │
│ VersionCollector    │ files│ VersionDiffer      │      │ .Mr.Create(b => …)   │
│ + GlabHelpParser    │      │ Command/Builder/   │      │ .Ci.Run(b => …)      │
│ + Podman pipeline   │      │ ClientEmitter      │      │                      │
└─────────────────────┘      └────────────────────┘      └──────────────────────┘
```

---

## Project structure

```
GitLab.Cli/
├── README.md                                          # This file
├── doc/                                               # Additional documentation
├── src/
│   ├── FrenchExDev.Net.GitLab.Cli/                    # Runtime library (consumer project)
│   │   ├── FrenchExDev.Net.GitLab.Cli.csproj
│   │   ├── GlabDescriptor.cs                          # [BinaryWrapper("glab")] marker
│   │   └── scrape/                                    # glab-{version}.json (AdditionalFiles)
│   │       ├── glab-1.46.1.json
│   │       ├── glab-1.47.0.json
│   │       └── ...
│   └── FrenchExDev.Net.GitLab.Cli.Design/             # Design-time scraper
│       ├── FrenchExDev.Net.GitLab.Cli.Design.csproj
│       ├── Program.cs                                 # Pipeline setup + entry point
│       ├── GlabHelpParser.cs                          # Custom parser for glab help format
│       └── GitLabReleasesVersionCollector.cs           # GitLab API v4 version discovery
└── test/
    └── FrenchExDev.Net.GitLab.Cli.Tests/
        ├── FrenchExDev.Net.GitLab.Cli.Tests.csproj
        └── GlabHelpParserTests.cs
```

---

## Version discovery — GitLabReleasesVersionCollector

Unlike Podman and Vagrant (which use `GitHubReleasesVersionCollector`), glab's canonical releases live on **gitlab.com**, not GitHub. The GitHub mirror at `gitlabhq/cli` is read-only and publishes **no releases**.

### Why not `GitHubReleasesVersionCollector`?

| Aspect | GitHub collector | GitLab situation |
|--------|-----------------|------------------|
| API endpoint | `api.github.com/repos/{owner}/{repo}/releases` | `gitlab.com/api/v4/projects/{id}/releases` |
| Tag format | `v{version}` | `v{version}` (same) |
| Pagination | `Link` header | `Link` header (same) |
| Pre-release filtering | `"prerelease": true` | `"upcoming_release": true` |
| Auth | `GITHUB_TOKEN` (optional) | `GITLAB_TOKEN` (optional, higher rate limits) |

We therefore need a **`GitLabReleasesVersionCollector`** that implements `IVersionCollector` using the GitLab REST API v4:

```
GET https://gitlab.com/api/v4/projects/gitlab-org%2Fcli/releases?per_page=100
```

The implementation follows the same pattern as `GitHubReleasesVersionCollector`:
- Paginate via `Link` header
- Strip `v` prefix from tag names (`DefaultTagToVersion`)
- Filter out upcoming/pre-release entries
- Sort with `CompareVersionStrings`

### Release assets

Each glab release includes platform-specific archives:

```
glab_{version}_linux_amd64.tar.gz     ← used for container scraping
glab_{version}_linux_amd64.deb
glab_{version}_linux_amd64.rpm
glab_{version}_linux_amd64.apk
glab_{version}_macOS_amd64.tar.gz
glab_{version}_macOS_arm64.tar.gz
glab_{version}_windows_amd64.zip
```

The download URL pattern:
```
https://gitlab.com/gitlab-org/cli/-/releases/v{version}/downloads/glab_{version}_linux_amd64.tar.gz
```

---

## Help format and custom parser — GlabHelpParser

glab uses Cobra under the hood but applies a **heavily customized help template** that differs from standard Cobra output. A dedicated `GlabHelpParser` is required.

### Format comparison

**Standard Cobra** (Podman, Docker):
```
Available Commands:
  list        List containers

Flags:
  -a, --all                 Show all containers
      --format string       Pretty-print using Go template
```

**glab custom template**:
```
COMMANDS

  list [--flags]                   List project issues.

FLAGS

  -a --assignee       Filter issue by assignee <username>.
  --author            Filter issue by author <username>.
  -A --all            Get all issues.
  --order             Order issue by <field>. (created_at)
  -p --page           Page number. (1)
```

### Key differences requiring a custom parser

| Feature | Cobra standard | glab custom |
|---------|---------------|-------------|
| Section headers | `Available Commands:` | `COMMANDS` (ALL-CAPS, no colon) |
| Flag separator | `-a, --all` (comma) | `-a --all` (space, no comma) |
| Type hints | `--format string` (inline Go type) | None — must infer from context |
| Default values | `(default "text")` | `(text)` at end of description |
| Value detection | Explicit type token | Heuristic: `(default)` suffix, `<placeholder>` in desc |
| Examples section | `Examples:` | `EXAMPLES` (ALL-CAPS) |
| Aliases section | `Aliases:` | `ALIASES` (ALL-CAPS) |

### Parser heuristics for value kind detection

Since glab omits explicit type hints, `GlabHelpParser` infers `OptionValueKind` using:

1. **Default value in `(...)`** at end of description → `Single` (the parenthesized text is the default)
2. **`<placeholder>`** in description (e.g., `<username>`, `<field>`) → `Single`
3. **"Multiple ... can be comma-separated or specified by repeating the flag"** → `Multiple`
4. **Otherwise** → `Flag` (boolean)

### Skipped commands

| Command | Reason |
|---------|--------|
| `help` | Recursive — echoes parent help |
| `completion` | Shell completion scripts, not useful for wrapper |
| `check-update` | Self-update check, not a real command |

---

## Scraping pipeline

The Design project uses the standard `DesignPipeline` middleware stack, following the Podman pattern:

```csharp
var images = new DesignImagePlan
{
    ImageName = "glab-cli",
    BaseImage = "alpine:3.19",
    Platform = "linux/amd64",
    BaseInstallScript = "apk add --no-cache curl tar",
    InstallScript = v =>
        $"curl -fsSL https://gitlab.com/gitlab-org/cli/-/releases/v{v}/downloads/glab_{v}_linux_amd64.tar.gz -o /tmp/glab.tar.gz && " +
        "tar xzf /tmp/glab.tar.gz --no-same-owner -C /tmp && " +
        "mv /tmp/bin/glab /usr/local/bin/glab && " +
        "chmod +x /usr/local/bin/glab && " +
        "rm -rf /tmp/glab.tar.gz /tmp/bin",
};

var pipeline = new DesignPipeline()
    .UseVersionImage()
    .UseContainer()
    .UseScraper("glab", parser)
    .Build();
```

Set `ImagePlanResolver = new SingleDesignImagePlanResolver(images)` on the `DesignPipelineRunner` alongside the existing version collector and pipelines.

### Pipeline stages

```text
GitLab releases -> prepare/reuse shared Alpine + curl + tar base
                -> build/reuse glab image for each selected version
                -> start container -> collect help -> glab-{version}.json
                -> remove container and version image (unless --keep-images)
```

### Running the scraper

```bash
# From GitLab.Cli/src/FrenchExDev.Net.GitLab.Cli.Design/

# List all discoverable versions
dotnet run --framework net10.0 -- --list

# Scrape all versions from 1.20.0 onward (first with current help format)
dotnet run --framework net10.0 -- --min-version 1.20.0 --dashboard

# Scrape only versions not yet scraped
dotnet run --framework net10.0 -- --missing --dashboard

# Reparse from cached help text (no containers needed)
dotnet run --framework net10.0 -- --reparse --dashboard

# Control parallelism
dotnet run --framework net10.0 -- --parallel 8 --scrape-parallel 6 --dashboard
```

---

## Generated API surface

After scraping and building, the source generator produces:

### Client entry point

```csharp
using FrenchExDev.Net.GitLab.Cli;

var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("glab"),
    ExecutablePath = "/usr/local/bin/glab"
};
var client = GlabClient.Create(binding);
```

### Command groups (generated from scraped versions)

```csharp
// Issues
await client.Issue.List(b => b
    .WithAssignee("@me")
    .WithLabel("bug")
    .WithMilestone("v2.0"));

await client.Issue.Create(b => b
    .WithTitle("Fix login flow")
    .WithLabel("bug,priority::high")
    .WithAssignee("@me"));

// Merge Requests
await client.Mr.Create(b => b
    .WithTitle("Add caching layer")
    .WithDescription("Implements Redis caching for API responses")
    .WithTargetBranch("main"));

await client.Mr.List(b => b
    .WithState("opened")
    .WithAuthor("@me"));

// CI/CD
await client.Ci.Run(b => b
    .WithBranch("develop"));

// Repositories
await client.Repo.Clone(b => b /* ... */);

// Releases
await client.Release.Create(b => b
    .WithTag("v1.0.0")
    .WithNotes("First stable release"));
```

### Version guards

Commands and options that appear or disappear across versions are automatically annotated:

```csharp
// If glab < 1.30.0, this throws at runtime with a clear message:
await client.Duo.Ask(b => b.WithPrompt("Explain this pipeline"));
// → "Command 'duo ask' requires glab >= 1.30.0 (detected: 1.25.0)"
```

---

## Dependencies

### Runtime (FrenchExDev.Net.GitLab.Cli)

| Package | Role |
|---------|------|
| `FrenchExDev.Net.BinaryWrapper` | Runtime abstractions (`ICliCommand`, `CommandExecutor`, etc.) |
| `FrenchExDev.Net.BinaryWrapper.Attributes` | `[BinaryWrapper]` attribute |
| `FrenchExDev.Net.BinaryWrapper.SourceGenerator` | Compile-time source generator (Analyzer) |
| `FrenchExDev.Net.Builder` | `AbstractBuilder<T>` for generated builders |
| `FrenchExDev.Net.Builder.SourceGenerator.Lib` | Builder emission (Analyzer) |
| `FrenchExDev.Net.Result` | `Result<T>` for command execution results |

### Design-time (FrenchExDev.Net.GitLab.Cli.Design)

| Package | Role |
|---------|------|
| `FrenchExDev.Net.BinaryWrapper.Design` | Parsers, scrapers, `IVersionCollector` |
| `FrenchExDev.Net.BinaryWrapper.Design.Lib` | `DesignPipeline`, `DesignPipelineRunner`, middleware |

---

## Minimum version strategy

glab has been releasing since `v0.1.0` (July 2020), but the early versions had a very different command surface and help format. Recommended minimum version: **`1.47.0`** — this is the earliest version whose release includes `glab_{version}_linux_amd64.tar.gz` assets on GitLab. Versions before 1.47.0 used a different release asset layout and return 404.

Older versions can be added later if needed by adjusting `--min-version`.

---

## Known considerations

### Authentication in containers

glab normally requires `GITLAB_TOKEN` for authenticated commands. During scraping, we only invoke `glab <command> --help` which does **not** require authentication. The scrape containers need no credentials.

### Archive structure

The `glab_{version}_linux_amd64.tar.gz` archive extracts to `bin/glab`. The install script handles this:
```
tar xzf /tmp/glab.tar.gz -C /tmp && mv /tmp/bin/glab /usr/local/bin/glab
```

### Help flag

glab uses `--help` (or `-h`) as the help flag, which is the default for the `UseScraper` middleware. No custom `helpFlag` override is needed.

---

## Related projects

| Project | Description |
|---------|-------------|
| [BinaryWrapper](../BinaryWrapper/) | Core infrastructure — source generator, runtime, design pipeline |
| [Podman](../Podman/) | Podman wrapper (Cobra parser, GitHub releases) |
| [Vagrant](../Vagrant/) | Vagrant wrapper (custom parser, HashiCorp releases) |
| [Builder](../Builder/) | `AbstractBuilder<T>` and builder source generator |
| [Result](../Result/) | `Result<T>` monad for error handling |

## Shared dependency and version images

The Design runner prepares the shared system dependencies once, then installs each
software version in an image derived from that base. `UseVersionImage().UseContainer()`
builds or reuses the image before collecting help. After collection, the container
and version image are removed; `--keep-images` retains the version image. The
shared base remains cached.

From the wrapper directory, with Podman running (or add `--runtime docker`):

```powershell
$design = './src/FrenchExDev.Net.GitLab.Cli.Design/FrenchExDev.Net.GitLab.Cli.Design.csproj'
dotnet run --project $design --framework net10.0 -- --help
dotnet run --project $design --framework net10.0 -- --build-base
dotnet run --project $design --framework net10.0 -- --list --missing
# Review the selection; optionally narrow it with --min-version.
dotnet run --project $design --framework net10.0 -- --build-images --missing --parallel 2
dotnet run --project $design --framework net10.0 -- --missing --parallel 2
dotnet run --project $design --framework net10.0 -- --reparse
dotnet run --project $design --framework net10.0 -- --clean-images
```

`--build-base` prepares only dependencies. `--build-images` installs selected versions
without scraping and keeps their images. `--clean-images` removes this wrapper's
version images before its base, without forcing removal. Base preparation and cleanup
do not query the version collector; `--list` and `--reparse` do not build images.
With `--missing`, selection is based on missing JSON files.

The three image operations are mutually exclusive and cannot be combined with
`--reparse` or known-missing management. `--build-base` and `--clean-images` also
reject `--list` and `--missing`.

The PowerShell launcher exposes `-BuildBase`, `-BuildImages`, `-CleanImages`,
`-KeepImages`, `-Reparse`, `-Missing`, `-List`, `-MinVersion`, `-Parallel`,
`-ScrapeParallel`, `-Runtime`, `-Output` and `-Framework`. It resolves its project
relative to the script, so it also works from another directory:

```powershell
./scripts/Find-Missing.ps1 -BuildBase -Framework net10.0
./scripts/Find-Missing.ps1 -BuildImages -Missing -Parallel 2 -Framework net10.0
```

See the [BinaryWrapper image pipeline guide](../BinaryWrapper/doc/UPGRADE-IMAGE-PIPELINES.md)
for each client's dependencies, cache identities, build logs, reuse and cleanup.
