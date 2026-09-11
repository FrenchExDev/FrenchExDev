# Packer.Alpine — Claude Context

Alpine Linux Packer image builder built on `Packer.Bundle`. Provides
contributors that compose a complete `PackerBundle` for an Alpine VirtualBox
image (boot ISO, answer files, provisioning scripts, Vagrantfile companion).

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Plan](doc/PLAN.md)

## Relevant skills
- [PACKER-BUNDLE](../../../Skills/Net/Programming/PACKER-BUNDLE/PHILOSOPHY.md)
- [BUILDER-PATTERN](../../../Skills/Net/Programming/BUILDER-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Packer.Alpine.slnx`

## Notes for Claude
- This package depends on `Packer.Bundle` and contributes to its `PackerBundle` workspace — never duplicate HCL2 record types here.
- Alpine version is parameterized (constructor arg), not hardcoded. Defaults follow the Alpine release cadence.
- Contributors must be non-destructive (`Box ??=`, `Plugins.Contains()` before adding) so they compose with downstream contributors (e.g. `Packer.Alpine.DockerHost`).
- The HTTP-served answer file (`answers.txt`) and the boot command live in this package as `BundleFile` entries.
- README/doc files may currently be empty stubs — verify content via the source before documenting.
