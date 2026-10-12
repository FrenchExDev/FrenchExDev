# Vos.Alpine — Claude Context

Alpine Linux machine type contributor for Vos. Implements
`IMachineTypeContributor` to apply Alpine VirtualBox defaults: SATA SSD
storage, nested virtualization, NIC promiscuous mode, Vagrant plugins
(`vagrant-hostmanager`, `vagrant-vbguest`). Maps to the legacy PowerShell
`New-VosAlpine` function.

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
- `FrenchExDev.Net.Vos.Alpine.slnx`

## Notes for Claude
- Contributor pattern, NOT inheritance. The class implements `IMachineTypeContributor` and mutates a `VosMachineType` in place.
- Defaults are non-destructive: `Box ??= "frenchexdev/alpine-3.21-virt"`, plugins checked via `Contains` before adding. This lets it compose cleanly with downstream contributors (`Vos.Alpine.DockerHost`).
- Alpine version is a constructor parameter (`alpineVersion: "3.21"`). Variables and box name reflect the version.
- Quality gate is PASSED at the time of writing: 96.9% line coverage, 100% branch, test quality 1.0. Don't regress.
- Only 7 tests but they cover every branch. Add tests when extending behavior.
