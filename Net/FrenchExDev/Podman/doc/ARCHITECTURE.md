# Architecture

## Overview

The Podman wrapper is built on the BinaryWrapper framework -- a source-generator-driven system for wrapping CLI binaries. The framework handles the mechanical work (command classes, builders, client API, version gating) while the Podman project provides domain-specific parsing via a custom cobra-aware help parser and the two-phase scraping pipeline.

```
                      Design-time                          Runtime
                 ┌─────────────────────┐           ┌────────────────────┐
  podman -h ──>  │  Design Tool        │           │  PodmanClient      │
  (in podman)    │  ┌─────────────┐    │           │  (generated)       │
                 │  │ HelpParser  │    │  JSON     │                    │
                 │  │ + Scraper   │────┼──────>    │  ┌──────────────┐  │
                 │  └─────────────┘    │  files    │  │ Command      │  │
                 └─────────────────────┘           │  │ Builder      │  │
                                                   │  │ Client       │  │
                 ┌─────────────────────┐           │  └──────┬───────┘  │
                 │  Source Generator   │           │         │          │
                 │  (BinaryWrapper SG) │           │  ┌──────▼───────┐  │
                 │  reads JSON ──> C#  │           │  │ Executor     │  │
                 └─────────────────────┘           │  │ Parser       │  │
                                                   │  │ Collector    │  │
                                                   │  └──────────────┘  │
                                                   └────────────────────┘
```

## Project structure

```
Podman/
├── FrenchExDev.Net.Podman.slnx
├── doc/
│   ├── ARCHITECTURE.md            # This file
│   ├── HOW-TO.md                  # Usage and scraping guide
│   ├── COMPARISON-TABLE.md        # BinaryWrapper consumer comparison
│   └── PHILOSOPHY.md              # Design rationale
├── src/
│   ├── FrenchExDev.Net.Podman/              # Main library
│   │   ├── FrenchExDev.Net.Podman.csproj
│   │   ├── PodmanDescriptor.cs              # [BinaryWrapper("podman")] trigger
│   │   └── scrape/
│   │       ├── podman-4.1.1.json
│   │       ├── ...
│   │       └── podman-5.8.0.json            # 55 version files
│   └── FrenchExDev.Net.Podman.Design/       # Scraping tool
│       ├── FrenchExDev.Net.Podman.Design.csproj
│       ├── Program.cs                       # Two-phase scraping pipeline
│       └── PodmanHelpParser.cs              # Cobra-aware help parser
└── test/
    └── FrenchExDev.Net.Podman.Tests/        # xUnit + CsCheck + Shouldly
        ├── FrenchExDev.Net.Podman.Tests.csproj
        ├── PodmanTests.cs                   # 62 tests, 100% coverage
        └── coverage.runsettings
```

## Source generator pipeline

The BinaryWrapper source generator (`FrenchExDev.Net.BinaryWrapper.SourceGenerator`) is an incremental Roslyn generator that runs at compile time:

### 1. Discovery

The generator finds classes annotated with `[BinaryWrapper("podman")]` -- in this case `PodmanDescriptor`. This triggers code generation.

### 2. JSON ingestion

The `.csproj` registers scrape files as `AdditionalFiles`:

```xml
<AdditionalFiles Include="scrape\podman-*.json" />
```

The generator reads all 55 matching files and parses them into `CommandTreeModel` objects -- one per version.

### 3. Version differencing

When multiple JSON files exist (55 versions for Podman), `VersionDiffer` merges them into a single unified tree. Each command and option is annotated with the version range in which it exists:

- `[SinceVersion("4.1.1")]` -- available from this version onward
- `[UntilVersion("4.3.1")]` -- removed after this version (e.g., `--dns-opt` replaced by `--dns-option`)

### 4. Code emission

Three emitters produce the generated C#:

**CommandClassEmitter** -- For each leaf command in the tree:

```csharp
[SinceVersion("4.1.1")]
public sealed partial class PodmanRunCommand : ICliCommand
{
    public bool? Detach { get; init; }
    public string? Name { get; init; }
    public IReadOnlyList<string>? Volume { get; init; }
    // ...
    public IReadOnlyList<string> CommandPath => new[] { "run" };
    public IReadOnlyList<string> ToArguments() { /* serializes non-null props */ }
}
```

**BuilderClassEmitter** -- A fluent builder extending `AbstractBuilder<T>`:

```csharp
public partial class PodmanRunCommandBuilder : AbstractBuilder<PodmanRunCommand>
{
    protected bool? Detach { get; private set; }

    public PodmanRunCommandBuilder WithDetach(bool? value) { ... return this; }
    protected virtual IEnumerable<Exception>? ValidateDetach(bool? value) => null;

    protected override Task<Result<ValidationResult>> ValidateAsync(...) { /* calls all Validate*() */ }
    protected sealed override Task<...> Instantiate(...) { /* creates command from props */ }
}
```

**ClientClassEmitter** -- The public API surface:

```csharp
public static partial class Podman
{
    public static PodmanClient Create(BinaryBinding binding) => new(binding);
}

public partial class PodmanClient
{
    // Top-level commands
    public PodmanRunCommand Run(Action<PodmanRunCommandBuilder> configure) { ... }
    public PodmanPsCommand Ps(Action<PodmanPsCommandBuilder> configure) { ... }

    // Nested command groups
    public PodmanClientContainerGroup Container => new(this);
    public PodmanClientImageGroup Image => new(this);
    public PodmanClientNetworkGroup Network => new(this);
    public PodmanClientVolumeGroup Volume => new(this);
    public PodmanClientPodGroup Pod => new(this);
    public PodmanClientSystemGroup System => new(this);
    // ...18 groups total
}
```

## Generated code statistics

- **374 generated source files** total
- **187 command classes** (sealed `ICliCommand` implementations)
- **187 builder classes** (`AbstractBuilder<T>` with fluent `With*()` API)
- **1 client class** with 18 nested command groups

## Two-phase scraping pipeline

Unlike Packer (single-phase), the Podman scraper follows the Vagrant pattern with two phases for performance:

```
Phase 1: Build Images
  For each version:
    alpine:3.19 + curl + tar
      → download podman-remote-static-linux_amd64.tar.gz
      → install to /usr/local/bin/podman
      → podman commit → podman-scrape:{version}

Phase 2: Scrape
  For each version (parallel):
    podman run -d podman-scrape:{version} sleep infinity
      → podman exec ... podman <cmd> --help
      → PodmanHelpParser → JSON
      → cleanup container

Finally:
  → cleanup all containers
  → cleanup all podman-scrape:* images
```

Phase 1 builds reusable images so Phase 2 can start containers instantly without repeating the download + install step. This dramatically improves parallel scraping performance since container startup from a pre-built image is near-instant.

## PodmanHelpParser

Podman uses Go's cobra CLI framework, which produces help output that differs from the `StandardHelpParser`'s expectations:

### Cobra help format

```
Available Commands:
  attach      Attach to a running container
  build       Build an image using instructions from Containerfiles

Flags:
      --config string           Location of config file (default "/etc/containers/containers.conf")
  -c, --connection string       Connection to use for remote Podman service
      --env stringArray         Set environment variables
  -l, --log-level string        Log messages above specified level (default "warn")
      --noout                   Do not output to stdout
```

Key differences from standard help parsers:
- **Type hints are lowercase identifiers** (`string`, `int`, `stringArray`) rather than UPPERCASE placeholders or `<bracketed>` tokens
- **Section headers** use "Available Commands:", "Flags:", "Global Flags:" (cobra convention)
- **Boolean flags** have no type hint suffix -- they are bare `--flag-name`

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

Podman uses GNU-style CLI conventions (the default for `[BinaryWrapper]`):

| Feature | Podman Convention |
|---------|-------------------|
| Flag prefix | `--` (double dash) |
| Value format | `--flag value` (space-separated) |
| Boolean format | `--flag` (presence = true, absence = false) |
| Multiple values | repeated flags (`--env FOO --env BAR`) |
| Short flags | `-f` (single dash, single char) |

This is configured by the bare `[BinaryWrapper("podman")]` attribute with no additional parameters (all defaults).

## Command group hierarchy

```
PodmanClient
├── attach, build, commit, compose, cp, create, diff, events, exec,
│   export, history, images, import, info, init, inspect, kill, load,
│   login, logout, logs, pause, port, ps, pull, push, rename, restart,
│   rm, rmi, run, save, search, start, stats, stop, tag, top, unpause,
│   untag, version, wait
├── Container/          attach, checkpoint, clone, commit, cp, create, ...
├── Image/              build, diff, exists, history, import, inspect, ...
├── Network/            connect, create, disconnect, exists, inspect, ls, ...
├── Volume/             create, exists, inspect, ls, prune, reload, rm
├── Pod/                clone, create, exists, inspect, kill, logs, ...
├── System/             connection/, df, info, migrate, prune, renumber, ...
│   └── Connection/     add, default, list, remove, rename
├── Machine/            info, init, inspect, list, os/, reset, rm, ...
│   └── Os/             apply
├── Secret/             create, inspect, ls, rm, exists
├── Manifest/           add, annotate, create, exists, inspect, push, rm
├── Healthcheck/        run
├── Generate/           kube, spec, systemd
├── Play/               kube
├── Kube/               apply, down, generate, play
├── Farm/               build, create, list, remove, update
├── Artifact/           add, extract, inspect, ls, pull, push, rm
└── Quadlet/            info
```

## Version coverage

- **55 versions scraped**: 4.1.1 through 5.8.0
- **2 broken versions excluded**: 4.1.0 and 4.3.0 (static binaries not truly static on Alpine)
- **Version range**: `[SinceVersion("4.1.1")]` through latest

## Dependencies

The Podman library depends on four sibling FrenchExDev.Net packages:

```
FrenchExDev.Net.Podman
├── FrenchExDev.Net.BinaryWrapper             Core: ICliCommand, CommandExecutor, IOutputParser
├── FrenchExDev.Net.BinaryWrapper.Attributes  [BinaryWrapper] attribute
├── FrenchExDev.Net.BinaryWrapper.SourceGenerator  (Analyzer, no runtime ref)
├── FrenchExDev.Net.Builder                   AbstractBuilder<T>, fluent builders
└── FrenchExDev.Net.Result                    Result<T>, Result<T,TError>
```

The Design tool additionally depends on:
- `FrenchExDev.Net.BinaryWrapper.Design` -- scraping framework, `ScrapePipeline`, `MultiVersionScraper`
- `Microsoft.Extensions.Logging` / `.Console`

## Known quirks

### Podman 4.1.0 and 4.3.0 broken binaries

These versions publish `podman-remote-static*.tar.gz` on GitHub releases, but the binaries are not truly static -- they fail with exec format errors or WSL detection crashes on Alpine. These versions are excluded from scraping. The `VersionDiffer` fills gaps from adjacent versions.

### Asset naming changed at v4.4.0

- Pre-4.4.0: `podman-remote-static.tar.gz`
- 4.4.0+: `podman-remote-static-linux_amd64.tar.gz`
- 4.0.0: no static binary at all

The `AssetName()` method in `Program.cs` handles this automatically using `CompareVersionStrings`.

### Some cobra options parsed as flags

The cobra help format for `podman build` uses `--tag` and `--file` without type hints in some versions, causing the parser to classify them as `bool?` rather than `string?`. This is a known edge case where the help text is ambiguous. The generated API still works -- callers set them as booleans. Future parser refinements may address this.

### Tar ownership in rootless podman

When scraping inside rootless podman, `tar` extraction may fail with `Cannot change ownership to uid ...`. The scraper uses `--no-same-owner` in the tar command to avoid this.
