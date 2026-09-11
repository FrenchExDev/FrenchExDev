# GitLab.Ci.Yaml — Claude Context

Schema-driven, source-generated typed model for `.gitlab-ci.yml`. Emits 30
model classes + 30 builders from the official GitLab CI JSON Schema, with
version tracking across GitLab 18.0–18.10, a YAML reader, a YAML writer, and
an `IGitLabCiContributor` composition pattern.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [COMPOSE-BUNDLE](../../../Skills/Net/Programming/COMPOSE-BUNDLE/PHILOSOPHY.md)
- [SG](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [BUILDER-PATTERN](../../../Skills/Net/Programming/BUILDER-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.GitLab.Ci.Yaml.slnx`

## Notes for Claude
- This is a compose-bundle pattern instance even though "Compose" is not in the name. Same five-project layout, same shared `Builder.SourceGenerator.Lib`, same `[SinceVersion]`/`[UntilVersion]` mechanism.
- Schemas are downloaded from GitLab releases (`v18.x.0-ee` tags) by `Bundle.Design`. They are NOT downloaded from GitHub.
- Jobs serialize at the **root level** of the YAML, not under a `jobs:` key — the writer special-cases this.
- The reader (`GitLabCiYamlReader`) round-trips real `.gitlab-ci.yml` files; do not break round-tripping when adding properties.
- `IGitLabCiContributor.Contribute(GitLabCiFile)` mutates in place — same shape as compose contributors.
- Test count is currently small (~20). Adding tests for new generator features is mandatory.
