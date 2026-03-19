# Architecture

## Overview

The GitLab CLI wrapper is built on the BinaryWrapper framework -- a source-generator-driven system for wrapping CLI binaries. The framework handles the mechanical work (command classes, builders, client API, version gating) while the GitLab CLI project provides domain-specific parsing via a custom Cobra-aware help parser and GitLab API v4 integration for version discovery.

```
                     Design-time                          Runtime
                ┌──────────────────────┐           ┌────────────────────┐
 glab -h ──>    │  Design Tool         │           │  GlabClient        │
 (in glab)      │  ┌─────────────┐     │           │  (generated)       │
                │  │ HelpParser  │     │  JSON     │                    │
                │  │ + Scraper   │─────┼──────>    │  ┌──────────────┐  │
                │  └─────────────┘     │  files    │  │ Command      │  │
                └──────────────────────┘           │  │ Builder      │  │
                                                   │  │ Client       │  │
                ┌──────────────────────┐           │  └──────┬───────┘  │
                │  Version Discovery   │           │         │          │
                │  (GitLab API v4)     │           │  ┌──────▼───────┐  │
                │  list releases ──────┤           │  │ Executor     │  │
                │                      │           │  │ Parser       │  │
                └──────────────────────┘           │  │ Collector    │  │
                                                   │  └──────────────┘  │
                ┌──────────────────────┐           │                    │
                │  Source Generator    │           │  (runtime config)  │
                │  (BinaryWrapper SG)  │           │                    │
                │  reads JSON ──> C#   │           └────────────────────┘
                └──────────────────────┘
```

## Project structure

```
GitLab.Cli/
├── FrenchExDev.Net.GitLab.Cli.slnx
├── doc/
│   ├── ARCHITECTURE.md            # This file
│   └── HOW-TO.md                  # Usage and scraping guide
├── src/
│   ├── FrenchExDev.Net.GitLab.Cli/              # Main library
│   │   ├── FrenchExDev.Net.GitLab.Cli.csproj
│   │   ├── GlabDescriptor.cs                    # [BinaryWrapper("glab")] trigger
│   │   └── scrape/
│   │       ├── glab-1.47.0.json
│   │       ├── ...
│   │       └── glab-{latest}.json               # Version files
│   └── FrenchExDev.Net.GitLab.Cli.Design/       # Scraping tool
│       ├── FrenchExDev.Net.GitLab.Cli.Design.csproj
│       ├── Program.cs                           # Single-phase scraping pipeline
│       ├── GitLabReleasesVersionCollector.cs    # GitLab API v4 integration
│       └── GlabHelpParser.cs                    # Cobra parser for glab's format
```

## Source generator pipeline

The BinaryWrapper source generator (`FrenchExDev.Net.BinaryWrapper.SourceGenerator`) is an incremental Roslyn generator that runs at compile time:

### 1. Discovery

The generator finds classes annotated with `[BinaryWrapper("glab")]` -- in this case `GlabDescriptor`. This triggers code generation.

### 2. JSON ingestion

The `.csproj` registers scrape files as `AdditionalFiles`:

```xml
<AdditionalFiles Include="scrape\glab-*.json" />
```

The generator reads all matching files and parses them into `CommandTreeModel` objects -- one per version.

### 3. Version differencing

When multiple JSON files exist, `VersionDiffer` merges them into a single unified tree. Each command and option is annotated with the version range in which it exists:

- `[SinceVersion("1.47.0")]` -- available from this version onward
- `[UntilVersion("1.50.0")]` -- removed after this version

### 4. Code emission

Three emitters produce the generated C#:

**CommandClassEmitter** -- For each leaf command in the tree:

```csharp
[SinceVersion("1.47.0")]
public sealed partial class GlabAuthLoginCommand : ICliCommand
{
    public string? Hostname { get; init; }
    public bool? Interactive { get; init; }
    public string? Token { get; init; }
    // ...
    public IReadOnlyList<string> CommandPath => new[] { "auth", "login" };
    public IReadOnlyList<string> ToArguments() { /* serializes non-null props */ }
}
```

**BuilderClassEmitter** -- A fluent builder extending `AbstractBuilder<T>`:

```csharp
public partial class GlabAuthLoginCommandBuilder : AbstractBuilder<GlabAuthLoginCommand>
{
    protected string? Hostname { get; private set; }

    public GlabAuthLoginCommandBuilder WithHostname(string? value) { ... return this; }
    protected virtual IEnumerable<Exception>? ValidateHostname(string? value) => null;

    protected override Task<Result<ValidationResult>> ValidateAsync(...) { /* calls all Validate*() */ }
    protected sealed override Task<...> Instantiate(...) { /* creates command from props */ }
}
```

**ClientClassEmitter** -- The public API surface:

```csharp
public static partial class Glab
{
    public static GlabClient Create(BinaryBinding binding) => new(binding);
}

public partial class GlabClient
{
    // Top-level commands
    public GlabAuthLoginCommand AuthLogin(Action<GlabAuthLoginCommandBuilder> configure) { ... }
    public GlabVersionCommand Version(Action<GlabVersionCommandBuilder> configure) { ... }

    // Nested command groups
    public GlabClientAuthGroup Auth => new(this);
    public GlabClientPrGroup Pr => new(this);
    public GlabClientIssueGroup Issue => new(this);
    public GlabClientRepoGroup Repo => new(this);
    // ...
}
```

## Generated code statistics

Generated code varies depending on the glab version. As of the latest scrapes:

- **Multiple generated source files**
- **Command classes** (sealed `ICliCommand` implementations)
- **Builder classes** (`AbstractBuilder<T>` with fluent `With*()` API)
- **1 client class** with nested command groups

## Single-phase scraping pipeline

Unlike Podman (two-phase) or Vagrant (two-phase), the glab scraper uses a single-phase pipeline for simplicity:

```
Phase 1: Download and Scrape
  For each version:
    alpine:3.19 + curl + tar
      → download glab_{version}_linux_amd64.tar.gz from GitLab releases
      → extract and install to /usr/local/bin/glab
      → glab exec ... glab <cmd> --help
      → GlabHelpParser → JSON
      → cleanup container

Finally:
  → cleanup all containers
```

The scraper builds a temporary image per version, extracts and scrapes immediately, then removes the container. This is simpler than Podman's two-phase approach but slightly slower for batch operations.

## GlabHelpParser

Glab uses Go's Cobra CLI framework with a heavily customized help output that has evolved across versions:

### Glab help format variations

**Older versions (≤~1.80):**
```
CORE COMMANDS:
  alias:                Create, list, and delete aliases.
  config:               Manage configuration settings.

FLAGS:
  -v, --version         Show glab version information.
      --help            Show help for this command.
```

**Newer versions (≥~1.81):**
```
COMMANDS:
  alias [command] [--flags]    Create, list, and delete aliases.
  config [command] [--flags]   Manage configuration settings.

FLAGS:
  -v --version                 Show glab version information.
      --help                   Show help for this command.
```

Key differences from standard help parsers:
- **ALL-CAPS section headers** ("COMMANDS", "FLAGS", "EXAMPLES", "ALIASES", "INHERITED FLAGS")
- **Variable command format** -- older uses `name:`, newer uses `name [args]`
- **Flag separator changed** -- older comma-separated (`-v, --version`), newer space-separated (`-v --version`)
- **No type hints** -- unlike Cobra's stdlibPodman parser with `string`, `int` hints, glab has heuristic value detection

### Value kind detection heuristics

| Pattern | Detection | ValueKind | CLR Type |
|---------|-----------|-----------|----------|
| No suffix | No flags in description | `Flag` | `bool` |
| `(default_value)` | Trailing parentheses at line end | `Single` | `string` or `integer` |
| `<placeholder>` | Angle-bracketed tokens in description | `Single` | `string` |
| "comma-separated" or "multiple" | Phrases in description | `Multiple` | `string` |

### Skipped commands

Three commands are excluded from scraping:

- `help` -- echoes root help (infinite recursion)
- `completion` -- generates shell completions (not useful for wrapping)
- `check-update` -- checks for updates (network call, not useful for CLI wrapping)

## GitLab API v4 integration

Version discovery uses `GitLabReleasesVersionCollector`, which queries the GitLab API:

```
GET https://gitlab.com/api/v4/projects/gitlab-org%2Fcli/releases?per_page=100
```

Key features:
- **URL-encoded project path** -- `gitlab-org%2Fcli` (note the `/2F` encoding)
- **Pagination support** -- follows `Link: <url>; rel="next"` headers for multiple pages
- **Tag-to-version transformation** -- strips `v` prefix (glab uses `v1.47.0` tags)
- **Skips upcoming releases** -- ignores `upcoming_release: true` entries (pre-release equivalents)
- **Auth support** -- checks `GITLAB_TOKEN` env var for higher rate limits and private projects

### Rate limiting

- **Unauthenticated**: 300 requests/hour
- **Authenticated**: Up to 600 requests/hour with `GITLAB_TOKEN`

The `GITLAB_TOKEN` environment variable is checked and automatically injected as the `PRIVATE-TOKEN` header.

## Runtime serialization

GitLab CLI uses GNU-style CLI conventions (the default for `[BinaryWrapper]`):

| Feature | glab Convention |
|---------|-----------------|
| Flag prefix | `--` (double dash) |
| Value format | `--flag value` (space-separated) |
| Boolean format | `--flag` (presence = true, absence = false) |
| Multiple values | repeated flags (`--filter FOO --filter BAR`) |
| Short flags | `-f` (single dash, single char) |

This is configured by the bare `[BinaryWrapper("glab")]` attribute with no additional parameters (all defaults).

## Command group hierarchy

Glab's command structure is highly nested. Common groups include:

```
GlabClient
├── auth/              login, logout, refresh-token, status
├── pr/                create, list, view, close, merge, approval, approve, unapprove, ...
├── issue/             create, list, view, close, reopen, comment, label, ...
├── repo/              create, clone, fork, view, deploy-key, delete, ...
├── mr/                (merge request alias for pr)
├── snippet/           create, list, view, delete, comment, ...
├── release/           create, list, view, delete, update, download, ...
├── variable/          list, set, delete, get, ...
├── label/             create, list, view, delete, ...
├── user/              get, list, ...
├── project/           (admin/namespace) operations
├── group/             list, view, delete, ...
├── config/            init, get, set, delete, ...
└── completion         shell completion (skipped)
```

Exact groups depend on the glab version.

## Version coverage

Version support depends on available scrape files:

- **Minimum version**: `1.47.0` (earliest with `glab_{v}_linux_amd64.tar.gz` assets)
- **Pre-1.47.0**: Returns HTTP 404 on asset lookup, not scraped
- **Dynamic range**: New versions are automatically included when scraped

The `DefaultMinVersion` in `Program.cs` is set to `"1.47.0"`.

## Dependencies

The GitLab CLI library depends on four sibling FrenchExDev.Net packages:

```
FrenchExDev.Net.GitLab.Cli
├── FrenchExDev.Net.BinaryWrapper             Core: ICliCommand, CommandExecutor, IOutputParser
├── FrenchExDev.Net.BinaryWrapper.Attributes  [BinaryWrapper] attribute
├── FrenchExDev.Net.BinaryWrapper.SourceGenerator  (Analyzer, no runtime ref)
├── FrenchExDev.Net.Builder                   AbstractBuilder<T>, fluent builders
└── FrenchExDev.Net.Result                    Result<T>, Result<T,TError>
```

The Design tool additionally depends on:
- `FrenchExDev.Net.BinaryWrapper.Design` -- scraping framework, `DesignPipeline`, `DesignPipelineRunner`
- `FrenchExDev.Net.BinaryWrapper.Design.Lib` -- container orchestration
- `Microsoft.Extensions.Logging` / `.Console`

## Asset naming

GitLab releases glab binaries with cross-platform naming:

- **Linux amd64**: `glab_{version}_linux_amd64.tar.gz` (used in scraper)
- **Linux arm64**: `glab_{version}_linux_arm64.tar.gz`
- **Windows**: `glab_{version}_windows_amd64.zip`
- **macOS**: `glab_{version}_macOS_amd64.tar.gz`

The scraper only downloads the Linux amd64 variant for scraping inside Alpine containers.

## Known quirks

### No pre-built images

Unlike Podman (two-phase), glab scraper doesn't pre-build images for reuse. Each version scrapes immediately after download. This is acceptable since glab binaries are small and downloads are fast.

### Glab version discovery is case-sensitive

Project path must be URL-encoded exactly: `gitlab-org%2Fcli`. The API is case-sensitive for project paths.

### Help output inconsistency

Glab's help parser handles multiple help output formats (old and new). The parser is designed to be lenient and accept both. If help output changes significantly in future versions, the parser may need updates.
