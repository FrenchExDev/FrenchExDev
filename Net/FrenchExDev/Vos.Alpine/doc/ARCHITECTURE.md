# Vos.Alpine -- Architecture

## 1. Overview

Vos.Alpine is a machine type contributor that configures `VosMachineType` with Alpine Linux VirtualBox defaults. It implements the `IMachineTypeContributor` interface from the Vos core framework, providing a composable building block for VM configuration.

---

## 2. Project Structure

```
Vos.Alpine/
  FrenchExDev.Net.Vos.Alpine.slnx
  quality-gate.yml
  coverage.runsettings
  src/
    FrenchExDev.Net.Vos.Alpine/
      AlpineVirtualBoxContributor.cs     (single source file)
      FrenchExDev.Net.Vos.Alpine.csproj
  test/
    FrenchExDev.Net.Vos.Alpine.Tests/
      AlpineVirtualBoxContributorTests.cs
      FrenchExDev.Net.Vos.Alpine.Tests.csproj
```

---

## 3. Dependency Graph

```
FrenchExDev.Net.Vos (core framework)
  |-- IMachineTypeContributor
  |-- VosMachineType, VosProviderConfig
  |-- VosProvisioningStep, VosSharedFolder
  |
  +-- FrenchExDev.Net.Vos.Alpine  <-- this project
        |-- AlpineVirtualBoxContributor : IMachineTypeContributor
        |
        +-- FrenchExDev.Net.Vos.Alpine.DockerHost (downstream consumer)
              +-- DockerHostContributor calls AlpineVirtualBoxContributor.Contribute()
```

---

## 4. AlpineVirtualBoxContributor

### Constructor

```csharp
public AlpineVirtualBoxContributor(
    string alpineVersion = "3.21",
    string? boxName = null)
```

- `alpineVersion`: Alpine Linux version (default: 3.21)
- `boxName`: Vagrant box name (default: `frenchexdev/alpine-{version}-virt`)

### MachineTypeName

Returns `"alpine"` for identification in the Vos type registry.

### Contribute(VosMachineType)

Applies the following configuration:

| Category | Configuration |
|----------|--------------|
| **Box** | `frenchexdev/alpine-{version}-virt` (only if not already set) |
| **Provider** | VirtualBox, 2048 MB RAM, 2 CPUs, linked clones |
| **VBoxManage** | I/O APIC, hardware virtualization, nested virt, nested paging, large pages, PAE |
| **Storage** | SATA controller with host IO cache, non-rotational (SSD) |
| **Network** | NIC2 promiscuous mode allow-all |
| **Plugins** | vagrant-hostmanager, vagrant-vbguest (idempotent add) |
| **Variables** | `ALPINE_VERSION={version}` |

### Non-destructive behavior

- `machineType.Box ??= _boxName` -- preserves existing box
- `machineType.Provider ??= new VosProviderConfig()` -- preserves existing provider
- Plugins use `Contains()` check before `Add()`
- Variables use `TryAdd()`

---

## 5. VBoxManage Commands

The contributor configures 9 VBoxManage commands for optimal Alpine VirtualBox performance:

```
modifyvm   --ioapic on              I/O APIC for multi-CPU
modifyvm   --hwvirtex on            Hardware virtualization
modifyvm   --nested-hw-virt on      Nested virtualization (Docker/K8s in VM)
modifyvm   --nestedpaging on        Performance: hardware nested paging
modifyvm   --largepages on          Performance: large page support
modifyvm   --pae on                 Physical Address Extension
storagectl --hostiocache on         SATA controller host I/O cache
storageattach --nonrotational on    Advertise disk as SSD
modifyvm   --nicpromisc2 allow-all  NIC2 promiscuous for bridged networking
```

---

## 6. Ecosystem Position

```
Vos (core)
  +-- Vos.Alpine            base Alpine config (this project)
  +-- Vos.Alpine.DockerHost extends Alpine + Docker provisioning
  +-- (future: Vos.Alpine.K8s, Vos.Debian, etc.)

Packer ecosystem (parallel):
  +-- Packer.Alpine          builds Alpine Vagrant boxes
  +-- Packer.Alpine.DockerHost  builds Docker-enabled Alpine boxes
```

The contributor pattern allows stacking: `DockerHostContributor` calls `AlpineVirtualBoxContributor.Contribute()` first, then adds Docker-specific configuration on top.

---

## 7. Test Coverage

- **7 xUnit tests** covering all configuration aspects
- **96.9% line coverage, 100% branch coverage**
- **Test quality score: 1.0**
- Uses Shouldly for fluent assertions
