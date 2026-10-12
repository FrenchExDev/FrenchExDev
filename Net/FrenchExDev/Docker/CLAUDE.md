# Docker — Claude Context

Typed C# wrapper for the `docker` CLI, generated from `--help` text scraped across 128+ versions (18.09.x through 29.x) via the BinaryWrapper framework.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Comparison Table](doc/COMPARISON-TABLE.md)

## Relevant skills
- [BINARY-WRAPPER](../../../Skills/Net/Programming/BINARY-WRAPPER/PHILOSOPHY.md)
- [WRAPPER-VERSIONING](../../../Skills/Net/Programming/WRAPPER-VERSIONING/PHILOSOPHY.md)
- [DESIGN-PHASED-PROJECT](../../../Skills/Net/Programming/DESIGN-PHASED-PROJECT/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Docker.slnx`

## Notes for Claude
- Version collector is `GitHubTagsVersionCollector("docker", "cli")`, **not** GitHub releases. The Docker CLI repo does not publish formal releases that match CLI versions.
- Docker static binaries are downloaded from `download.docker.com`, not GitHub. Install script handles versioned `docker-{version}.tgz` URLs.
- Help parser is `CobraHelpParser` (built-in).
- 51 versions also have cached `.help.txt` dumps so `--reparse` can iterate without containers.
- Several 20.10.x and 23.0.x patch versions are marked known-missing (broken upstream tarballs) — do not retry.
- 16 nested command groups (`Container`, `Image`, `Network`, `Volume`, `Trust.Key`, etc.).
- Needs `GITHUB_TOKEN` in `Net/FrenchExDev/.env` for tag enumeration; 60/hour rate limit otherwise.
- Output parsing not yet implemented (no `IOutputParser` / `IResultCollector`).
