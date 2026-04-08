# PodmanCompose — Claude Context

Typed C# wrapper for `podman-compose`, generated from `--help` output across multiple versions via the BinaryWrapper framework.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)

## Relevant skills
- [BINARY-WRAPPER](../../../Skills/Net/Programming/BINARY-WRAPPER/PHILOSOPHY.md)
- [COMPOSE-BUNDLE](../../../Skills/Net/Programming/COMPOSE-BUNDLE/PHILOSOPHY.md)
- [WRAPPER-VERSIONING](../../../Skills/Net/Programming/WRAPPER-VERSIONING/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.PodmanCompose.slnx`

## Notes for Claude
- `podman-compose` is a Python tool, not Go. Help parser is `ArgparseHelpParser`, not Cobra.
- Install in the scrape container is `pip install podman-compose=={version}`, not a tarball download.
- Version collector is `GitHubReleasesVersionCollector("containers", "podman-compose")`. Needs `GITHUB_TOKEN`.
- Versions covered: 1.1.0 through 1.5.0 (6 versions). Small surface compared to Podman itself.
- Descriptor uses GNU defaults (`[BinaryWrapper("podman-compose")]`) — `--flag value` style.
- `SkippedCommands`: `help`. Argparse does not have a `completion` subcommand to skip.
- Generated client is `PodmanComposeClient` with 25 typed command classes.
