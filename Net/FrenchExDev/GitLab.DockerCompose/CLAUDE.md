# GitLab.DockerCompose — Claude Context

Docker Compose service contributor for self-hosted GitLab Omnibus. Implements
`IComposeFileContributor` from `DockerCompose.Bundle` to emit a typed GitLab
service block (and optional companions: PostgreSQL, Redis, Runner, MinIO),
with the full Omnibus configuration surface modeled as `[Builder]` types.

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
- `FrenchExDev.Net.GitLab.DockerCompose.slnx`

## Notes for Claude
- **Status: design / specification phase.** The doc tree describes the architecture but src/test may be partial.
- This package depends on `DockerCompose.Bundle` and contributes to its `ComposeFile` model — never duplicate compose model classes here.
- The GitLab Omnibus `gitlab.rb` configuration is exposed as typed C# models written into `GITLAB_OMNIBUS_CONFIG` — versioned `.rb` template snippets are emitted by the SG.
- Companion services (Postgres, Redis, Runner, MinIO) are independent contributors so users can opt out per service.
- Mandatory volumes: `/etc/gitlab`, `/var/log/gitlab`, `/var/opt/gitlab`. Ports: 80/443/22/5050/8060.
- Designed to interop with Traefik via labels — do not bake reverse-proxy assumptions into the GitLab service itself.
