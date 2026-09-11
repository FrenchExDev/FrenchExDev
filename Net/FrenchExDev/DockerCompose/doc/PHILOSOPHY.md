# Design Philosophy

## Why wrap Docker Compose?

Docker Compose V2 is the standard multi-container orchestration tool. It has 35+ commands with options that evolve across versions -- commands like `watch`, `scale`, `attach`, `stats`, `export`, `commit`, `publish`, and `volumes` were all added at different points between 2.20.0 and 5.1.0. Manually maintaining typed wrappers for this surface area would be impractical. The BinaryWrapper source generator automates this entirely from scraped help text.

## Key decisions

### Shared CobraHelpParser over custom parser

Docker Compose, Docker, and Podman all use Go's cobra framework, producing identical help text formats. Rather than maintaining a third copy of the same parsing logic, the Docker Compose wrapper uses the shared `CobraHelpParser` from `BinaryWrapper.Design` via declarative strategy resolution:

```csharp
.UseParser("cobra")
```

This is a deliberate architectural choice -- the parsing strategy is selected by name, not by concrete type. The `HelpParsers` registry in `BinaryWrapper.Design` resolves `"cobra"` to a `CobraHelpParser` instance. This means:
- No `DockerComposeHelpParser.cs` exists or is needed
- New parsers can be added via `HelpParsers.Register()` without modifying existing code (OCP)
- The Design project has zero custom parsing code

### Two-phase pipelined scraping with eager cleanup

The scraper uses a `Channel<string>`-based producer/consumer pipeline where Phase 1 (image builds) and Phase 2 (scraping) overlap:

- **Phase 1 producers** build images and publish ready versions to the channel immediately
- **Phase 2 consumers** read from the channel and scrape as soon as images are available

This is faster than the sequential two-phase approach used by the Vagrant wrapper, because scraping begins before all images are built.

### Eager cleanup over batch cleanup

Unlike the Podman and Docker wrappers that accumulate all images until the end, the Docker Compose scraper deletes each version's image immediately after its scrape completes:

```
scrape version → rm container → rmi image → done
```

This keeps disk usage proportional to `--parallel` count (typically 4 images at once), not total version count (57+ images). With 57 versions, this is the difference between ~400MB peak disk usage and ~5.7GB.

The `finally` block only handles crash recovery -- cleaning up straggler containers and images that weren't deleted due to an unexpected failure.

### Raw binary download (not tarball)

Docker Compose V2 publishes standalone binaries directly on GitHub releases:

```
https://github.com/docker/compose/releases/download/v{version}/docker-compose-linux-x86_64
```

This is simpler than Docker (tarball with nested directory structure) or Podman (tarball with varying archive names). The install step is a single `curl` + `chmod +x`, with no tar extraction needed.

### GitHub Releases version collector (reuse, not custom)

Docker Compose publishes releases on GitHub in the standard `v{major}.{minor}.{patch}` tag format. The existing `GitHubReleasesVersionCollector("docker", "compose")` works out of the box with no custom code -- the same collector type used by Podman.

### Minimal library -- generated code only

The Docker Compose library contains only:
- `DockerComposeDescriptor.cs` -- one-line `[BinaryWrapper("docker-compose")]` trigger
- 57 scrape JSON files

There are no hand-written events, parsers, or collectors. This is deliberate:
- The generated command/builder/client API is immediately useful for building CLI invocations
- Output parsing can be added incrementally as specific use cases arise
- A minimal library is easier to maintain and has zero runtime overhead beyond the generated code

### GNU-style defaults

Docker Compose follows GNU CLI conventions (`--flag value`, `--flag` for booleans), which are the default for `[BinaryWrapper]`. No custom attribute parameters are needed, unlike Packer which requires `FlagPrefix="-"`, `FlagValueSeparator="="`, and `UseBoolEqualsFormat=true`.

### Broad version coverage (2.20.0 through 5.1.0)

The scraper covers 57 versions across two major versions (V2 and V5). This gives the source generator enough data to compute accurate `[SinceVersion]` and `[UntilVersion]` attributes, allowing consumers to target specific Docker Compose versions with runtime safety.

No broken versions exist in the coverage range -- all published releases have working standalone binaries.

### Binary name: `docker-compose` (standalone V2)

The wrapper targets `docker-compose` (the standalone binary) rather than `docker compose` (the Docker CLI plugin subcommand). This is intentional:
- Standalone binaries are independently versioned and downloadable
- No dependency on the Docker CLI for resolution
- The wrapper can control the exact binary path via `BinaryBinding`

## What this wrapper does NOT do

- **No process execution** -- The wrapper generates command objects (`ICliCommand`) that serialize to argument lists. Actual process spawning is handled by the BinaryWrapper runtime (`CommandExecutor`).
- **No output parsing** -- There is no `DockerComposeOutputParser` or event hierarchy yet. This is by design -- Compose output formats (JSON, table, streaming logs) vary by command and are better handled per use case.
- **No Compose file parsing** -- The wrapper does not read or validate `docker-compose.yml` files. It wraps the CLI binary, not the file format.
- **No Docker CLI plugin mode** -- The wrapper targets the standalone `docker-compose` binary, not `docker compose` as a plugin subcommand.
