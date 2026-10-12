# Packer.Alpine.DockerHost — Claude Context

Extends `Packer.Alpine` with Docker installation. Adds provisioning scripts,
shared folders, and environment variables to a `PackerBundle` so the resulting
Alpine image ships with Docker + docker-compose pre-installed.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [PACKER-BUNDLE](../../../Skills/Net/Programming/PACKER-BUNDLE/PHILOSOPHY.md)
- [BUILDER-PATTERN](../../../Skills/Net/Programming/BUILDER-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Packer.Alpine.DockerHost.slnx`

## Notes for Claude
- Composes ON TOP of `Packer.Alpine` — do not re-implement the Alpine base.
- The Docker install provisioner is privileged (root) — required for `apk add docker`.
- README is currently an empty stub; doc files may also be empty. Verify content before citing.
- This package's only public surface is one or two `IPackerBundleContributor` classes; everything else is provisioning script content.
- Keep provisioning scripts idempotent — Packer may re-run them.
