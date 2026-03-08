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
│       └── Program.cs                    # 2-phase pipelined scraper
└── test/
    └── FrenchExDev.Net.DockerCompose.Tests/
```

## Key design choices

- **No custom help parser** -- Docker Compose uses Go's cobra framework; the shared `CobraHelpParser` from `BinaryWrapper.Design` handles it via `.UseParser("cobra")`
- **Eager cleanup** -- scraper deletes each version's image immediately after scrape, keeping disk usage proportional to `--parallel` count
- **Pipelined scraping** -- Phase 1 (image builds) and Phase 2 (scraping) overlap via `Channel<string>` for faster throughput
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
