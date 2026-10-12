# Architecture

## Overview

The Docker Compose wrapper is built on the BinaryWrapper framework -- a source-generator-driven system for wrapping CLI binaries. The framework handles the mechanical work (command classes, builders, client API, version gating) while the Docker Compose project provides the two-phase scraping pipeline and leverages the shared `CobraHelpParser` from `BinaryWrapper.Design`.

```
                      Design-time                          Runtime
                 +---------------------+           +--------------------+
docker-compose   |  Design Tool        |           |  DockerCompose     |
  --help ----->  |  +-------------+    |           |  Client (generated)|
  (in container) |  | CobraParser |    |  JSON     |                    |
                 |  | + Scraper   |----+------>    |  +~~~~~~~~~~~~~~+  |
                 |  +-------------+    |  files    |  | Command      |  |
                 +---------------------+           |  | Builder      |  |
                                                   |  | Client       |  |
                 +---------------------+           |  +~~~~~~+~~~~~~~+  |
                 |  Source Generator   |           |         |          |
                 |  (BinaryWrapper SG) |           |  +------v-------+  |
                 |  reads JSON --> C#  |           |  | Executor     |  |
                 +---------------------+           |  | Parser       |  |
                                                   |  | Collector    |  |
                                                   |  +--------------+  |
                                                   +--------------------+
```

## Project structure

```
DockerCompose/
+-- FrenchExDev.Net.DockerCompose.slnx
+-- doc/
|   +-- ARCHITECTURE.md            # This file
|   +-- HOW-TO.md                  # Usage and scraping guide
|   +-- PHILOSOPHY.md              # Design rationale
+-- src/
|   +-- FrenchExDev.Net.DockerCompose/              # Main library
|   |   +-- FrenchExDev.Net.DockerCompose.csproj
|   |   +-- DockerComposeDescriptor.cs              # [BinaryWrapper("docker-compose")] trigger
|   |   +-- scrape/
|   |       +-- docker-compose-2.20.0.json
|   |       +-- ...
|   |       +-- docker-compose-5.1.0.json           # 57 version files
|   +-- FrenchExDev.Net.DockerCompose.Design/       # Scraping tool
|       +-- FrenchExDev.Net.DockerCompose.Design.csproj
|       +-- Program.cs                              # Two-phase pipelined scraper
+-- test/
    +-- FrenchExDev.Net.DockerCompose.Tests/        # xUnit + CsCheck + Shouldly
        +-- FrenchExDev.Net.DockerCompose.Tests.csproj
        +-- DescriptorTests.cs
        +-- coverage.runsettings
```

Note: There is no custom help parser in the Design project. Docker Compose uses Go's cobra CLI framework, and the shared `CobraHelpParser` from `BinaryWrapper.Design` handles it via `.UseParser("cobra")`.

## Source generator pipeline

The BinaryWrapper source generator (`FrenchExDev.Net.BinaryWrapper.SourceGenerator`) is an incremental Roslyn generator that runs at compile time:

### 1. Discovery

The generator finds classes annotated with `[BinaryWrapper("docker-compose")]` -- in this case `DockerComposeDescriptor`. This triggers code generation.

### 2. JSON ingestion

The `.csproj` registers scrape files as `AdditionalFiles`:

```xml
<AdditionalFiles Include="scrape\docker-compose-*.json" />
```

The generator reads all 57 matching files and parses them into `CommandTreeModel` objects -- one per version.

### 3. Version differencing

When multiple JSON files exist (57 versions for Docker Compose), `VersionDiffer` merges them into a single unified tree. Each command and option is annotated with the version range in which it exists:

- `[SinceVersion("2.20.0")]` -- available from this version onward
- `[UntilVersion("2.23.3")]` -- removed after this version

This is critical for Docker Compose, which has evolved significantly -- commands like `watch`, `scale`, `attach`, `stats`, `export`, `commit`, `publish`, and `volumes` were all added across different versions.

### 4. Code emission

Three emitters produce the generated C#:

**CommandClassEmitter** -- For each leaf command in the tree:

```csharp
[SinceVersion("2.20.0")]
public sealed partial class DockerComposeUpCommand : ICliCommand
{
    public bool? Detach { get; init; }
    public bool? Build { get; init; }
    public IReadOnlyList<string>? Scale { get; init; }
    // ...
    public IReadOnlyList<string> CommandPath => new[] { "up" };
    public IReadOnlyList<string> ToArguments() { /* serializes non-null props */ }
}
```

**BuilderClassEmitter** -- A fluent builder extending `AbstractBuilder<T>`:

```csharp
public partial class DockerComposeUpCommandBuilder : AbstractBuilder<DockerComposeUpCommand>
{
    protected bool? Detach { get; private set; }

    public DockerComposeUpCommandBuilder WithDetach(bool? value) { ... return this; }
    protected virtual IEnumerable<Exception>? ValidateDetach(bool? value) => null;

    protected override Task<Result<ValidationResult>> ValidateAsync(...) { /* calls all Validate*() */ }
    protected sealed override Task<...> Instantiate(...) { /* creates command from props */ }
}
```

**ClientClassEmitter** -- The public API surface:

```csharp
public static partial class DockerCompose
{
    public static DockerComposeClient Create(BinaryBinding binding) => new(binding);
}

public partial class DockerComposeClient
{
    // Top-level commands
    public DockerComposeUpCommand Up(Action<DockerComposeUpCommandBuilder> configure) { ... }
    public DockerComposeDownCommand Down(Action<DockerComposeDownCommandBuilder> configure) { ... }
    public DockerComposeBuildCommand Build(Action<DockerComposeBuildCommandBuilder> configure) { ... }
    public DockerComposePsCommand Ps(Action<DockerComposePsCommandBuilder> configure) { ... }

    // Nested command group
    public DockerComposeClientBridgeGroup Bridge => new(this);
}
```

## Generated code statistics

- **76 generated source files** total
- **37 command classes** (sealed `ICliCommand` implementations)
- **37 builder classes** (`AbstractBuilder<T>` with fluent `With*()` API)
- **1 client class** with 1 nested command group (`Bridge` with `Transformations` sub-group)
- **1 descriptor** (`DockerComposeDescriptor.BinaryWrapper.g.cs`)

## Two-phase pipelined scraping

The Docker Compose scraper uses a pipelined Channel-based architecture where Phase 1 and Phase 2 overlap:

```
Phase 1: Build Images (producers)
  For each version (parallel with --parallel workers):
    alpine:3.19 + curl
      -> download docker-compose-linux-x86_64 (raw binary, no tarball)
      -> install to /usr/local/bin/docker-compose
      -> podman commit -> docker-compose-scrape:{version}
      -> publish version to Channel

Phase 2: Scrape (consumers)
  Read from Channel as versions become ready:
    podman run -d docker-compose-scrape:{version} sleep infinity
      -> podman exec ... docker-compose <cmd> --help
      -> CobraHelpParser -> JSON
      -> cleanup container
      -> cleanup image (eager -- immediately after scrape)

Finally (crash recovery only):
  -> cleanup straggler containers
  -> cleanup straggler images
```

### Key difference from other wrappers: eager cleanup

Unlike the Podman and Docker wrappers that accumulate all images until the end, the Docker Compose scraper deletes each version's image immediately after its scrape completes. This keeps disk usage proportional to `--parallel` count (typically 4 images at once), not total version count (57+ images).

### Binary download format

Docker Compose V2 publishes standalone binaries (not tarballs) on GitHub releases:

- Asset name: `docker-compose-linux-x86_64`
- URL: `https://github.com/docker/compose/releases/download/v{version}/docker-compose-linux-x86_64`
- Install: `curl -fsSL ... -o /usr/local/bin/docker-compose && chmod +x /usr/local/bin/docker-compose`

This is simpler than Podman (tarball) or Docker (tarball with nested directory structure).

## Help parsing -- CobraHelpParser (shared)

Docker Compose uses Go's cobra CLI framework, producing the same help format as Docker and Podman. Rather than maintaining a separate parser, the wrapper uses the shared `CobraHelpParser` from `BinaryWrapper.Design`:

```csharp
// In Program.cs
var pipeline = new ScrapePipeline()
    .Binary("docker-compose")
    .UseParser("cobra")   // declarative strategy resolution
    // ...
```

### Cobra help format

```
Define and run multi-container applications with Docker

Usage:
  docker compose [OPTIONS] COMMAND

Available Commands:
  attach      Attach local standard input, output, and error streams to a service
  build       Build or rebuild services
  up          Create and start containers

Flags:
  -f, --file stringArray           Compose configuration files
      --parallel int               Control max parallelism, -1 for unlimited (default -1)
      --progress string            Set type of progress output (auto, tty, plain, json, quiet)
  -p, --project-name string        Project name
```

### Cobra type mapping

| Cobra Type | OptionValueKind | CLR Type |
|-----------|----------------|----------|
| (none) | `Flag` | `bool` |
| `string` | `Single` | `string` |
| `int`, `int64`, `uint` | `Single` | `integer` |
| `float`, `float64` | `Single` | `string` |
| `duration` | `Single` | `string` |
| `strings`, `stringArray`, `stringSlice` | `Multiple` | `string` |
| `intSlice`, `uintSlice` | `Multiple` | `string` |
| UPPERCASE token (e.g., `ARCH`) | `Single` | `string` |

### Skipped commands

Two commands are excluded from scraping:

- `help` -- echoes root help (infinite recursion)
- `completion` -- generates shell completions (not useful for wrapping)

## Runtime serialization

Docker Compose uses GNU-style CLI conventions (the default for `[BinaryWrapper]`):

| Feature | Docker Compose Convention |
|---------|--------------------------|
| Flag prefix | `--` (double dash) |
| Value format | `--flag value` (space-separated) |
| Boolean format | `--flag` (presence = true, absence = false) |
| Multiple values | repeated flags (`--file a.yml --file b.yml`) |
| Short flags | `-f` (single dash, single char) |

## Command tree

```
DockerComposeClient
+-- attach, build, commit, config, cp, create, down, events, exec,
|   export, images, kill, logs, ls, pause, port, ps, publish, pull,
|   push, restart, rm, run, scale, start, stats, stop, top, unpause,
|   up, version, volumes, wait, watch
+-- Bridge/
    +-- Convert
    +-- Transformations/
        +-- Create, List
```

### Command availability by version

| Command | Since |
|---------|-------|
| `build`, `config`, `cp`, `create`, `down`, `events`, `exec`, `images`, `kill`, `logs`, `ls`, `pause`, `port`, `ps`, `pull`, `push`, `restart`, `rm`, `run`, `start`, `stop`, `top`, `unpause`, `up`, `version`, `wait` | 2.20.0 |
| `scale`, `watch` | 2.22.0 |
| `attach`, `stats` | 2.24.0 |
| `export` | 2.30.1 |
| `commit` | 2.31.0 |
| `publish` | 2.34.0 |
| `volumes` | 2.38.0 |
| `bridge convert`, `bridge transformations create/list` | 5.0.0 |

## Version coverage

- **57 versions scraped**: 2.20.0 through 5.1.0 (includes V5 major version)
- **No broken versions**: all published releases have working standalone binaries
- **Version range**: `[SinceVersion("2.20.0")]` through latest

## Dependencies

The Docker Compose library depends on four sibling FrenchExDev.Net packages:

```
FrenchExDev.Net.DockerCompose
+-- FrenchExDev.Net.BinaryWrapper             Core: ICliCommand, CommandExecutor, IOutputParser
+-- FrenchExDev.Net.BinaryWrapper.Attributes  [BinaryWrapper] attribute
+-- FrenchExDev.Net.BinaryWrapper.SourceGenerator  (Analyzer, no runtime ref)
+-- FrenchExDev.Net.Builder                   AbstractBuilder<T>, fluent builders
+-- FrenchExDev.Net.Result                    Result<T>, Result<T,TError>
```

The Design tool additionally depends on:
- `FrenchExDev.Net.BinaryWrapper.Design` -- scraping framework, `ScrapePipeline`, `CobraHelpParser`, `HelpParsers` registry
- `Microsoft.Extensions.Logging` / `.Console`
