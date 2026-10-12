# Vos.Alpine.DockerHost

Machine type contributor that extends Alpine VirtualBox configuration with Docker-specific provisioning, shared folders, and environment variables. Maps to the PowerShell `New-VosAlpineDocker` function.

## Quick Start

```csharp
var machineType = new VosMachineType();
new DockerHostContributor().Contribute(machineType);

// machineType now has everything from Alpine base PLUS:
//   Provisioning: docker (privileged, DOCKER_COMPOSE_VERSION=latest)
//   SharedFolders: ./docker-compose -> /opt/docker-compose
//                  ./data -> /data
//   Variables:     DOCKER_HOST_TYPE=alpine, DOCKER_BRIDGE=docker0
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Vos.Alpine.DockerHost` | net10.0 | `DockerHostContributor` implementation |
| `Vos.Alpine.DockerHost.Tests` | net10.0 | 5 xUnit + Shouldly tests, 100% coverage |

## Key Design Decisions

- **Contributor pattern** -- implements `IMachineTypeContributor`, composes with `AlpineVirtualBoxContributor`
- **Composition by delegation** -- calls `AlpineVirtualBoxContributor.Contribute()` first, then adds Docker layer
- **Privileged provisioning** -- Docker installation requires root access
- **Quality gate: PASSED** -- 100% line + branch coverage, test quality score 1.0

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- contributor composition, Docker configuration, ecosystem position
- [HOW-TO.md](doc/HOW-TO.md) -- usage, customization, shared folders, provisioning
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why Docker host is a separate contributor

## Building

```bash
dotnet build Vos.Alpine.DockerHost/FrenchExDev.Net.Vos.Alpine.DockerHost.slnx
dotnet test Vos.Alpine.DockerHost/FrenchExDev.Net.Vos.Alpine.DockerHost.slnx
```
