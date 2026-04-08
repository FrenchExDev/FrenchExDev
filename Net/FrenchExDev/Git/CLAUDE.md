# Git — Claude Context

Typed C# wrapper for the `git` CLI, generated from `--help` text scraped across 134+ git versions (2.30.x through 2.53.x) via the BinaryWrapper framework.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Plan](doc/PLAN.md)

## Relevant skills
- [BINARY-WRAPPER](../../../Skills/Net/Programming/BINARY-WRAPPER/PHILOSOPHY.md)
- [WRAPPER-VERSIONING](../../../Skills/Net/Programming/WRAPPER-VERSIONING/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Git.slnx`

## Notes for Claude
- Git is built **from source** in the scrape container (`alpine:3.19` + `make` + `gcc` + source tarball). No prebuilt binary, no package manager install. The build is slow — Phase 1 image build is the bottleneck.
- Help parser is custom `GitHelpParser`. Git's `--help` writes to stderr and exits with code 129; the runtime middleware handles both, plus a `git help -a` override for top-level command discovery.
- Help flag is `-h`, not `--help` (the latter opens a man page).
- Version collector is `GitHubTagsVersionCollector("git", "git")`. Needs `GITHUB_TOKEN`.
- Generated surface: ~150 commands, 422 source files, including nested groups (`Stash`, `Remote`, `Submodule`, `Worktree`, ...).
- 15 parser tests live in `FrenchExDev.Net.Git.Tests`. Add a regression test when a new git release introduces an option that breaks the parser.
