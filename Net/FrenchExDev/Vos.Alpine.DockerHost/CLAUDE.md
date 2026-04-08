# Vos.Alpine.DockerHost — Claude Context

Docker host machine type contributor for Vos. Composes on top of `Vos.Alpine`
to add Docker provisioning, shared folders (`./docker-compose →
/opt/docker-compose`), and Docker-specific environment variables.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [VOS-ORCHESTRATION](../../../Skills/Net/Programming/VOS-ORCHESTRATION/PHILOSOPHY.md)
- [BUILDER-PATTERN](../../../Skills/Net/Programming/BUILDER-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Vos.Alpine.DockerHost.slnx`

## Notes for Claude
- Composes by **delegation**, not inheritance: calls `AlpineVirtualBoxContributor.Contribute()` first, then layers Docker on top.
- Docker provisioning is `Privileged = true` — required to install Docker.
- `DOCKER_COMPOSE_VERSION=latest` is the default; override via constructor.
- Shared folder convention: `./docker-compose → /opt/docker-compose` and `./data → /data`. Both are picked up by the static Vagrantfile.
- Quality gate is PASSED: 100% line + branch coverage. Don't regress.
- This package's only public surface is one contributor class — keep it minimal.
