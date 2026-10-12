# GitLab.Cli — Claude Context

Typed C# wrapper for `glab`, the official GitLab CLI, generated from `--help` text scraped via the BinaryWrapper framework.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)

## Relevant skills
- [BINARY-WRAPPER](../../../Skills/Net/Programming/BINARY-WRAPPER/PHILOSOPHY.md)
- [WRAPPER-VERSIONING](../../../Skills/Net/Programming/WRAPPER-VERSIONING/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.GitLab.Cli.slnx`

## Notes for Claude
- Version discovery uses **GitLab API v4**, not GitHub. The GitHub mirror at `gitlabhq/cli` publishes no releases.
- Custom `GitLabReleasesVersionCollector` lives in this package's Design project; project path is URL-encoded `gitlab-org%2Fcli`, tags use the `v` prefix.
- Auth env var is `GITLAB_TOKEN`, not `GITHUB_TOKEN`. Header is `PRIVATE-TOKEN: <token>`, not `Authorization: Bearer ...`. Scraping itself does not need a token; only collection.
- `DefaultMinVersion = "1.47.0"`. Earlier releases use a different asset naming scheme (`glab_{v}_linux_amd64.tar.gz` only exists 1.47+).
- Custom `GlabHelpParser` handles glab's customised Cobra template: ALL-CAPS headers (`COMMANDS`, `FLAGS`, `USAGE`, `EXAMPLES`, `ALIASES`, `INHERITED FLAGS`), space-separated `-s --long` flag pairs, no type hints. Standard Cobra parser does not work.
- Value detection in the parser is heuristic — `(default)` suffix, `<placeholder>` in description, or "comma-separated" wording.
- `SkippedCommands`: `help`, `completion`, `check-update`.
- Asset URL pattern: `glab_{version}_linux_amd64.tar.gz`, extracts to `bin/glab` inside the tarball.
