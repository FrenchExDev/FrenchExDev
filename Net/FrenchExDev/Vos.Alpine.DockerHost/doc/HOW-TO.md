# Vos.Alpine.DockerHost -- Developer Guide (HOW-TO)

## Table of Contents

1. [Basic Usage](#1-basic-usage)
2. [What Gets Configured](#2-what-gets-configured)
3. [Customizing Docker Compose Version](#3-customizing-docker-compose-version)
4. [Adding Shared Folders](#4-adding-shared-folders)
5. [Overriding Inherited Alpine Defaults](#5-overriding-inherited-alpine-defaults)
6. [Running Tests](#6-running-tests)

---

## 1. Basic Usage

```csharp
var machineType = new VosMachineType();
new DockerHostContributor().Contribute(machineType);
```

This applies the full Alpine VirtualBox base configuration and adds Docker-specific provisioning on top.

---

## 2. What Gets Configured

After `Contribute()`, the machine type includes:

**From Alpine base:**
- Vagrant box: `frenchexdev/alpine-3.21-virt`
- VirtualBox: 2GB RAM, 2 CPUs, linked clones
- VBoxManage: nested virt, SATA SSD, NIC promiscuous
- Plugins: vagrant-hostmanager, vagrant-vbguest
- Variable: `ALPINE_VERSION=3.21`

**From Docker layer:**
- Provisioning step: `docker` (privileged, Docker Compose latest)
- Shared folder: `./docker-compose` -> `/opt/docker-compose`
- Shared folder: `./data` -> `/data`
- Variables: `DOCKER_HOST_TYPE=alpine`, `DOCKER_BRIDGE=docker0`

---

## 3. Customizing Docker Compose Version

After `Contribute()`, modify the provisioning step:

```csharp
var mt = new VosMachineType();
new DockerHostContributor().Contribute(mt);

var dockerStep = mt.Provisioning.First(p => p.Key == "docker");
dockerStep.Env["DOCKER_COMPOSE_VERSION"] = "2.24.5";
```

---

## 4. Adding Shared Folders

```csharp
var mt = new VosMachineType();
new DockerHostContributor().Contribute(mt);

mt.SharedFolders.Add(new VosSharedFolder
{
    HostPath = "./configs",
    GuestPath = "/opt/configs",
    Type = "virtualbox"
});
```

---

## 5. Overriding Inherited Alpine Defaults

```csharp
var mt = new VosMachineType();
new DockerHostContributor().Contribute(mt);

// Increase resources for Docker workloads
mt.Provider!.Memory = 4096;
mt.Provider.Cpus = 4;
```

Or set the box before calling `Contribute()` to use a custom image:

```csharp
mt.Box = "my-org/alpine-docker";
new DockerHostContributor().Contribute(mt);
// Box stays "my-org/alpine-docker" (Alpine contributor uses ??=)
```

---

## 6. Running Tests

```bash
dotnet test Vos.Alpine.DockerHost/FrenchExDev.Net.Vos.Alpine.DockerHost.slnx

# With coverage
dotnet quality-gate test --config Vos.Alpine.DockerHost/quality-gate.yml
```

Test cases:
- `Contribute_InheritsAlpineBase` -- verifies box, provider, plugins from Alpine contributor
- `Contribute_AddsDockerProvisioning` -- validates Docker step with env vars
- `Contribute_AddsSharedFolders` -- confirms both shared folder mappings
- `Contribute_AddsDockerVariables` -- checks DOCKER_HOST_TYPE and DOCKER_BRIDGE
- `MachineTypeName_IsDockerHost` -- validates identifier
