# Vos.Alpine.DockerHost -- Architecture

## 1. Overview

Vos.Alpine.DockerHost extends the base Alpine VirtualBox configuration with Docker-specific defaults: a privileged Docker provisioning step, shared folders for docker-compose and data directories, and Docker environment variables. It implements `IMachineTypeContributor` and composes with `AlpineVirtualBoxContributor` by delegation.

---

## 2. Project Structure

```
Vos.Alpine.DockerHost/
  FrenchExDev.Net.Vos.Alpine.DockerHost.slnx
  quality-gate.yml
  coverage.runsettings
  src/
    FrenchExDev.Net.Vos.Alpine.DockerHost/
      DockerHostContributor.cs         (single source file)
      FrenchExDev.Net.Vos.Alpine.DockerHost.csproj
  test/
    FrenchExDev.Net.Vos.Alpine.DockerHost.Tests/
      DockerHostContributorTests.cs
      FrenchExDev.Net.Vos.Alpine.DockerHost.Tests.csproj
```

---

## 3. Dependency Graph

```
FrenchExDev.Net.Vos (core framework)
  |-- IMachineTypeContributor
  |-- VosMachineType, VosProviderConfig, VosProvisioningStep, VosSharedFolder
  |
  +-- FrenchExDev.Net.Vos.Alpine
  |     +-- AlpineVirtualBoxContributor
  |
  +-- FrenchExDev.Net.Vos.Alpine.DockerHost  <-- this project
        +-- DockerHostContributor
              calls AlpineVirtualBoxContributor.Contribute() internally
```

---

## 4. DockerHostContributor

### MachineTypeName

Returns `"docker-host"`.

### Contribute(VosMachineType)

Executes in two phases:

**Phase 1: Apply Alpine base**
```csharp
new AlpineVirtualBoxContributor().Contribute(machineType);
```

**Phase 2: Add Docker layer**

| Category | Configuration |
|----------|--------------|
| **Provisioning** | `docker` step: version 1.0, enabled, privileged, `DOCKER_COMPOSE_VERSION=latest` |
| **Shared Folders** | `./docker-compose` -> `/opt/docker-compose` (VirtualBox type) |
| | `./data` -> `/data` (default type) |
| **Variables** | `DOCKER_HOST_TYPE=alpine`, `DOCKER_BRIDGE=docker0` |

### Composition model

```
DockerHostContributor.Contribute(mt)
  1. AlpineVirtualBoxContributor.Contribute(mt)
     -> Box, Provider, VBoxManage, Plugins, ALPINE_VERSION
  2. Docker provisioning step
  3. Docker shared folders
  4. Docker variables
```

---

## 5. Provisioning Step Detail

```csharp
new VosProvisioningStep
{
    Key = "docker",
    Version = "1.0",
    Enabled = true,
    Privileged = true,      // Docker install requires root
    Env = { ["DOCKER_COMPOSE_VERSION"] = "latest" }
}
```

The provisioning step key (`docker`) maps to a shell script in the Vos provisioning system that installs Docker and Docker Compose on the Alpine guest.

---

## 6. Shared Folders

| Host Path | Guest Path | Type | Purpose |
|-----------|-----------|------|---------|
| `./docker-compose` | `/opt/docker-compose` | virtualbox | Docker Compose project files |
| `./data` | `/data` | (default) | Persistent data volumes |

---

## 7. Test Coverage

- **5 xUnit tests** covering inheritance, provisioning, shared folders, variables, and machine type name
- **100% line + branch coverage**
- **Test quality score: 1.0**
- Uses Shouldly for fluent assertions
