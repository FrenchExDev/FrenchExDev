# Vos.Alpine

Machine type contributor that provides Alpine Linux VirtualBox defaults for the Vos ecosystem. Configures SATA SSD storage, nested virtualization, NIC promiscuous mode, and Vagrant plugins. Maps to the PowerShell `New-VosAlpine` function.

## Quick Start

```csharp
var machineType = new VosMachineType();
new AlpineVirtualBoxContributor().Contribute(machineType);

// machineType now has:
//   Box:      "frenchexdev/alpine-3.21-virt"
//   Provider: VirtualBox, 2GB RAM, 2 CPUs, linked clones
//   VBoxManage: nested virt, SATA SSD, NIC promiscuous
//   Plugins:  vagrant-hostmanager, vagrant-vbguest
//   Variables: ALPINE_VERSION=3.21
```

### Custom version

```csharp
new AlpineVirtualBoxContributor(alpineVersion: "3.20").Contribute(machineType);
// Box: "frenchexdev/alpine-3.20-virt", ALPINE_VERSION=3.20
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Vos.Alpine` | net10.0 | `AlpineVirtualBoxContributor` implementation |
| `Vos.Alpine.Tests` | net10.0 | 7 xUnit + Shouldly tests, 100% branch coverage |

## Key Design Decisions

- **Contributor pattern** -- implements `IMachineTypeContributor` from the Vos core framework
- **Composition over inheritance** -- extends `VosMachineType` by adding configuration, not by subclassing
- **Non-destructive defaults** -- `Box` uses `??=` to preserve existing values; plugins check `Contains()` before adding
- **Parameterized version** -- Alpine version is a constructor parameter, not hardcoded
- **Quality gate: PASSED** -- 96.9% line coverage, 100% branch coverage, test quality score 1.0

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- contributor pattern, VBoxManage commands, ecosystem relationships
- [HOW-TO.md](doc/HOW-TO.md) -- usage, customization, extending the contributor
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why machine types are composable contributors

## Building

```bash
dotnet build Vos.Alpine/FrenchExDev.Net.Vos.Alpine.slnx
dotnet test Vos.Alpine/FrenchExDev.Net.Vos.Alpine.slnx
```
