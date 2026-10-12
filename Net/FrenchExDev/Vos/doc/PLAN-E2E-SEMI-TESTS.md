# Plan: Semi-integrated + E2E testing architecture

## Context

One shared orchestrator, two execution modes:
1. **Semi-integrated** (`Tests.SemiIntegrated`) — lambdas call Lib services directly (IVosPackerService, IVosBoxService, IVosVmService)
2. **CLI E2E** (future) — same orchestrator, lambdas shell out to `vos build`, `vos box add`, `vos up`, `vos ssh-command`

This ensures Lib and CLI produce identical results for the same workflow.

---

## Project structure

```
Vos/
├── test/
│   ├── FrenchExDev.Net.Vos.Testing/                    (NEW — shared orchestrator)
│   │   ├── VosIntegrationOrchestrator.cs
│   │   └── VosIntegrationStepResult.cs
│   ├── FrenchExDev.Net.Vos.Tests.SemiIntegrated/       (NEW — Lib-driven tests)
│   │   └── PackerToVosSemiIntegratedTests.cs
│   └── FrenchExDev.Net.Vos.Tests.E2E/                  (FUTURE — CLI-driven tests)
│       └── PackerToVosE2ETests.cs
```

---

## `Vos.Testing` — shared orchestrator

The orchestrator defines the workflow steps and receives lambdas for each one. It doesn't know whether it's calling Lib services or shelling out to CLI.

```csharp
public sealed class VosIntegrationOrchestrator
{
    // Step delegates — injected at construction
    private readonly Func<CancellationToken, Task<VosIntegrationStepResult>> _detectVirtualBox;
    private readonly Func<string, CancellationToken, Task<VosIntegrationStepResult>> _buildPackerImage;
    private readonly Func<string, string, CancellationToken, Task<VosIntegrationStepResult>> _registerBox;
    private readonly Func<string, CancellationToken, Task<VosIntegrationStepResult>> _initProject;
    private readonly Func<string, string, string, int, int, CancellationToken, Task<VosIntegrationStepResult>> _addMachineType;
    private readonly Func<string, string, string, int, CancellationToken, Task<VosIntegrationStepResult>> _addMachine;
    private readonly Func<string, CancellationToken, Task<VosIntegrationStepResult>> _validate;
    private readonly Func<string, CancellationToken, Task<VosIntegrationStepResult>> _upAll;
    private readonly Func<string, CancellationToken, Task<VosIntegrationStepResult>> _statusAll;
    private readonly Func<string, string, string, CancellationToken, Task<VosIntegrationStepResult>> _sshCommand;
    private readonly Func<string, CancellationToken, Task<VosIntegrationStepResult>> _destroyAll;
    private readonly Func<string, CancellationToken, Task<VosIntegrationStepResult>> _removeBox;

    public VosIntegrationOrchestrator(/* all delegates */) { ... }

    /// <summary>
    /// Runs the full Packer -> Vos lifecycle. Returns ordered list of step results.
    /// </summary>
    public async Task<IReadOnlyList<VosIntegrationStepResult>> RunAsync(
        string packerDir, string vosDir, string boxName,
        (string TypeName, string Box, int Memory, int Cpus)[] machineTypes,
        (string MachineName, string TypeName, int InstanceCount)[] machines,
        (string InstanceName, string Command, string ExpectedOutput)[] sshTests,
        CancellationToken ct = default);
}

public sealed record VosIntegrationStepResult(
    string StepName,
    bool Success,
    string? Output = null,
    string? Error = null,
    TimeSpan Duration = default);
```

### `RunAsync` workflow

1. Calls `_detectVirtualBox(ct)`
2. Calls `_buildPackerImage(packerDir, ct)`
3. Calls `_registerBox(boxPath, boxName, ct)`
4. Calls `_initProject(vosDir, ct)`
5. For each machineType: `_addMachineType(config, name, box, mem, cpus, ct)`
6. For each machine: `_addMachine(config, name, type, count, ct)`
7. Calls `_validate(config, ct)`
8. Calls `_upAll(config, ct)`
9. Calls `_statusAll(config, ct)`
10. For each sshTest: `_sshCommand(config, instance, command, ct)` — asserts `ExpectedOutput`
11. Calls `_destroyAll(config, ct)`
12. Calls `_removeBox(boxName, ct)`

Each step is timed, result collected. If any step fails, remaining steps are skipped (except cleanup which always runs).

---

## `Tests.SemiIntegrated` — Lib-driven

Constructs the orchestrator with lambdas that call Lib services:

```csharp
[Trait("Category", "SemiIntegrated")]
public class PackerToVosSemiIntegratedTests : IAsyncLifetime
{
    // DI container with real VagrantBackend, PhysicalFileSystem, VosFileReader/Writer, all Lib services

    [Fact]
    public async Task Packer_Image_To_Vos_SSH_Echo_And_Docker_Hello_World()
    {
        var orchestrator = new VosIntegrationOrchestrator(
            detectVirtualBox: async ct => {
                var ver = await new VirtualBoxSystemVersionDiscoverer().DiscoverAsync(ct);
                return new VosIntegrationStepResult("detect-vbox", true, ver.ToStringWithoutRelease());
            },
            buildPackerImage: async (dir, ct) => {
                var result = await _packerSvc.BuildAsync(dir);
                return new VosIntegrationStepResult("packer-build", result.IsSuccess);
            },
            sshCommand: async (config, instance, cmd, ct) => {
                var result = await _vmSvc.SshCommandAsync(config, instance, cmd, ct: ct);
                return new VosIntegrationStepResult($"ssh-{instance}", result.IsSuccess, result.Value?.Output);
            },
            // ... all other delegates calling Lib services
        );

        var results = await orchestrator.RunAsync(
            _packerDir, _vosDir, _boxName,
            machineTypes: [
                ("docker-host", _boxName, 2048, 2),
                ("worker", _boxName, 1024, 1)
            ],
            machines: [
                ("docker", "docker-host", 2),
                ("worker", "worker", 2)
            ],
            sshTests: [
                ("docker-01", "echo hello world", "hello world"),
                ("docker-01", "docker run --rm hello-world", "Hello from Docker!"),
                ("worker-01", "echo worker ok", "worker ok")
            ],
            ct);

        results.ShouldAllBe(r => r.Success);
    }
}
```

---

## Future `Tests.E2E` — CLI-driven (same orchestrator)

```csharp
var orchestrator = new VosIntegrationOrchestrator(
    sshCommand: async (config, instance, cmd, ct) => {
        var process = Process.Start("vos", $"ssh-command {instance} \"{cmd}\" --config {config}");
        await process.WaitForExitAsync(ct);
        return new VosIntegrationStepResult($"ssh-{instance}", process.ExitCode == 0, stdout);
    },
    // ... all other delegates shelling out to vos CLI
);
// Same RunAsync call, same assertions
```

---

## Pre-flight: VirtualBox version detection

The integration test detects the installed VirtualBox version to pass the correct Guest Additions ISO checksum to the Packer build.

Uses `FrenchExDev.Net.VirtualBox.Version` (`VirtualBox.Version/src/FrenchExDev.Net.VirtualBox.Version/`):

```csharp
// Step 0: Detect VirtualBox version + get Guest Additions checksum
var vboxVersion = await new VirtualBoxSystemVersionDiscoverer().DiscoverAsync(ct);
var vboxInfos = await new VirtualBoxVersionInformationSearcher()
    .SearchAsync(new VirtualBoxVersionInformationSearchingFilters(vboxVersion.ToStringWithoutRelease()), ct);
var guestAdditionsSha = vboxInfos.FirstOrDefault()?.AdditionsIsoSha256;
// Feed vboxVersion + guestAdditionsSha into AlpinePackerConfig
```

---

## VM topology

| Machine type | Box | Memory | CPUs | Instances |
|---|---|---|---|---|
| `docker-host` | built from Packer (Alpine + Docker) | 2048 MB | 2 | `docker-01`, `docker-02` |
| `worker` | same box, lighter config | 1024 MB | 1 | `worker-01`, `worker-02` |

4 VMs total, 2 types. Both use the same Packer-built box. `worker` is a lighter type (less RAM, no shared folders, no Docker provisioning).

---

## Test steps

| # | Phase | Action | Assert |
|---|---|---|---|
| **0** | Pre-flight | Detect VirtualBox version + Guest Additions SHA256 | VBox found, SHA obtained |
| **1** | Packer | `PackerBundle.Apply(AlpineBaseContributor, DockerContributor)` | Bundle has scripts |
| **2** | Packer | `PackerBundleWriter.WriteAsync(bundle, packerDir)` | HCL + scripts on disk |
| **3** | Packer | Build image (packer init + packer build) | .box artifact exists |
| **4** | Box | Register box in Vagrant | success |
| **5** | Config | Init Vos project | config-vos.yaml exists |
| **6** | Config | Add machine type `docker-host` (2048 MB, 2 CPUs) | type added |
| **7** | Config | Add machine type `worker` (1024 MB, 1 CPU) | type added |
| **8** | Config | Add machine `docker` (type=docker-host, 2 instances) | machine added |
| **9** | Config | Add machine `worker` (type=worker, 2 instances) | machine added |
| **10** | Config | Validate config | 0 errors, 4 instances |
| **11** | VM | Start all 4 VMs | all success |
| **12** | VM | Check status | all "running" |
| **13** | SSH | `echo hello world` on docker-01 | output contains "hello world" |
| **14** | Docker | `docker run --rm hello-world` on docker-01 | output contains "Hello from Docker!" |
| **15** | SSH | `echo worker ok` on worker-01 | output contains "worker ok" |
| **16** | Cleanup | Destroy all 4 VMs | all success |
| **17** | Cleanup | Deregister box | success |
| **18** | Cleanup | Delete .box + temp dirs | files removed |

---

## csproj dependencies

**`Vos.Testing`** (shared, no test framework dependency):
- net10.0, no project refs — pure orchestrator logic, takes delegates

**`Tests.SemiIntegrated`**:
- `Vos.Testing` (orchestrator)
- `Vos.Lib` (services)
- `Vos.Infra.Vagrant` (VagrantBackend)
- `Vos.Infra.FileSystem` (PhysicalFileSystem)
- `Vos.VosFile` (reader/writer)
- `Vos.Bundle` (VosBundleWriter)
- `Packer.Alpine` (AlpineBaseContributor, AlpinePackerConfig)
- `Packer.Alpine.DockerHost` (DockerContributor)
- `Packer.Bundle` (PackerBundle, PackerBundleWriter)
- `Vos.Alpine` (AlpineVirtualBoxContributor)
- `Vos.Alpine.DockerHost` (DockerHostContributor)
- `VirtualBox.Version` (VirtualBoxSystemVersionDiscoverer)
- `M.E.DI`, `M.E.Logging`, `xUnit`, `Shouldly`

---

## Files to create

| File | Content |
|---|---|
| `test/FrenchExDev.Net.Vos.Testing/FrenchExDev.Net.Vos.Testing.csproj` | Shared orchestrator project |
| `test/FrenchExDev.Net.Vos.Testing/VosIntegrationOrchestrator.cs` | Workflow engine with delegate steps |
| `test/FrenchExDev.Net.Vos.Testing/VosIntegrationStepResult.cs` | Step result record |
| `test/FrenchExDev.Net.Vos.Tests.SemiIntegrated/FrenchExDev.Net.Vos.Tests.SemiIntegrated.csproj` | Semi-integrated test project |
| `test/FrenchExDev.Net.Vos.Tests.SemiIntegrated/PackerToVosSemiIntegratedTests.cs` | Lib-driven test using orchestrator |

## Files to modify

| File | Change |
|---|---|
| `Vos/FrenchExDev.Net.Vos.slnx` | Add Testing + Tests.SemiIntegrated projects |

---

## Run

```pwsh
cd Net/FrenchExDev/Vos

# Unit tests only (fast, no VirtualBox needed)
dotnet test --filter "Category!=SemiIntegrated&Category!=E2E"

# Semi-integrated (requires VirtualBox + Vagrant + Packer, ~30 min)
dotnet test --filter "Category=SemiIntegrated"

# Future: CLI E2E
dotnet test --filter "Category=E2E"
```

## Prerequisites

- VirtualBox installed and `VBoxManage` on PATH (version auto-detected)
- Vagrant installed and on PATH
- Packer installed and on PATH
- Internet access (downloads Alpine ISO + VBox Guest Additions on first build)
- ~30 min runtime (Packer build ~20 min, 4x vagrant up ~10 min)
