# Architecture

This document describes the system design of FrenchExDev.Net.Docker -- a source-generated, type-safe .NET wrapper for the Docker CLI.

## Overview

The architecture has three distinct phases: **Design**, **Build**, and **Runtime**. Each phase is handled by a separate project or mechanism.

```
┌──────────────────────────────────────────────────────────────────────┐
│                          DESIGN TIME                                 │
│                                                                      │
│  GitHubTagsVersionCollector ──► version list (docker/cli tags)       │
│           │                                                          │
│           ▼                                                          │
│  DesignPipeline (two-phase)                                          │
│    Phase 1: Build alpine:3.19 images with static Docker binary       │
│    Phase 2: Scrape docker --help recursively via cobra parser        │
│           │                                                          │
│           ▼                                                          │
│  scrape/docker-{version}.json  (129 files, 18.09.0 – 29.3.0)         │
└──────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────────┐
│                          BUILD TIME                                  │
│                                                                      │
│  BinaryWrapperGenerator (Roslyn Incremental Source Generator)        │
│    Inputs:                                                           │
│      • [BinaryWrapper("docker")] on DockerDescriptor                 │
│      • AdditionalFiles: scrape/docker-*.json                         │
│    Processing:                                                       │
│      • VersionDiffer.Merge() — union of all commands across versions │
│      • SinceVersion / UntilVersion annotation                        │
│      • PruneClashingLeaves — resolve group/leaf name conflicts       │
│    Outputs (350 files):                                              │
│      • 174 Docker{Cmd}Command.g.cs                                   │
│      • 174 Docker{Cmd}CommandBuilder.g.cs                            │
│      • DockerClient.g.cs (16 groups + top-level commands)            │
│      • DockerDescriptor.BinaryWrapper.g.cs                           │
└──────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────────┐
│                           RUNTIME                                    │
│                                                                      │
│  Docker.Create(binding) ──► DockerClient                             │
│    client.Container.RunAsync(b => b.WithDetach(true)...)             │
│      │                                                               │
│      ├── VersionGuard.EnsureCommandSupported()                       │
│      ├── Builder.BuildAsync() → Result<DockerContainerRunCommand>    │
│      │                                                               │
│      ▼                                                               │
│  DockerContainerRunCommand.ToArguments()                             │
│    → ["--detach", "--name", "my-app", ...]                           │
│      │                                                               │
│      ▼                                                               │
│  CommandExecutor.ExecuteAsync(binding, cmd, ct)                      │
│    → spawns docker process with serialized arguments                 │
└──────────────────────────────────────────────────────────────────────┘
```

## Projects

### FrenchExDev.Net.Docker (Main Library)

The only hand-written source file is `DockerDescriptor.cs`:

```csharp
[BinaryWrapper("docker")]
public partial class DockerDescriptor;
```

Everything else is source-generated at build time. The `.csproj` includes the JSON scrape files as `AdditionalFiles`, which the `BinaryWrapperGenerator` reads to produce the full API surface.

### FrenchExDev.Net.Docker.Design (Scraper)

A console application that orchestrates the scraping pipeline:

1. **Version discovery** -- `GitHubTagsVersionCollector("docker", "cli")` queries GitHub's tag API
2. **Image build** -- For each version, builds an Alpine 3.19 image with the static Docker binary installed
3. **Help scraping** -- Starts a container for each image, recursively runs `docker <cmd> --help`, and parses with the cobra parser
4. **JSON serialization** -- Writes `docker-{version}.json` to the `scrape/` directory

The pipeline supports two modes:
- **Full pipeline**: builds images + scrapes (default)
- **Reparse pipeline**: re-parses from cached help text without containers

### FrenchExDev.Net.Docker.Tests

56 xUnit tests organized into three classes:
- `CommandSerializationTests` -- verifies `ToArguments()` for flags, strings, and lists
- `GeneratedCodeTests` -- verifies client API surface, group navigation, and builder fluent chains
- `DescriptorTests` -- smoke test for `DockerDescriptor`

## Source Generator Pipeline

### Input processing

The `BinaryWrapperGenerator` is an incremental source generator that:

1. Detects the `[BinaryWrapper("docker")]` attribute on `DockerDescriptor`
2. Collects all `AdditionalFiles` matching `scrape/docker-*.json`
3. Deserializes each JSON file into a command tree structure
4. Merges all versions using `VersionDiffer.Merge()` to produce a single unified tree

### Version merging

`VersionDiffer` tracks when each command and option first appears (`SinceVersion`) and when it disappears (`UntilVersion`). The merged tree is the union of all commands across all scraped versions, with version annotations preserving compatibility information.

Example: `docker build` (top-level) exists in 18.09.0 but is removed by 23.0.0 in favor of `docker builder build`. The merged API includes both, with appropriate version annotations.

### Clash resolution

`PruneClashingLeaves` handles cases where a leaf command and a sub-group have the same name across different versions. The leaf is pruned in favor of the group, since the group provides more granular access.

### Output generation

For each command in the merged tree, the generator emits:

| File | Contents |
|------|----------|
| `Docker{Cmd}Command.g.cs` | Sealed class implementing `ICliCommand` with `init`-only properties and `ToArguments()` |
| `Docker{Cmd}CommandBuilder.g.cs` | Class extending `AbstractBuilder<T>` with fluent `With*()` methods |
| `DockerClient.g.cs` | Client class with async methods for each command and nested group classes |
| `DockerDescriptor.BinaryWrapper.g.cs` | Descriptor metadata (binary name, version info) |

All generated code is marked `[ExcludeFromCodeCoverage]` and uses `#nullable enable`.

## Command Hierarchy

The generated `DockerClient` mirrors Docker's CLI structure with 16 top-level groups and 2 nested sub-groups:

```
DockerClient
├── 40+ top-level commands (shortcuts for common container/image operations)
├── Builder       (2 commands)
├── Checkpoint    (3 commands)
├── Config        (5 commands)
├── Container     (25 commands)
├── Context       (9 commands)
├── Engine        (4 commands)
├── Image         (12 commands)
├── Manifest      (5 commands)
├── Network       (7 commands)
├── Node          (7 commands)
├── Plugin        (10 commands)
├── Secret        (4 commands)
├── Service       (9 commands)
├── Stack         (6 commands)
├── Swarm         (8 commands)
├── System        (4 commands)
├── Trust         (3 commands + 2 sub-groups)
│   ├── Key       (2 commands)
│   └── Signer    (2 commands)
└── Volume        (6 commands)
```

Each group is implemented as a nested class within `DockerClient`, constructed lazily via a property accessor. Groups receive the parent client's `_binding` for version checking.

## Type Mapping

The cobra help parser maps Docker's CLI option types to .NET types:

| Docker CLI | Cobra type hint | Generated C# type | Serialization |
|------------|----------------|-------------------|---------------|
| `--detach` | (none) | `bool?` | `--detach` |
| `--name string` | `string` | `string?` | `--name value` |
| `--label list` | `list` | `IReadOnlyList<string>?` | `--label a --label b` |
| `--memory bytes` | `bytes` | `string?` | `--memory 512m` |
| `--cpu-count int` | `int` | `string?` | `--cpu-count 4` |

## Version Gating

Version gating operates at two levels:

1. **Client level** -- `VersionGuard.EnsureCommandSupported()` checks the bound version before building a command. Throws `CommandNotSupportedException` if outside range.

2. **Builder level** -- `VersionGuard.EnsureOptionSupported()` checks individual options in `With*()` methods. Throws if an option is used with an incompatible version.

Both checks are no-ops when `DetectedVersion` is null (unknown version), allowing permissive usage when version detection is not available.

## Dependency Graph

```
DockerDescriptor.cs
    │
    ├── [BinaryWrapper] ← FrenchExDev.Net.BinaryWrapper.Attributes
    │
    ├── Generated commands ← FrenchExDev.Net.BinaryWrapper (ICliCommand, VersionGuard)
    │                      ← FrenchExDev.Net.Builder (AbstractBuilder<T>)
    │                      ← FrenchExDev.Net.Result (Result<T>)
    │
    └── Source generator ← FrenchExDev.Net.BinaryWrapper.SourceGenerator (analyzer)
                         ← FrenchExDev.Net.Builder.SourceGenerator.Lib (analyzer)
```

## Known Quirks

- **Deprecated top-level commands**: Docker 23.0.0+ removed several top-level shortcuts (`build`, `exec`, `images`, `login`, `logout`). These are annotated with `[UntilVersion("23.0.0")]` and remain accessible for older binaries.
- **Engine commands**: `docker engine activate/check/ls/update` existed briefly in 18.09.x-19.03.x and were removed. They remain in the generated API with appropriate version annotations.
- **Swarm commands**: The full Swarm command set is always present but may be non-functional in Docker Desktop configurations that don't support Swarm mode.
