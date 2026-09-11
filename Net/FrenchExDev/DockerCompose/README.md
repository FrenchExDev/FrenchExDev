# FrenchExDev.Net.DockerCompose

A type-safe .NET wrapper for Docker Compose V2, auto-generated from scraped CLI help text using the BinaryWrapper source generator.

## Overview

This library provides a fully typed C# API for building Docker Compose commands. The source generator reads 57 scraped JSON files (versions 2.20.0 through 5.1.0) and produces:

- **37 command classes** -- sealed `ICliCommand` implementations with typed properties
- **37 builder classes** -- fluent `AbstractBuilder<T>` with `With*()` methods and `Validate*()` hooks
- **1 client class** -- `DockerComposeClient` with 35 top-level commands and 1 nested group (`Bridge`)

Every command and option is annotated with `[SinceVersion]` / `[UntilVersion]` attributes for runtime version safety.

## Quick start

```csharp
var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("docker-compose"),
    ExecutablePath = "/usr/local/bin/docker-compose",
    DetectedVersion = SemanticVersion.Parse("5.1.0")
};

var client = DockerCompose.Create(binding);

// Build and start services
var upCmd = client.Up(b => b
    .WithDetach(true)
    .WithBuild(true)
);

// Execute via CommandExecutor
var executor = new CommandExecutor(binaryResolver);
var output = await executor.ExecuteAsync(
    new BinaryIdentifier("docker-compose"), upCmd);
```

## Command coverage

| Category | Commands |
|----------|----------|
| Lifecycle | `Up`, `Down`, `Create`, `Start`, `Stop`, `Restart`, `Pause`, `Unpause`, `Kill`, `Rm`, `Wait` |
| Inspection | `Ps`, `Ls`, `Top`, `Images`, `Port`, `Events`, `Stats`, `Version`, `Volumes` |
| Build & Deploy | `Build`, `Pull`, `Push`, `Publish`, `Config` |
| Execution | `Run`, `Exec`, `Attach`, `Logs` |
| Data | `Cp`, `Export`, `Commit` |
| Development | `Watch`, `Scale` |
| Bridge | `Bridge.Convert`, `Bridge.Transformations.Create`, `Bridge.Transformations.List` |

See [doc/ARCHITECTURE.md](doc/ARCHITECTURE.md) for full command tree with version availability.

## Project structure

```
DockerCompose/
├── FrenchExDev.Net.DockerCompose.slnx
├── doc/
│   ├── ARCHITECTURE.md          # System architecture and generated code details
│   ├── HOW-TO.md                # Usage guide, scraping, testing, troubleshooting
│   └── PHILOSOPHY.md            # Design rationale and key decisions
├── src/
│   ├── FrenchExDev.Net.DockerCompose/
│   │   ├── DockerComposeDescriptor.cs    # [BinaryWrapper("docker-compose")]
│   │   └── scrape/                       # 57 version JSON files
│   └── FrenchExDev.Net.DockerCompose.Design/
│       └── Program.cs                    # Shared base and per-version image scraper
└── test/
    └── FrenchExDev.Net.DockerCompose.Tests/
```

## Key design choices

- **No custom help parser** -- Docker Compose uses Go's cobra framework; the shared `CobraHelpParser` from `BinaryWrapper.Design` handles it via `.UseParser("cobra")`
- **Eager cleanup** -- scraper deletes each version's image immediately after scrape, keeping disk usage proportional to `--parallel` count
- **Shared dependencies** -- prepare Alpine and curl once; each parallel worker builds or reuses a version image before scraping
- **Raw binary download** -- no tarball extraction needed; Docker Compose publishes standalone binaries

## Dependencies

```
FrenchExDev.Net.DockerCompose
├── FrenchExDev.Net.BinaryWrapper             Core runtime
├── FrenchExDev.Net.BinaryWrapper.Attributes  [BinaryWrapper] attribute
├── FrenchExDev.Net.BinaryWrapper.SourceGenerator  (Analyzer)
├── FrenchExDev.Net.Builder                   AbstractBuilder<T>
└── FrenchExDev.Net.Result                    Result<T>
```

## Documentation

- [Architecture](doc/ARCHITECTURE.md) -- system design, source generator pipeline, version differencing
- [How-To](doc/HOW-TO.md) -- creating clients, building commands, scraping, testing, troubleshooting
- [Philosophy](doc/PHILOSOPHY.md) -- design rationale, key decisions, what the wrapper does NOT do

## Shared dependency and version images

The Design runner prepares the shared system dependencies once, then installs each
software version in an image derived from that base. `UseVersionImage().UseContainer()`
builds or reuses the image before collecting help. After collection, the container
and version image are removed; `--keep-images` retains the version image. The
shared base remains cached.

From the wrapper directory, with Podman running (or add `--runtime docker`):

```powershell
$design = './src/FrenchExDev.Net.DockerCompose.Design/FrenchExDev.Net.DockerCompose.Design.csproj'
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
