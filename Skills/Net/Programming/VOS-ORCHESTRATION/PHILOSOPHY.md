# VOS-ORCHESTRATION — Philosophy

A "VM orchestration" library in this monorepo provides a typed, testable,
backend-agnostic surface over hypervisor lifecycle tools (Vagrant, Podman
Machine, Multipass, etc.). It is the run-time counterpart to a packer-bundle
(which produces images) — this skill describes how those images become
running VMs, configured by a typed C# object model.

The pattern is named after Vos ("Vagrant on Steroids") but is generic. Apply
it any time you need to wrap a fleet of "spin up a VM and SSH into it" tools
behind one C# API.

## YAML config is the user contract; C# config is the truth

Users edit a `.vosfile` YAML at the project root. The library parses it into
a strongly-typed `VosConfig` graph and operates on that. The YAML is a
serialization format; the C# graph is the canonical model.

This means:

- A schema upgrade is a C# refactor + a serializer change, not a YAML hand-edit.
- Validation runs against the C# graph (deserialization rejects malformed
  YAML before any business logic sees it).
- Tools that need to read or write the file (CLI, PowerShell cmdlets, IDE
  plugins) all share one model.

The file always carries a `schema_version: 1` field reserved for future
evolution.

## Layered config: base + local override

```
config-vos.yaml                ← base config (git tracked, shared)
local/config-vos-local.yaml    ← local override (gitignored, personal)
─────────────────────────────
= resolved config              ← what the backend sees (deep-merged)
```

The reader always returns the **resolved** (merged) config but exposes the
layers independently for diffing and per-layer mutation. CLI commands that
mutate take a `--local` flag to target the local layer instead of the base.

The Vagrantfile or equivalent backend artifact deep-merges these two YAML
files at runtime as well, so the host-side and guest-side views agree.

## Backend abstraction with capability discovery

```csharp
public interface IVosBackend
{
    string Name { get; }
    IReadOnlySet<VosAction> SupportedActions { get; }

    Task<Result> UpAsync(VosMachine machine, CancellationToken ct);
    Task<Result> HaltAsync(VosMachine machine, CancellationToken ct);
    Task<Result> DestroyAsync(VosMachine machine, CancellationToken ct);
    Task<Result<VosMachineStatus>> StatusAsync(VosMachine machine, CancellationToken ct);
    Task<Result> SshAsync(VosMachine machine, string command, CancellationToken ct);
    // snapshots, port-forwarding, ...
}
```

Implementations exist for Vagrant, Podman Machine, and any other backend the
ecosystem grows. They differ in capabilities — Podman Machine has no
`reload` analogue, no provisioning, only some snapshot operations. Hence
`SupportedActions`: callers MUST check before invoking, and any backend that
silently ignores an unsupported action **violates Liskov**. Returning a clean
`Result.Failure(NotSupported)` is the only acceptable behavior for declared-
unsupported actions.

## SOLID services, one method per interface

The library decomposes into thin, single-method services:

| Interface | Method |
|-----------|--------|
| `IVosFileReader` | `ReadAsync(path, ct) → Result<VosConfig>` |
| `IVosFileWriter` | `WriteAsync(config, path, ct) → Result` |
| `IVosFileValidator` | `Validate(config) → Result<IReadOnlyList<string>>` |
| `IVosMachineService` | `UpAsync`, `HaltAsync`, `DestroyAsync`, `StatusAsync` (one method per command) |
| `IVosBoxService` | `AddAsync`, `RemoveAsync`, `ListAsync` |
| `IVosNetworkService` | `AssignIpAsync`, `ReleaseIpAsync` |
| `IVosSnapshotService` | `SaveAsync`, `RestoreAsync`, `DeleteAsync` |
| `IVosPackerService` | `BuildImageAsync` |

This is the SOLID skill applied uniformly. Each service has one reason to
change. Hand-written `Fakes` in `test/.../Fakes/` make tests trivial. No
mocking framework.

## DI-agnostic, container-agnostic

Services accept dependencies as **optional** constructor params with
sensible defaults. No DI container is required. Production callers either
construct services directly or use the auto-generated registration extension
from `[Injectable]`:

```csharp
services.AddFrenchExDevNetVosLibInjectables();   // SG-generated
```

Tests construct services directly with hand-written fakes.

## Options as `[Builder]`-driven option records

Every service method that takes more than two parameters takes an option
record built with `[Builder]`. The source generator emits the fluent builder.
This avoids parameter explosion and gives every option a single, validated
construction site.

```csharp
[Builder]
public sealed class VosUpOptions
{
    public string? MachineName { get; init; }
    public string? Provider { get; init; }
    public bool? Provision { get; init; }
    public bool? DestroyOnError { get; init; }
    public Dictionary<string, string> EnvironmentOverrides { get; init; } = [];
}
```

There are two SG-imposed quirks worth knowing:

- All `bool` properties must be `bool?` (the SG cannot tell whether `false`
  was explicitly set or defaulted).
- Dictionary properties must be the concrete `Dictionary<,>` type, not
  `IReadOnlyDictionary<,>` (the SG cannot assign through the readonly
  indexer).

## Machine types as composable contributors

A "machine type" is a named, reusable VM template (e.g. `alpine-virtualbox`,
`docker-host`, `gitlab-runner`). Machine types are not classes; they are
**values produced by contributors**:

```csharp
public interface IMachineTypeContributor
{
    void Contribute(VosMachineType machineType);
}

var machineType = new VosMachineType();
new AlpineVirtualBoxContributor("3.21").Contribute(machineType);
new DockerHostContributor().Contribute(machineType);
```

This is the same contributor pattern as compose-bundles and packer-bundles.
Contributors compose by mutation; ordering matters where it matters
(`DockerHostContributor` expects an Alpine base). They are non-destructive
by default (`Box ??= "frenchexdev/alpine-3.21-virt"`, `Plugins.Contains()`
before adding).

## Event-driven observability

Every service emits events through `IVosEventEmitter`. Events are records
with a `CorrelationId` so a CLI run, a PowerShell cmdlet invocation, and a
test can all trace the full chain of operations. There are 140+ event
records covering every state transition: `MachineUpRequested`,
`MachineUpStarted`, `BoxResolved`, `ProvisioningScriptExecuted`,
`SnapshotSaved`, `BackendCommandFailed`, etc.

The CLI binds events to console output. Tests bind them to a recording
emitter for assertions. PowerShell cmdlets bind them to `WriteVerbose` /
`WriteProgress`. Production tooling binds them to structured logging.

## Multiple front ends

The library is consumed by:

- A System.CommandLine v2 CLI (`vos`) on every platform
- PowerShell cmdlets (`gvm` alias) on Windows for legacy PoSh users
- Tests (xUnit + Shouldly + hand-written fakes)
- Higher-level orchestrators (HomeLab) that drive Vos as one tool among many

The Lib is the single source of behavior; front ends are thin adapters.

## What this is not

- Not a hypervisor abstraction. The backends ARE Vagrant and Podman Machine.
  We do not invent a new VM API.
- Not a configuration management tool. Provisioning steps shell out to the
  backend's provisioner; the library does not run them itself.
- Not a cloud abstraction. It is a developer-host tool for local VMs.
- Not opinionated about images. Any Vagrant box or Podman Machine image
  works. Image production belongs to a packer-bundle.
