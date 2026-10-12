# VOS-ORCHESTRATION — Architecture

## Layered project decomposition

```
Layer 0 — Foundation
  Vos                         core domain: VosConfig, VosMachineType,
                               IVosBackend, IMachineTypeContributor

Layer 1 — File + Bundle
  Vos.VosFile                 YAML reader/writer/validator, env vars,
                               local override deep-merge, file lock
  Vos.Bundle                  in-memory workspace + embedded Vagrantfile

Layer 2 — Abstractions
  Vos.Lib.Abstractions        IFile/IFileSystem, request/handler pipeline,
                               IValidationRule<T>, IUnitOfWork,
                               8 service interfaces, [Builder] option
                               classes, 140+ event records

Layer 3 — Implementations (DI-agnostic)
  Vos.Lib                     service classes, validation rules, event
                               emitter, all marked [Injectable]
  Vos.Infra.Vagrant           VagrantBackend : IVosBackend
  Vos.Infra.Podman            PodmanMachineBackend : IVosBackend
  Vos.Infra.FileSystem        PhysicalFile, PhysicalFileSystem
  Vos.Infra.PowerShell        PowerShell cmdlets

Layer 4 — Entry points
  Vos.Cli                     System.CommandLine v2

Tests
  Vos.Tests, Vos.VosFile.Tests, Vos.Bundle.Tests, Vos.Lib.Tests,
  Vos.IntegrationTests
```

No project in layer N depends on a project in layer ≥ N. The graph is a
strict DAG.

## Core domain (Layer 0)

```csharp
public sealed class VosConfig
{
    public int SchemaVersion { get; init; } = 1;
    public Dictionary<string, VosMachineType> MachineTypes { get; init; } = [];
    public List<VosInstance> Instances { get; init; } = [];
    public VosNetwork? Network { get; init; }
    public Dictionary<string, string> Variables { get; init; } = [];
}

public sealed class VosMachineType
{
    public string Box { get; set; } = "";
    public string? BoxVersion { get; set; }
    public int Memory { get; set; } = 2048;
    public int Cpus { get; set; } = 2;
    public string Provider { get; set; } = "virtualbox";
    public List<VosProvisioningStep> Provisioning { get; set; } = [];
    public List<VosSharedFolder> SharedFolders { get; set; } = [];
    public List<string> Plugins { get; set; } = [];
    public Dictionary<string, string> Variables { get; set; } = [];
    public List<VosVboxManageCommand> VboxManage { get; set; } = [];
}
```

`VosMachineType` is intentionally mutable so contributors can mutate it via
`IMachineTypeContributor.Contribute(...)`.

## VosFile reader/writer (Layer 1)

```csharp
public interface IVosFileReader
{
    Task<Result<VosConfig>>       ReadAsync(string path, CancellationToken ct);
    Task<Result<VosConfigLayers>> ReadLayersAsync(string path, CancellationToken ct);
}

public sealed record VosConfigLayers(
    VosConfig Base,
    VosConfig? Local,
    VosConfig Resolved);

public interface IVosFileWriter
{
    Task<Result> WriteAsync(VosConfig config, string path, CancellationToken ct);
}

public interface IVosFileValidator
{
    Result<IReadOnlyList<string>> Validate(VosConfig config);
}
```

The reader:

1. Loads `config-vos.yaml` (base layer).
2. Loads `local/config-vos-local.yaml` if it exists.
3. Substitutes `${ENV_VAR}` in all string values from
   `Environment.GetEnvironmentVariable`.
4. Deep-merges the local layer into the base.
5. Returns the resolved `VosConfig`.

The writer:

1. Acquires a `VosFileLock` (5s timeout, file-based via `path + ".lock"`).
2. Atomic write: serialize to temp file, fsync, rename.
3. Releases the lock.

YAML uses `UnderscoredNamingConvention`, omits nulls, and always emits
`schema_version: 1`.

## VosBundle (Layer 1)

```csharp
public sealed class VosBundle
{
    public VosConfig Config { get; set; } = new();
    public SortedList<string, VosBundleFile> Files { get; } = new();

    public IEnumerable<VosBundleFile> ProvisioningScripts =>
        Files.Values.Where(f => f.Directory == "provisioning");

    public VosBundle Apply(params IVosBundleContributor[] contributors)
    {
        foreach (var c in contributors) c.Contribute(this);
        return this;
    }

    public void AddProvisioningScript(string key, string version, string content,
                                       string extension = "sh");
    public void AddFile(string directory, string name, string content);
}

public sealed record VosBundleFile(string Directory, string FileName,
                                   string Extension, string Content);

public interface IVosBundleContributor
{
    void Contribute(VosBundle bundle);
}
```

`VosBundleWriter` materializes the bundle: writes `config-vos.yaml`,
extracts the embedded static `Vagrantfile`, writes provisioning scripts to
`provisioning/{version}/{key}.{ext}`, writes shared files.

## Service interfaces (Layer 2)

Each service has a narrow surface. `[Builder]`-driven options keep method
signatures small.

```csharp
public interface IVosMachineService
{
    Task<Result> UpAsync     (VosUpOptions      o, CancellationToken ct);
    Task<Result> HaltAsync   (VosHaltOptions    o, CancellationToken ct);
    Task<Result> DestroyAsync(VosDestroyOptions o, CancellationToken ct);
    Task<Result<VosMachineStatus>> StatusAsync(VosStatusOptions o, CancellationToken ct);
    Task<Result> SshAsync    (VosSshOptions     o, CancellationToken ct);
}

public interface IVosBoxService
{
    Task<Result> AddAsync   (VosBoxAddOptions o, CancellationToken ct);
    Task<Result> RemoveAsync(VosBoxRemoveOptions o, CancellationToken ct);
    Task<Result<IReadOnlyList<VosBox>>> ListAsync(CancellationToken ct);
}

public interface IVosNetworkService { /* ... */ }
public interface IVosSnapshotService { /* ... */ }
public interface IVosPackerService    { /* ... */ }
public interface IVosVmService        { /* low-level VM ops */ }
```

Each implementation in `Vos.Lib` is `[Injectable]`-decorated so the
auto-DI source generator emits a single registration extension.

## Backend abstraction

```csharp
public interface IVosBackend
{
    string Name { get; }
    IReadOnlySet<VosAction> SupportedActions { get; }

    Task<Result> UpAsync     (VosMachine m, VosBackendOptions o, CancellationToken ct);
    Task<Result> HaltAsync   (VosMachine m, VosBackendOptions o, CancellationToken ct);
    Task<Result> DestroyAsync(VosMachine m, VosBackendOptions o, CancellationToken ct);
    Task<Result<VosMachineStatus>> StatusAsync(VosMachine m, CancellationToken ct);
    Task<Result> SshAsync    (VosMachine m, string command, CancellationToken ct);

    Task<Result> SnapshotSaveAsync   (VosMachine m, string name, CancellationToken ct);
    Task<Result> SnapshotRestoreAsync(VosMachine m, string name, CancellationToken ct);
    Task<Result> SnapshotDeleteAsync (VosMachine m, string name, CancellationToken ct);
}

public enum VosAction { Up, Halt, Destroy, Status, Ssh, Snapshot, Provision, Reload, /* ... */ }
```

Implementations:

| Backend | Project | Underlying |
|---------|---------|-----------|
| `VagrantBackend` | `Vos.Infra.Vagrant` | `FrenchExDev.Net.Vagrant` (BinaryWrapper) |
| `PodmanMachineBackend` | `Vos.Infra.Podman` | `FrenchExDev.Net.Podman` (BinaryWrapper) |

Both delegate to a typed CLI client; neither shells out manually.

## Event system

```csharp
public interface IVosEventEmitter
{
    void Emit(VosEvent evt);
}

public abstract record VosEvent(Guid CorrelationId, DateTimeOffset At);
public sealed record MachineUpRequested(Guid CorrelationId, DateTimeOffset At, string Name) : VosEvent(...);
public sealed record MachineUpStarted  (Guid CorrelationId, DateTimeOffset At, string Name, string Backend) : VosEvent(...);
public sealed record MachineUpCompleted(Guid CorrelationId, DateTimeOffset At, string Name, TimeSpan Duration) : VosEvent(...);
public sealed record BackendCommandFailed(Guid CorrelationId, DateTimeOffset At, string Backend, string Command, string Error) : VosEvent(...);
// 140+ records
```

Subscribers: console writer (CLI), recording emitter (tests), `WriteVerbose`
adapter (PowerShell), structured logger (production).

## CLI architecture

`Vos.Cli` uses System.CommandLine v2 with the Symbols/Throw/Resolver/Command
pattern:

```
Symbols    — Option<T> / Argument<T> definitions
Throw      — composes options into a strongly-typed request DTO
Resolver   — looks up services from DI
Command    — invokes the Lib service, binds events to Console
```

Each subcommand is its own class. Completers are registered for
`--machine`, `--provider`, `--snapshot`, etc.

## PowerShell architecture

`Vos.Infra.PowerShell` exposes cmdlets like `Get-VosMachine`, `Start-VosMachine`,
`Stop-VosMachine`. Cmdlets:

- Inject Lib services via constructor (no `Activator.CreateInstance`)
- Bind `IVosEventEmitter` to `WriteVerbose` / `WriteProgress`
- Use the same option records as the CLI
- Ship a `gvm` alias for muscle-memory compatibility with the legacy PoSh
  module

## Validation pipeline

Validation runs at three points:

1. **Schema validation** during YAML deserialization (rejects malformed
   structure).
2. **Static validation** via `IVosFileValidator` (semantic rules: machine
   names unique, IP ranges valid, provisioning scripts exist on disk).
3. **Pre-flight validation** before invoking a backend (capability check
   against `IVosBackend.SupportedActions`).

Failures return `Result.Failure(ValidationResult)` and emit a
`ValidationFailed` event.

## Tests

| Project | Coverage |
|---------|----------|
| `Vos.Tests` | Core domain (config, contributors, validators) |
| `Vos.VosFile.Tests` | Reader, writer, validator, env var resolver, layer merging |
| `Vos.Bundle.Tests` | VosBundle composition, contributors, writer |
| `Vos.Lib.Tests` | Service classes with hand-written fakes for every interface |
| `Vos.IntegrationTests` | E2E: build image → up → ssh → destroy |
