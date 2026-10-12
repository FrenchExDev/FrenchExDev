# Vos.Alpine -- Developer Guide (HOW-TO)

## Table of Contents

1. [Basic Usage](#1-basic-usage)
2. [Custom Alpine Version](#2-custom-alpine-version)
3. [Custom Box Name](#3-custom-box-name)
4. [Composing with Other Contributors](#4-composing-with-other-contributors)
5. [Overriding Defaults](#5-overriding-defaults)
6. [Running Tests](#6-running-tests)
7. [Creating a New Contributor](#7-creating-a-new-contributor)

---

## 1. Basic Usage

```csharp
var machineType = new VosMachineType();
new AlpineVirtualBoxContributor().Contribute(machineType);

// machineType is now fully configured for Alpine VirtualBox
```

The contributor sets box, provider, VBoxManage commands, plugins, and variables in a single call.

---

## 2. Custom Alpine Version

```csharp
var contributor = new AlpineVirtualBoxContributor(alpineVersion: "3.20");
contributor.Contribute(machineType);
// Box: "frenchexdev/alpine-3.20-virt"
// Variable: ALPINE_VERSION=3.20
```

---

## 3. Custom Box Name

```csharp
var contributor = new AlpineVirtualBoxContributor(boxName: "my-org/custom-alpine");
contributor.Contribute(machineType);
// Box: "my-org/custom-alpine"
```

Or set the box before calling `Contribute()` -- the contributor won't override it:

```csharp
machineType.Box = "my-org/custom-alpine";
new AlpineVirtualBoxContributor().Contribute(machineType);
// Box stays "my-org/custom-alpine"
```

---

## 4. Composing with Other Contributors

The contributor pattern enables stacking:

```csharp
var mt = new VosMachineType();

// Base Alpine configuration
new AlpineVirtualBoxContributor().Contribute(mt);

// Add Docker on top
new DockerHostContributor().Contribute(mt);
// DockerHostContributor calls AlpineVirtualBoxContributor internally,
// then adds Docker provisioning, shared folders, and variables
```

---

## 5. Overriding Defaults

After `Contribute()`, you can override any setting:

```csharp
var mt = new VosMachineType();
new AlpineVirtualBoxContributor().Contribute(mt);

// Override memory and CPUs
mt.Provider!.Memory = 4096;
mt.Provider.Cpus = 4;

// Add extra plugins
mt.Plugins.Add("my-custom-plugin");
```

---

## 6. Running Tests

```bash
dotnet test Vos.Alpine/FrenchExDev.Net.Vos.Alpine.slnx

# With coverage
dotnet quality-gate test --config Vos.Alpine/quality-gate.yml
```

The test suite covers:
- Box name assignment and preservation
- VirtualBox provider configuration
- VBoxManage commands (nested virt, SSD, NIC)
- Plugin registration (idempotent)
- Alpine version variable
- Existing configuration preservation

---

## 7. Creating a New Contributor

To create a new machine type (e.g. `Vos.Debian`):

1. Create a new project referencing `FrenchExDev.Net.Vos`
2. Implement `IMachineTypeContributor`:

```csharp
public sealed class DebianVirtualBoxContributor : IMachineTypeContributor
{
    public string MachineTypeName => "debian";

    public void Contribute(VosMachineType machineType)
    {
        machineType.Box ??= "debian/bookworm64";
        machineType.Provider ??= new VosProviderConfig();
        machineType.Provider.Type = "virtualbox";
        machineType.Provider.Memory = 2048;
        machineType.Provider.Cpus = 2;
        // ... additional configuration
    }
}
```

3. Add tests following the same pattern as `AlpineVirtualBoxContributorTests`
4. For a Docker variant, create a separate contributor that calls the base first
