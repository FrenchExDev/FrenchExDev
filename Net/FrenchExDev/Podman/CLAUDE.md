# Podman — Claude Context

Typed C# wrapper for the `podman` container engine, generated from `--help` text scraped across 55+ versions (4.1.0 through 5.8.x) via the BinaryWrapper framework.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [BINARY-WRAPPER](../../../Skills/Net/Programming/BINARY-WRAPPER/PHILOSOPHY.md)
- [WRAPPER-VERSIONING](../../../Skills/Net/Programming/WRAPPER-VERSIONING/PHILOSOPHY.md)
- [DESIGN-PHASED-PROJECT](../../../Skills/Net/Programming/DESIGN-PHASED-PROJECT/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Podman.slnx`

## Notes for Claude
- Versions `4.1.0` and `4.3.0` are permanently broken on Alpine — `podman-remote-static` is not actually static for those releases. Mark known-missing, do not retry.
- Asset naming changed at 4.4.0: pre-4.4.0 ships `podman-remote-static.tar.gz`, 4.4.0+ ships `podman-remote-static-linux_amd64.tar.gz`. The install script in `Program.cs` handles this conditionally.
- Container runtime for scraping is **podman itself** (alpine:3.19 base). Phase 1 builds `podman-scrape:{version}` images, Phase 2 scrapes from them.
- Help parser is `CobraHelpParser` (built-in). Cobra type hints (`string`, `int`, `stringArray`, `uint`) are recognised.
- `SkippedCommands`: `help`, `completion`.
- Cleanup `finally` removes both containers and `podman-scrape:*` images after the run.
- Version collector is `GitHubReleasesVersionCollector("containers", "podman")`. Set `GITHUB_TOKEN` in `Net/FrenchExDev/.env` or hit the 60/hour rate limit.
- Some versions have a `version` leaf command that clashes with a `version` sub-group; `ClientClassEmitter.PruneClashingLeaves` handles it — do not hand-fix.
