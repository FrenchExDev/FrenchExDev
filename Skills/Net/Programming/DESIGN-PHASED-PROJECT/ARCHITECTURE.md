# DESIGN-PHASED-PROJECT — Architecture

## 3-Phase Diagram

```
Phase 1: DESIGN (.Design CLI)
  |-- IVersionCollector --> version list
  |-- DesignPipelineRunner --> parallel execution per version
  |     |-- UseImageBuild --> container image
  |     |-- UseContainer --> running container
  |     |-- UseScraper + IHelpParser --> command tree JSON
  |     +-- finally: cleanup containers/images
  |-- Output: scrape/{tool}-{version}.json (checked into git)

Phase 2: ATTRIBUTES (.Attributes)
  |-- [BinaryWrapper("tool")] descriptor class
  |-- [Builder], [ComposeBundle], etc.

Phase 3: SOURCE GENERATION (.SourceGenerator + Runtime)
  |-- Generator reads JSON (AdditionalFiles) + attributes
  |-- Emits: command classes, builder classes, typed client
  |-- Runtime: AbstractBuilder<T>, CommandExecutor, etc.
```

## DesignPipeline Middleware Stack

| Middleware | Purpose | Used By |
|-----------|---------|---------|
| `UseImageBuild` | Build container image (Dockerfile or inline) | Vagrant, Podman |
| `UseContainer` | Run container, execute commands inside it | Vagrant, Podman, glab |
| `UseScraper` | Recursively scrape `--help` via `IHelpParser` | All binary wrappers |
| `UseHttpDownload` | Download files from HTTP (schemas, specs) | DockerCompose, Packer |
| `UseContentTransform` | Parse/transform downloaded content | DockerCompose, Packer |
| `UseSave` | Write output to disk | DockerCompose, Packer |

See: `BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper.Design.Lib/DesignPipelineExtensions.cs`

## VersionContext (Per-Version State Bag)

Each version scrape receives a `VersionContext` containing:
- `Version` — the version being scraped
- `ImageTag` — container image tag (if applicable)
- `ContainerId` — running container ID (if applicable)
- `RunHelp` delegate — function to execute `--help` in the container
- `OutputDir` — where to write JSON output

## DesignPipelineRunner (Orchestrator)

Coordinates multi-version scraping:
1. Collects versions via `IVersionCollector`
2. Filters by `--missing` (only unscraped versions) or explicit version list
3. Runs pipeline per version (parallel with `--parallel`)
4. Handles crashes with retry/skip logic
5. Cleanup in `finally` block

See: `BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper.Design.Lib/DesignPipelineRunner.cs`

## IVersionCollector Implementations

| Collector | Source | Used By |
|-----------|--------|---------|
| `GitHubReleasesVersionCollector` | GitHub Releases API | Podman, DockerCompose |
| `GitLabReleasesVersionCollector` | GitLab API v4 (custom) | glab |
| `VagrantVersionCollector` | HashiCorp releases API | Vagrant |

Reuse `GitHubReleasesVersionCollector` when the tool has GitHub releases. Create custom collectors only for non-GitHub sources.

## IHelpParser Implementations

| Parser | Format | Used By |
|--------|--------|---------|
| `VagrantHelpParser` | "Common commands:" / "Available subcommands:" headers | Vagrant |
| `PodmanHelpParser` | Cobra-aware, recognizes type hints (`string`, `int`, `uint`, `stringArray`) | Podman |
| `GlabHelpParser` | ALL-CAPS headers (COMMANDS, FLAGS, USAGE), glab's custom Cobra template | glab |
| `Hcl2SpecParser` | HCL2 Go source files (field extraction) | Packer |

Each tool has unique `--help` output format. Never assume a generic parser will work.

## Instance Catalog

| Project | Tool | Versions | Collector | Parser | Output Dir |
|---------|------|----------|-----------|--------|-----------|
| Vagrant | `vagrant` | 55 | `VagrantVersionCollector` | `VagrantHelpParser` | `Vagrant/src/.../scrape/` |
| Podman | `podman` | 55 | `GitHubReleasesVersionCollector` | `PodmanHelpParser` | `Podman/src/.../scrape/` |
| GitLab.Cli | `glab` | varies | `GitLabReleasesVersionCollector` | `GlabHelpParser` | `GitLab.Cli/src/.../scrape/` |
| Packer | `packer` | varies | `GitHubReleasesVersionCollector` | `Hcl2SpecParser` | `Packer/src/.../scrape/` |
| DockerCompose | compose-spec | 32 | `GitHubReleasesVersionCollector` | JSON schema parser | `DockerCompose/src/.../schemas/` |

## Key Files

- `BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper.Design.Lib/DesignPipeline.cs` — middleware pipeline
- `BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper.Design.Lib/DesignPipelineRunner.cs` — orchestrator
- `BinaryWrapper/src/FrenchExDev.Net.BinaryWrapper.Design.Lib/DesignPipelineExtensions.cs` — middleware methods
- `Vagrant/src/FrenchExDev.Net.Vagrant.Design/Program.cs` — Vagrant design entry point
- `Podman/src/FrenchExDev.Net.Podman.Design/Program.cs` — Podman design entry point
- `GitLab.Cli/src/FrenchExDev.Net.GitLab.Cli.Design/Program.cs` — glab design entry point
- `DockerCompose/src/FrenchExDev.Net.DockerCompose.Bundle.Design/Program.cs` — DockerCompose design entry point
