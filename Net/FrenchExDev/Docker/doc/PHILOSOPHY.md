# Philosophy

Design rationale and key decisions behind FrenchExDev.Net.Docker.

## Core Principle: Generate Everything

The entire public API of this library is source-generated from scraped help text. The only hand-written code is a 6-line descriptor class. This is intentional and central to the design.

### Why generate?

- **Docker has 170+ commands** across 16 groups, each with dozens of options. Writing and maintaining these by hand is impractical.
- **Options change between versions** -- flags are added, renamed, deprecated, and removed. The generator tracks these changes automatically via `[SinceVersion]` / `[UntilVersion]`.
- **Consistency is guaranteed** -- every command follows the same pattern (sealed class, init properties, `ToArguments()`, fluent builder), eliminating the class of bugs that arise from inconsistent hand-written wrappers.

### What we trade

- **No custom output parsing** -- the generated API builds commands but doesn't parse Docker's output. This is intentional: output parsing is use-case specific and should be added incrementally.
- **No semantic validation** -- the builder validates types but not Docker-specific constraints (e.g., `--memory` must be a valid byte size). Docker itself validates these at execution time.
- **No Docker Compose** -- Docker Compose is a separate binary with its own wrapper project.

## Cobra Parser Choice

Docker is written in Go and uses the [cobra](https://github.com/spf13/cobra) CLI framework. Cobra produces help output with lowercase type hints (`string`, `int`, `stringArray`) rather than UPPERCASE placeholders used by some other tools.

Using the standard help parser would misclassify most options as boolean flags (since they lack `UPPERCASE VALUE` after the flag name). The cobra-aware parser correctly interprets these type hints, producing accurate CLR type mappings.

## Static Binaries for Scraping

Docker distributes pre-built static Linux binaries at `download.docker.com`. These are fully self-contained -- no glibc, no Docker daemon, no dependencies. This makes Alpine 3.19 an ideal scraping base: minimal image size, fast builds, and the binary just works.

The `--help` output doesn't require a running Docker daemon, so the static binary is sufficient for scraping even though it can't actually run containers.

## GitHubTagsVersionCollector

Docker's `docker/cli` repository uses git tags rather than GitHub Releases to mark versions. `GitHubTagsVersionCollector` reads these tags via the GitHub API, providing a reliable version list without scraping HTML.

## Two-Phase Scraping

Downloading 129 static binaries (~20MB each) sequentially would be slow. The two-phase approach separates concerns:

- **Phase 1** builds reusable images (one per version), caching the downloaded binary
- **Phase 2** starts containers from cached images (~100ms each) and scrapes in parallel

This architecture allows re-scraping a specific version without re-downloading its binary, and enables parallel scraping for throughput.

## GNU-Style CLI Defaults

Docker follows GNU CLI conventions:
- Boolean flags: `--detach` (presence = true)
- Value options: `--name my-container` (space-separated)
- Multi-value: `--env FOO=bar --env BAZ=qux` (repeated flag)

These are the default conventions for `[BinaryWrapper]`, so no custom attribute parameters are needed (unlike Packer which requires `FlagPrefix="-"`, `FlagValueSeparator="="`, and `UseBoolEqualsFormat=true`).

## Minimal Hand-Written Code

The library intentionally avoids hand-written extensions:

- **No custom events or parsers** -- output parsing is use-case specific. The generated command/builder/client API provides the foundation; consumers add parsing as needed.
- **No convenience methods** -- methods like `RunAndWaitAsync()` or `BuildAndPushAsync()` would couple the library to specific workflows. The generated API is deliberately low-level.
- **No Docker daemon management** -- starting/stopping Docker daemon is an OS-level concern outside the scope of a CLI wrapper.

This keeps the library focused on one thing: building correctly-typed Docker CLI invocations with version awareness.

## Version Range

The library includes scrape data from Docker 18.09.0 through 29.3.0. The `DefaultMinVersion` for new scrapes is `23.0.0` because:

- Docker 23.0.0 was the first version with the modern command structure (sub-groups replacing top-level shortcuts)
- Pre-23.0.0 versions are included for legacy compatibility but are not the primary target
- The version numbering changed from `YY.MM.patch` to sequential at 23.0.0

## What This Library Does NOT Do

- **Execute commands** -- that's `CommandExecutor`'s job (from BinaryWrapper)
- **Parse output** -- consumers implement `IOutputParser<T>` for their specific needs
- **Manage Docker installation** -- the library assumes Docker is already installed
- **Provide Docker Compose support** -- see `FrenchExDev.Net.DockerCompose`
- **Abstract over container runtimes** -- this wraps Docker specifically; see `FrenchExDev.Net.Podman` for Podman
