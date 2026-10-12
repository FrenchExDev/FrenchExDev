# BinaryWrapper Consumer Comparison

## All Consumers

| Feature | Docker | DockerCompose | Podman | PodmanCompose | Packer | Vagrant |
|---------|--------|---------------|--------|---------------|--------|---------|
| **Binary** | `docker` | `docker-compose` | `podman` | `podman-compose` | `packer` | `vagrant` |
| **CLI framework** | Go (cobra) | Go (cobra) | Go (cobra) | Python (argparse) | Go (HashiCorp) | Ruby |
| **Flag style** | `--flag value` | `--flag value` | `--flag value` | `--flag value` | `-flag=value` | `--flag VALUE` |
| **Boolean style** | `--flag` (presence) | `--flag` (presence) | `--flag` (presence) | `--flag` (presence) | `-flag=true/false` | `--flag` (presence) |
| **Descriptor attribute** | `[BinaryWrapper("docker")]` | `[BinaryWrapper("docker-compose")]` | `[BinaryWrapper("podman")]` | `[BinaryWrapper("podman-compose")]` | `[BinaryWrapper("packer", FlagPrefix="-", FlagValueSeparator="=", UseBoolEqualsFormat=true)]` | `[BinaryWrapper("vagrant")]` |
| **Help parser** | `CobraHelpParser` | `CobraHelpParser` | `CobraHelpParser` | `ArgparseHelpParser` | `PackerHelpParser` | `VagrantHelpParser` + `LoggingHelpParser` |
| **Help flag** | `--help` | `--help` | `--help` | `--help` | `-h` | `-h` |
| **Version collector** | `GitHubTagsVersionCollector("docker","cli")` | `GitHubReleasesVersionCollector("docker","compose")` | `GitHubReleasesVersionCollector("containers","podman")` | `GitHubReleasesVersionCollector("containers","podman-compose")` | `PackerVersionCollector` (custom) | `VagrantVersionCollector` (custom) |
| **Base image** | `alpine:3.19` | `alpine:3.19` | `alpine:3.19` | `alpine:3.19` | `alpine:3.19` | `debian:bookworm` |
| **Install method** | Static binary from download.docker.com | Static binary from GitHub releases | Static tar.gz from GitHub releases | `pip install` | ZIP from HashiCorp releases | `.deb` from HashiCorp + WSL patch |
| **Pipeline** | UseImageBuild + UseContainer | UseImageBuild + UseContainer | UseImageBuild + UseContainer | UseImageBuild + UseContainer | UseInlineContainer | UseImageBuild + UseContainer (shell: bash) |
| **Min version** | 23.0.0 | 2.20.0 | 4.1.0 | 1.0.0 | — | — |
| **JSON files** | 128 | 71 | 55 | 6 | 96 | 56 |
| **Help cache** | 51 versions | — | Yes | — | — | Yes |
| **Output parsers** | — | — | — | — | `PackerBuildParser` | `VagrantOutputParser` |
| **Event types** | — | — | — | — | `PackerEvent` (7 types) | `VagrantEvent` (6 types) |
| **Result collectors** | — | — | — | — | `PackerBuildCollector` | `VagrantUpCollector` |
| **Known missing** | 20.10.25-27, 23.0.7-10, etc. | — | 4.1.0, 4.3.0 | — | 0.1.0-0.12.3 | 2.0.3-2.3.5 |

## Help Parser Comparison

Five parsers handle different CLI frameworks:

| Feature | StandardHelpParser | CobraHelpParser | ArgparseHelpParser | PackerHelpParser | VagrantHelpParser |
|---------|-------------------|-----------------|-------------------|-----------------|-------------------|
| **Used by** | (base/fallback) | Docker, DockerCompose, Podman | PodmanCompose | Packer | Vagrant |
| **Section headers** | `Options:`, `Commands:` | `Available Commands:`, `Flags:`, `Global Flags:` | `positional arguments:`, `options:` | Packer-specific | `Common commands:`, `Available subcommands:` |
| **Type hints** | None | `string`, `int`, `uint`, `stringArray`, etc. | None | None | None |
| **Value detection** | UPPERCASE / `<bracketed>` | Cobra type annotations | `-V VALUE` pattern | Packer-specific rules | Section-based |
| **Multi-value** | Via `UPPERCASE...` | Via `strings`, `stringSlice`, etc. | `nargs` patterns | Via repeated flags | N/A |
| **Skip list** | None | `help`, `completion` | `help` | None | `help`, `list-commands`, `serve` |
| **Logging wrapper** | — | — | — | — | `LoggingHelpParser` decorator |

## Pipeline Comparison

All consumers use the `DesignPipeline` middleware composition from `Design.Lib`. Two patterns exist:

### UseImageBuild + UseContainer (Docker, DockerCompose, Podman, PodmanCompose, Vagrant)

```
UseImageBuild: build & cache image per version
  └─ UseContainer: start container from cached image
       └─ UseScraper: scrape help text → JSON
```

Each middleware owns its resource lifecycle:
- `UseImageBuild` creates the image, removes it in `finally` after inner pipeline completes
- `UseContainer` starts the container, sets `ctx.RunHelp` to exec inside it, removes it in `finally`
- `UseScraper` calls `ctx.RunHelp` to fetch help text, writes JSON output

Faster for parallel scraping since Phase 2 (UseContainer + UseScraper) starts from pre-built images. Better for binaries with slow or complex installation.

### UseInlineContainer (Packer)

```
UseInlineContainer: create container + install binary inline
  └─ UseScraper: scrape help text → JSON
```

Simpler single-step: creates a container, installs the binary, sets `ctx.RunHelp`, then passes to scraper. No cached image. Good when installation is fast (e.g., downloading a single ZIP).

### --reparse mode (all consumers)

Both patterns support `--reparse` via a separate `ReparsePipeline`:

```
UseCachedHelp: read previously-dumped .help.txt files from disk
  └─ UseScraper: parse cached help text → JSON
```

`UseCachedHelp` sets `ctx.RunHelp` to a file-reading callback (no container needed). `ctx.HelpDumpDir` is left null so `UseScraper` skips file dumping. This enables re-scraping with updated parsers without re-downloading binaries.

## Event-Driven Consumption Status

The BinaryWrapper runtime supports three consumption modes for command execution:

| Mode | API | Description |
|------|-----|-------------|
| **Raw** | `ExecuteAsync()` → `CommandResult` | Simple execution, returns exit code + stdout/stderr |
| **Streaming** | `ExecuteAsync<TEvent>(parser)` → `CommandExecution<TEvent>` | Events via `IAsyncEnumerable<TEvent>`, dual-consumption handle |
| **Collected** | `ExecuteAsync<TEvent, TResult>(parser, collector)` → `CommandExecution<TEvent>` | Events streamed + final `TResult` via collector |

### Consumer implementation status

| Consumer | Output Parser | Event Types | Result Collector | Status |
|----------|--------------|-------------|-----------------|--------|
| **Packer** | `PackerBuildParser` | `PackerEvent` (7 types: Ui, Artifact, Error, etc.) | `PackerBuildCollector` → `PackerBuildResult` | Complete |
| **Vagrant** | `VagrantOutputParser` | `VagrantEvent` (6 types: Action, Detail, Progress, etc.) | `VagrantUpCollector` → `VagrantUpResult` | Complete |
| **Docker** | — | — | — | Pending |
| **DockerCompose** | — | — | — | Pending |
| **Podman** | — | — | — | Pending |
| **PodmanCompose** | — | — | — | Pending |

## When to Use Which Approach

| Scenario | Recommended |
|----------|-------------|
| Few versions, fast install | `UseInlineContainer` (Packer-style) |
| Many versions, slow install | `UseImageBuild` + `UseContainer` |
| Standard `--help` format | `StandardHelpParser` |
| Go/cobra CLI | `CobraHelpParser` |
| Python/argparse CLI | `ArgparseHelpParser` |
| Custom help format | Write a custom `IHelpParser` |
| Re-scrape with updated parser | `--reparse` with `UseCachedHelp` pipeline |
| Tool with machine-readable output | Add `IOutputParser<TEvent>` + `IResultCollector<TEvent, TResult>` |
