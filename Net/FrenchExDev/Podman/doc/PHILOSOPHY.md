# Design Philosophy

## Why wrap Podman?

Podman is a daemonless container engine compatible with the Docker CLI. It has over 180 commands across 18 command groups, with options that change between versions. Manually maintaining typed wrappers for this surface area would be impractical. The BinaryWrapper source generator automates this entirely from scraped help text.

## Key decisions

### Cobra-aware parser over StandardHelpParser

Podman uses Go's cobra framework, which produces help text with lowercase type hints (`string`, `int`, `stringArray`) rather than UPPERCASE placeholders. The `StandardHelpParser` only recognizes UPPERCASE and `<bracketed>` tokens, so it would classify most Podman options as boolean flags.

The `PodmanHelpParser` was written specifically to handle cobra conventions:
- Recognizes cobra type hints and maps them to CLR types
- Handles `Flags:` and `Global Flags:` section headers
- Distinguishes single-value types (`string`, `int`) from multi-value types (`strings`, `stringArray`)

This ensures the generated API has correct types: `string?` for single-value options, `IReadOnlyList<string>?` for multi-value options, and `bool?` for flags.

### Two-phase scraping (Vagrant pattern)

Podman publishes static binaries on GitHub releases (~20MB each). Downloading and installing 55 times sequentially would be slow. The two-phase approach:

1. **Phase 1** builds a `podman-scrape:{version}` image for each version (download + install once)
2. **Phase 2** starts containers from pre-built images for fast parallel scraping

This is the same pattern used by the Vagrant wrapper. Container startup from a local image is near-instant (~100ms), making Phase 2 highly parallelizable.

### Podman as its own container runtime

The scraper uses podman itself to run containers for scraping podman. This is intentional:
- Podman is daemonless and works in rootless mode
- No dependency on Docker for development
- The `--runtime` flag allows switching to docker if needed

### GitHub Releases version collector (reuse, not custom)

Unlike Vagrant (which needs a custom collector for HashiCorp's releases API), Podman publishes releases on GitHub in the standard `v{major}.{minor}.{patch}` tag format. The existing `GitHubReleasesVersionCollector("containers", "podman")` works out of the box with no custom code.

### Minimal library -- generated code only

The Podman library currently contains only:
- `PodmanDescriptor.cs` -- one-line `[BinaryWrapper("podman")]` trigger
- 55 scrape JSON files

There are no hand-written events, parsers, or collectors yet. This is deliberate:
- The generated command/builder/client API is immediately useful for building CLI invocations
- Output parsing can be added incrementally as specific use cases arise
- A minimal library is easier to maintain and has zero runtime overhead beyond the generated code

### GNU-style defaults

Podman follows GNU CLI conventions (`--flag value`, `--flag` for booleans), which are the default for `[BinaryWrapper]`. No custom attribute parameters are needed, unlike Packer which requires `FlagPrefix="-"`, `FlagValueSeparator="="`, and `UseBoolEqualsFormat=true`.

### Version coverage breadth over depth

The scraper covers 55 versions (4.1.1 through 5.8.0) rather than just the latest. This gives the source generator enough data to compute accurate `[SinceVersion]` and `[UntilVersion]` attributes, allowing consumers to target specific Podman versions with runtime safety.

Two broken versions (4.1.0, 4.3.0) are excluded rather than hacked around. The `VersionDiffer` fills gaps from adjacent versions, so no data is lost.

## What this wrapper does NOT do

- **No process execution** -- The wrapper generates command objects (`ICliCommand`) that serialize to argument lists. Actual process spawning is handled by the BinaryWrapper runtime (`CommandExecutor`).
- **No output parsing** -- Unlike Packer and Vagrant, there is no `PodmanOutputParser` or event hierarchy yet. This is by design -- Podman's output formats vary widely by command and are better handled per use case.
- **No daemon management** -- Podman is daemonless. There is no equivalent of Docker's daemon lifecycle management.
- **No Compose support** -- While `podman compose` exists, it delegates to external tools (docker-compose, podman-compose). The wrapper generates the command but does not integrate with Compose file parsing.
