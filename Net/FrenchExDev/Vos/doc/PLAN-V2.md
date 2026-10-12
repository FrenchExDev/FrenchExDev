# Plan: Extract Vos.Lib from Vos.Cli/Program.cs

## Context

`Program.cs` (844 lines) is a monolith mixing CLI plumbing, workflow orchestration, and console output. The domain classes are well-factored but the application layer lives in handler lambdas. Additionally, `IVosExecutionService` would be too fat: it mixes Vagrant VM lifecycle, Packer image building, box management, SSH, and snapshots.

Goal: create a SOLID application service layer with proper event pub/sub, DI registration, and clean separation between Packer workflows and Vagrant workflows.

### Key decisions

| Question | Decision |
|---|---|
| YAML schema | **Nouveau schema C#** + nouveau Vagrantfile Ruby adapté. Rupture avec PoSh. |
| Resilience | **Polly** (Microsoft.Extensions.Resilience v8). Standard industrie. |
| Config cache | **Full cache + FileSystemWatcher**. PowerShell module reste chargé longtemps. |
| Phasing | **Full Lib d'abord, CLI après**. Phase 1: Lib complet (pipeline, UoW, specs, resilience, events, cache) + tests. Phase 2: CLI + completers + PowerShell. |

---

## 1. Project table + dependency graph

### All projects

| # | Project | Status | Responsibility |
|---|---|---|---|
| 1 | `FrenchExDev.Net.Vos` | existing | Core domain: VosConfig, VosMachineType, VosConfigManager, VosOrchestrator, VosConfigMerger, VosConfigValidator, NetworkGenerator, IVosBackend, IMachineTypeContributor |
| 2 | `FrenchExDev.Net.Vos.VosFile` | **NEW** | YAML schema reader/writer/validator for config-vos.yaml. Env var substitution (`${VAR}`), local override deep-merge, file locking |
| 3 | `FrenchExDev.Net.Vos.Bundle` | **NEW** | In-memory `VosBundle` workspace (like PackerBundle). Contains full config + provisioning scripts + shared files. All `[Builder]`-driven. `IVosBundleContributor` pattern. `VosBundleWriter` materializes to disk |
| 4 | `FrenchExDev.Net.Vos.Lib.Abstractions` | **NEW** | `IFile`/`IFileSystem` IO abstractions, `IVosRequest`/`IVosHandler`/`IVosBehavior` pipeline, `IValidationRule<T>` specs, `IVosUnitOfWork`, `IVosConfigCache`, service interfaces, VosEvent hierarchy (with CorrelationId), IVosEventEmitter, [Builder] option classes |
| 5 | `FrenchExDev.Net.Vos.Lib` | **NEW** | Handlers, pipeline behaviors, VosEventEmitter, VosUnitOfWork, VosConfigCache, completers, `LoggerMessage`. All service classes use `[Injectable]` → SG auto-generates `AddFrenchExDevNetVosLibInjectables(this IServiceCollection)`. **No hand-written DI project.** |
| 6 | `FrenchExDev.Net.Vos.Cli` | existing, rewrite | Thin DI wrapper over Lib. System.CommandLine + AddCompletions |
| 7 | `FrenchExDev.Net.Vos.Infra.Vagrant` | existing, update | VagrantBackend : IVosBackend. Fix bugs, add option record support |
| 8 | `FrenchExDev.Net.Vos.Infra.FileSystem` | existing, repurposed | `IFile` implementation: real filesystem I/O (`PhysicalFile`, `PhysicalFileSystem`) |
| 9 | `FrenchExDev.Net.Vos.Infra.PowerShell` | existing, update | PowerShell cmdlets. Add new completers, inject Lib services |
| 10 | `FrenchExDev.Net.Vos.Tests` | existing | Tests for core domain (VosConfigManager, Merger, Validator) |
| 11 | `FrenchExDev.Net.Vos.VosFile.Tests` | **NEW** | Tests for reader, writer, validator, env var resolver |
| 12 | `FrenchExDev.Net.Vos.Bundle.Tests` | **NEW** | Tests for VosBundle, contributors, writer |
| 13 | `FrenchExDev.Net.Vos.Lib.Tests` | **NEW** | Unit tests for all Lib services with fakes |
| 14 | `FrenchExDev.Net.Vos.IntegrationTests` | **NEW** | E2E: Packer Alpine DockerHost → Vos up → SSH → destroy |

### Dependency graph (no cycles)

```
Layer 0 — Foundation (no project deps)
  [1] Vos ──→ Result

Layer 1 — File + Bundle (depend on Vos only)
  [2] Vos.VosFile ──→ Vos, YamlDotNet, Builder, Builder.Attributes, Builder.SG
  [3] Vos.Bundle ──→ Vos, Builder, Builder.Attributes, Builder.SG

Layer 2 — Abstractions (depend on Layer 0-1)
  [4] Vos.Lib.Abstractions ──→ Vos, Result, Builder, Builder.Attributes, Builder.SG, M.E.Logging.Abstractions

Layer 3 — Implementations (depend on Layer 0-2, DI-agnostic)
  [5] Vos.Lib ──→ Lib.Abstractions, Vos.VosFile, Vos.Bundle, Injectable.Attributes, Injectable.Microsoft.SG, M.E.Logging.Abstractions, M.E.Resilience (Polly), System.Diagnostics.DiagnosticSource
  [7] Vos.Infra.Vagrant ──→ Vos, Vagrant (BinaryWrapper client), Injectable.Attributes, Injectable.Microsoft.SG
  [8] Vos.Infra.FileSystem ──→ Lib.Abstractions, Injectable.Attributes, Injectable.Microsoft.SG
  [9] Vos.Infra.PowerShell ──→ Lib.Abstractions, PowerShellStandard.Library

Layer 4 — Entry points (depend on Layer 3)
  [6] Vos.Cli ──→ Lib, Infra.Vagrant, Infra.FileSystem, M.E.DI, M.E.Configuration, M.E.Configuration.Json, M.E.Logging, System.CommandLine

Layer T — Tests
  [10] Vos.Tests ──→ Vos
  [11] Vos.VosFile.Tests ──→ Vos.VosFile
  [12] Vos.Bundle.Tests ──→ Vos.Bundle
  [13] Vos.Lib.Tests ──→ Lib, Lib.Abstractions
  [14] Vos.IntegrationTests ──→ Lib, Infra.Vagrant, Packer.Alpine.DockerHost, Vos.Alpine.DockerHost
```

### Cycle check

No project in Layer N depends on any project in Layer N or higher. `VosFile` and `Bundle` are siblings at Layer 1 with no dependency on each other. `Lib` depends on both but neither depends on `Lib`. Clean DAG.

---

## 1b. `Vos.VosFile` — YAML schema reader/writer/validator

**Path**: `Vos/src/FrenchExDev.Net.Vos.VosFile/`

Replaces `Vos.Infra.FileSystem` (which is deprecated). Single responsibility: read, write, validate `config-vos.yaml`.

### Classes

| Class | Responsibility |
|---|---|
| `IVosFileReader` | `ReadAsync(path, ct)` → `Result<VosConfig>`. Env var substitution, local override deep-merge |
| `IVosFileWriter` | `WriteAsync(config, path, ct)`. Atomic write with file locking |
| `IVosFileValidator` | `Validate(config)` → `Result<IReadOnlyList<string>>`. Schema + semantic validation |
| `VosFileReader` | Impl: detect v1/v2, deserialize, merge `local/config-vos-local.yaml`, resolve `${ENV_VAR}` |
| `VosFileWriter` | Impl: serialize with `UnderscoredNamingConvention`, omit nulls, set `schema_version: 1` |
| `VosFileValidator` | Impl: all current VosConfigValidator rules + provisioning script existence + IP validity + no duplicate names |
| `VosFileLock` | File-based lock: `configPath + ".lock"`, 5s timeout, `IDisposable` |

### Schema versioning

```csharp
public static class VosSchemaVersion
{
    public const int Current = 1; // C# format: machine_types, flat, underscore naming
}
```

The `schema_version` field is reserved for future evolution. v1 = the C# format defined here.

### Env var substitution

Before deserialization, scan all string values for `${VAR_NAME}` patterns and replace from `Environment.GetEnvironmentVariable()`. Unresolved → emit `SecretNotFound` event, return `Result.Failure`.

---

## 1c. `Vos.Bundle` — in-memory Vos workspace

**Path**: `Vos/src/FrenchExDev.Net.Vos.Bundle/`

Like `PackerBundle`: a mutable in-memory workspace that holds the complete Vos project (config + all provisioning scripts + shared files + static Vagrantfile). Used to **compose** a full Vos project from contributors, then **materialize** to disk.

### Core types

```csharp
/// <summary>
/// Mutable in-memory workspace for a complete Vos project.
/// </summary>
public sealed class VosBundle
{
    public VosConfig Config { get; set; } = new();
    public SortedList<string, VosBundleFile> Files { get; } = new();

    // Convenience accessors
    public IEnumerable<VosBundleFile> ProvisioningScripts => Files.Values.Where(f => f.Directory == "provisioning");
    public IEnumerable<VosBundleFile> SharedFiles => Files.Values.Where(f => f.Directory == "files");

    // Contributor pipeline (same pattern as PackerBundle.Apply)
    public VosBundle Apply(params IVosBundleContributor[] contributors)
    {
        foreach (var c in contributors) c.Contribute(this);
        return this;
    }

    // File management
    public void AddProvisioningScript(string key, string version, string content, string extension = "sh");
    public void AddFile(string directory, string name, string content);
}

/// <summary>
/// A file in the bundle (provisioning script, config file, etc.).
/// </summary>
public sealed record VosBundleFile(string Directory, string FileName, string Extension, string Content);

/// <summary>
/// Contributor pattern — composable Vos project configuration.
/// </summary>
public interface IVosBundleContributor
{
    void Contribute(VosBundle bundle);
}
```

### VosBundleWriter

```csharp
public sealed class VosBundleWriter
{
    /// <summary>
    /// Materializes the bundle to disk:
    /// - config-vos.yaml (via VosFileWriter)
    /// - Vagrantfile (static, embedded resource)
    /// - provisioning/{version}/{key}.{ext} (all scripts)
    /// - files/* (shared files)
    /// </summary>
    public async Task WriteAsync(VosBundle bundle, string outputDir, CancellationToken ct = default);
}
```

### Builder-driven config construction

All config types in the bundle use `[Builder]`:

```csharp
[Builder]
public sealed class VosBundleMachineType
{
    public string Box { get; init; } = "";
    public string? BoxVersion { get; init; }
    public int Memory { get; init; } = 2048;
    public int Cpus { get; init; } = 2;
    public int VideoMemory { get; init; } = 64;
    public bool Gui { get; init; }
    public string Provider { get; init; } = "virtualbox";
    public string? ProvisioningPath { get; init; }
    public List<VosBundleProvisioningStep> Provisioning { get; init; } = [];
    public List<VosBundleSharedFolder> SharedFolders { get; init; } = [];
    public List<string> Plugins { get; init; } = [];
    public Dictionary<string, string> Variables { get; init; } = [];
    public List<VosBundleVboxManageCommand> VboxManage { get; init; } = [];
}

[Builder]
public sealed class VosBundleInstance
{
    public string Name { get; init; } = "";
    public string? Hostname { get; init; }
    public List<VosBundleNetworkInterface> Networking { get; init; } = [];
    public int? Memory { get; init; }
    public int? Cpus { get; init; }
}

[Builder]
public sealed class VosBundleProvisioningStep
{
    public string Key { get; init; } = "";
    public string? Version { get; init; }
    public string Extension { get; init; } = "sh";
    public bool Enabled { get; init; } = true;
    public bool Privileged { get; init; } = true;
    public bool ReloadBefore { get; init; }
    public bool ReloadAfter { get; init; }
    public Dictionary<string, string> Env { get; init; } = [];
}

[Builder]
public sealed class VosBundleSharedFolder
{
    public string HostPath { get; init; } = "";
    public string GuestPath { get; init; } = "";
    public string? Type { get; init; }
    public bool Disabled { get; init; }
}

public sealed record VosBundleNetworkInterface(string Kind, string? Ip, string? Mac, string? NetworkBridge);
public sealed record VosBundleVboxManageCommand(List<string> Args);
```

### Usage — how Lib uses Bundle

```csharp
// In VosProjectService.InitAsync:
var bundle = new VosBundle();
bundle.Apply(contributors);  // e.g., DockerHostContributor
await new VosBundleWriter().WriteAsync(bundle, outputDir);

// In VosMachineTypeService — contributor adds scripts + config:
public class DockerHostBundleContributor : IVosBundleContributor
{
    public void Contribute(VosBundle bundle)
    {
        // Add machine type
        bundle.Config.MachineTypes["docker-host"] = new VosMachineType { ... };

        // Add provisioning script content
        bundle.AddProvisioningScript("install-docker", "1.0",
            "#!/bin/sh\nset -eux\napk add docker docker-cli-compose\nrc-update add docker\nservice docker start");
    }
}
```

### Relationship: VosFile vs Bundle

| | VosFile | Bundle |
|---|---|---|
| **What** | Reads/writes YAML layers (base + local override) | In-memory workspace: config + all files |
| **When** | Runtime: load, mutate, save | Composition: build a new project from scratch |
| **Analogy** | `VosConfigSerializer` (layered read/write) | `PackerBundle` (compose + materialize) |
| **Used by** | Lib services (load/save on every operation) | `InitAsync`, contributors, integration tests |

### Config layering — `config-vos.yaml` + `local/config-vos-local.yaml`

The Vagrantfile deep-merges two YAML files at runtime. VosFile must model this as first-class:

```
config-vos.yaml                    ← base config (git tracked, shared)
local/config-vos-local.yaml        ← local override (gitignored, personal)
────────────────────────────────
= resolved config                  ← what Vagrant sees (deep-merged)
```

**VosFile reader** always returns the **resolved** (merged) config. But it also exposes the layers independently for diffing and management.

```csharp
// In VosFile
public interface IVosFileReader
{
    /// <summary>Returns the deep-merged (resolved) config.</summary>
    Task<Result<VosConfig>> ReadAsync(string configPath, CancellationToken ct = default);

    /// <summary>Returns base + local layers separately for inspection.</summary>
    Task<Result<VosConfigLayers>> ReadLayersAsync(string configPath, CancellationToken ct = default);
}

public sealed record VosConfigLayers(
    VosConfig Base,           // config-vos.yaml
    VosConfig? Local,         // local/config-vos-local.yaml (null if not present)
    VosConfig Resolved);      // deep-merged result
```

**VosFile writer** writes to a specific layer:

```csharp
public interface IVosFileWriter
{
    Task WriteAsync(VosConfig config, string path, CancellationToken ct = default);
    Task WriteLocalAsync(VosConfig localOverrides, string configPath, CancellationToken ct = default);
}
```

### `--local` flag on all config-mutating commands

A global `--local` option on every config-mutating command. When set, the mutation targets `local/config-vos-local.yaml` instead of `config-vos.yaml`. If the local file doesn't exist, it's created automatically (+ `local/` added to `.gitignore`).

```csharp
var localOption = new Option<bool>("--local") { Description = "Write to local override (gitignored) instead of base config" };
```

All `IConfigMutating` pipeline requests carry a `bool Local` property. The `SaveBehavior` checks it and calls `IVosFileWriter.WriteAsync` or `IVosFileWriter.WriteLocalAsync` accordingly.

| Existing command | With --local |
|---|---|
| `vos type set docker-host --memory 4096` | writes to `config-vos.yaml` |
| `vos type set docker-host --memory 4096 --local` | writes to `local/config-vos-local.yaml` |
| `vos instance add main main-01 --ip 192.168.56.99 --local` | IP override in local layer |
| `vos network generate --local` | generated IPs go to local layer |

Read commands (`show`, `list`, `resolve`, `config show`) always return the **resolved** (merged) config — no `--local` flag needed.

### What goes in local (examples)

```yaml
# local/config-vos-local.yaml — gitignored
machines:
  main:
    instances:
      - name: main-01
        networking:
          - kind: private
            ip: "192.168.56.99"     # override IP for my machine

machine_types:
  docker-host:
    base:
      ram_mb: 4096                  # I have more RAM than the team default
      provisioning:
        install-tools:
          env:
            GITLAB_RUNNER_TOKEN: "${MY_TOKEN}"   # personal secret
```

### Bundle awareness

`VosBundleWriter` creates both layers when materializing:
- `config-vos.yaml` — the shared base config from the bundle
- `local/config-vos-local.yaml` — only if the bundle has local overrides (via `VosBundle.LocalOverrides`)
- `local/.gitignore` — with `*` content (everything in `local/` is gitignored)

```csharp
public sealed class VosBundle
{
    public VosConfig Config { get; set; } = new();
    public VosConfig? LocalOverrides { get; set; }  // optional local layer
    // ... rest unchanged
}
```

---

## 2. `Vos.Lib.Abstractions` — all abstractions

### IO abstraction: `IFile` / `IFileSystem`

All projects work against these abstractions — never `System.IO` directly. This enables in-memory fakes for testing.

```csharp
public interface IFile
{
    string Name { get; }
    string Extension { get; }
    string FullPath { get; }
    Task<string> ReadContentAsync(CancellationToken ct = default);
    Task WriteContentAsync(string content, CancellationToken ct = default);
    bool Exists { get; }
}

public interface IFileSystem
{
    IFile GetFile(string path);
    IEnumerable<IFile> GetFiles(string directory, string pattern = "*");
    void CreateDirectory(string path);
    bool DirectoryExists(string path);
    void DeleteDirectory(string path, bool recursive = false);
    void DeleteFile(string path);
}
```

**Implementations**:
- `Infra.FileSystem` → `PhysicalFile : IFile`, `PhysicalFileSystem : IFileSystem` (real disk I/O)
- `Lib.Tests/Fakes` → `InMemoryFile : IFile`, `InMemoryFileSystem : IFileSystem` (in-memory for unit tests)

**Injection**: all services receive `IFileSystem` via DI. CLI registers `PhysicalFileSystem`. Tests register `InMemoryFileSystem`.

### Event pub/sub: `IVosEventEmitter`

Follows the existing `IResultCollector` pattern but adds typed subscription:

```csharp
public interface IVosEventEmitter
{
    void Emit(VosEvent evt);
    IDisposable Subscribe(Action<VosEvent> handler);
    IDisposable Subscribe<T>(Action<T> handler) where T : VosEvent;
}
```

Multiple subscribers (CLI output, logging, test assertions). `Subscribe<T>` filters by event type.

### SOLID service interfaces — split by domain

| Interface | Responsibility | Depends on backend? |
|---|---|---|
| `IVosProjectService` | init, validate, resolve, config show, version | No |
| `IVosMachineTypeService` | type add/remove/list/show/set + VBoxManage CRUD | No |
| `IVosMachineService` | machine add/remove/list/enable/disable | No |
| `IVosInstanceService` | instance add/remove/list | No |
| `IVosNetworkService` | network generate/show | No |
| `IVosVmService` | VM lifecycle (up/halt/destroy/reload/provision/status/suspend/resume), SSH/remote, snapshots, port, package | Yes (`IVosBackend`) |
| `IVosBoxService` | box list/add/remove/update/prune/outdated/repackage | Yes (`IVosBackend`) |
| `IVosPackerService` | box init, box build (Packer integration) | Yes (`IVosBackend`) |

Split rationale: Packer workflow (image build) != Vagrant workflow (VM lifecycle) != Box management (registry). Each has different dependencies and test strategies.

### `VosServiceBase` (abstract)

```csharp
public abstract class VosServiceBase(IVosConfigSerializer serializer, IVosEventEmitter emitter)
{
    protected Task<Result<VosConfig>> LoadConfigAsync(string path, CancellationToken ct = default);
    protected Task<Result<(VosConfigManager Mgr, string Path)>> LoadManagerAsync(string path, CancellationToken ct = default);
    protected Task SaveAsync(VosConfig config, string path, CancellationToken ct = default);
    protected void Emit(VosEvent evt);
}
```

### Supporting types

```csharp
[Builder]
public sealed class MachineTypeSettings
{
    public int? Memory { get; init; }
    public int? Cpus { get; init; }
    public int? VideoMemory { get; init; }
    public bool NestedVirt { get; init; }
    public bool SataSsd { get; init; }
    public bool Gui { get; init; }
    public string? NicPromisc { get; init; }
    public bool NoLinkedClones { get; init; }
}

public sealed class VosLibOptions
{
    public string DefaultConfigPath { get; set; } = "config-vos.yaml";
    public string DefaultSubnet { get; set; } = "192.168.56.0/24";
    public int DefaultStartAt { get; set; } = 10;
}
```

---

## 3. Leaf command reference — full Vagrant/Packer option surface

Every Vos proxy command must expose ALL relevant Vagrant/Packer options. Current `IVosBackend` is too restrictive and `VagrantBackend` has bugs (upload ignores source/destination, snapshots ignore name, box add/remove ignore name).

**Convention**: `--config` is always implicit on config-aware commands (not repeated in tables). Common Vagrant debug flags (`--no-color`, `--machine-readable`, `--debug`, `--timestamp`) are handled at backend level, not exposed in Vos CLI.

### VM lifecycle (IVosVmService)

| CLI command | Arguments | Vagrant-mapped options | Service method |
|---|---|---|---|
| `up` | `<name>` | `--provision` / `--no-provision`, `--provision-with <list>`, `--destroy-on-error` / `--no-destroy-on-error`, `--parallel` / `--no-parallel`, `--provider <name>`, `--install-provider` / `--no-install-provider` | `UpAsync(configPath, name, VosUpOptions?)` |
| `halt` | `<name>` | `--force` | `HaltAsync(configPath, name, force?)` |
| `destroy` | `<name>` | `--force`, `--graceful`, `--parallel` / `--no-parallel` | `DestroyAsync(configPath, name, VosDestroyOptions?)` |
| `reload` | `<name>` | `--provision` / `--no-provision`, `--provision-with <list>` | `ReloadAsync(configPath, name, VosProvisionOptions?)` |
| `provision` | `<name>` | `--provision-with <list>` | `ProvisionAsync(configPath, name, string[]? provisionWith)` |
| `status` | | | `StatusAsync(configPath)` |
| `suspend` | `<name>` | | `SuspendAsync(configPath, name)` |
| `resume` | `<name>` | `--provision` / `--no-provision`, `--provision-with <list>` | `ResumeAsync(configPath, name, VosProvisionOptions?)` |

#### Option classes — `[Builder]` source-generated

All option types use `[Builder]` → SG generates `With*()` fluent methods, per-property `Validate*()`, `BuildAsync() → Result<Reference<T>>`. Validation catches conflicting flags (e.g., `Provision = true` + `ProvisionWith` empty list).

```csharp
[Builder]
public sealed class VosUpOptions
{
    public bool? Provision { get; init; }            // --provision / --no-provision
    public string[]? ProvisionWith { get; init; }    // --provision-with shell,puppet
    public bool? DestroyOnError { get; init; }       // --destroy-on-error / --no-destroy-on-error
    public bool? Parallel { get; init; }             // --parallel / --no-parallel
    public string? Provider { get; init; }           // --provider virtualbox
    public bool? InstallProvider { get; init; }      // --install-provider / --no-install-provider
}

[Builder]
public sealed class VosDestroyOptions
{
    public bool Force { get; init; }                 // --force
    public bool Graceful { get; init; }              // --graceful
    public bool? Parallel { get; init; }             // --parallel / --no-parallel
}

[Builder]
public sealed class VosProvisionOptions
{
    public bool? Provision { get; init; }            // --provision / --no-provision
    public string[]? ProvisionWith { get; init; }    // --provision-with
}
```

CLI usage:
```csharp
var options = await new VosUpOptionsBuilder()
    .WithProvision(true)
    .WithProvisionWith(["shell", "puppet"])
    .WithProvider("virtualbox")
    .BuildAsync();
// options is Result<Reference<VosUpOptions>>
```

### SSH / Remote access (IVosVmService)

| CLI command | Arguments | Vagrant-mapped options | Service method |
|---|---|---|---|
| `ssh` | `<name>` | `--plain`, `--no-tty` | `SshAsync(configPath, name, VosSshOptions?)` |
| `ssh-command` | `<name>` `<command>` | `--no-tty` | `SshCommandAsync(configPath, name, command, noTty?)` |
| `ssh-config` | `<name>` | `--host <name>` | `SshConfigAsync(configPath, name, host?)` |
| `upload` | `<name>` `<source>` `<destination>` | `--temporary`, `--compress`, `--compression-type <type>` | `UploadAsync(configPath, name, source, destination, VosUploadOptions?)` |
| `port` | `<name>` | `--guest <port>` | `PortAsync(configPath, name, guest?)` |
| `package` | `<name>` | `--output <file>`, `--include <files>`, `--vagrantfile <file>`, `--info <file>`, `--base <name>` | `PackageAsync(configPath, name, VosPackageOptions?)` |
| `rdp` | `<name>` | | `RdpAsync(configPath, name)` |
| `powershell` | `<name>` | `--command <cmd>`, `--elevated` | `PowershellAsync(configPath, name, command?, elevated?)` |
| `winrm` | `<name>` | `--command <cmd>`, `--elevated`, `--shell <type>` | `WinrmAsync(configPath, name, command?, VosWinrmOptions?)` |
| `winrm-config` | `<name>` | `--host <name>` | `WinrmConfigAsync(configPath, name, host?)` |

#### Option records

```csharp
[Builder]
public sealed class VosSshOptions
{
    public bool Plain { get; init; }
    public bool NoTty { get; init; }
}

[Builder]
public sealed class VosUploadOptions
{
    public bool Temporary { get; init; }
    public bool Compress { get; init; }
    public string? CompressionType { get; init; }
}

[Builder]
public sealed class VosPackageOptions
{
    public string? Output { get; init; }
    public string? Include { get; init; }
    public string? Vagrantfile { get; init; }
    public string? Info { get; init; }
    public string? Base { get; init; }
}

[Builder]
public sealed class VosWinrmOptions
{
    public bool Elevated { get; init; }
    public string? Shell { get; init; }
}
```

### Snapshots (IVosVmService)

| CLI command | Arguments | Vagrant-mapped options | Service method |
|---|---|---|---|
| `snapshot save` | `<name>` `<snapshot-name>` | `--force` | `SnapshotSaveAsync(configPath, name, snapshotName, force?)` |
| `snapshot restore` | `<name>` `<snapshot-name>` | `--no-start`, `--no-provision`, `--provision-with <list>` | `SnapshotRestoreAsync(configPath, name, snapshotName, VosSnapshotRestoreOptions?)` |
| `snapshot delete` | `<name>` `<snapshot-name>` | | `SnapshotDeleteAsync(configPath, name, snapshotName)` |
| `snapshot list` | `<name>` | | `SnapshotListAsync(configPath, name)` |
| `snapshot push` | `<name>` | | `SnapshotPushAsync(configPath, name)` |
| `snapshot pop` | `<name>` | `--no-delete`, `--no-start`, `--no-provision`, `--provision-with <list>` | `SnapshotPopAsync(configPath, name, VosSnapshotPopOptions?)` |

#### Option records

```csharp
[Builder]
public sealed class VosSnapshotRestoreOptions
{
    public bool NoStart { get; init; }
    public bool? Provision { get; init; }
    public string[]? ProvisionWith { get; init; }
}

[Builder]
public sealed class VosSnapshotPopOptions
{
    public bool NoDelete { get; init; }
    public bool NoStart { get; init; }
    public bool? Provision { get; init; }
    public string[]? ProvisionWith { get; init; }
}
```

### Project commands (IVosProjectService)

| CLI command | Arguments | Options | Service method |
|---|---|---|---|
| `init` | | | `InitAsync(outputDir)` |
| `config show` | | | `ShowConfigAsync(configPath)` |
| `validate` | | | `ValidateAsync(configPath)` |
| `resolve` | `[name]` | `--all` | `ResolveAsync(configPath, name?, all)` |
| `version` | | | `GetVersion()` |

### Machine type commands (IVosMachineTypeService)

| CLI command | Arguments | Options | Service method |
|---|---|---|---|
| `type add` | `<name>` | `--box` (required), `--memory`, `--cpus` | `AddAsync(configPath, name, box, memory, cpus)` |
| `type list` | | | `ListAsync(configPath)` |
| `type show` | `<name>` | | `ShowAsync(configPath, name)` |
| `type remove` | `<name>` | | `RemoveAsync(configPath, name)` |
| `type set` | `<name>` | `--memory`, `--cpus`, `--video-memory`, `--nested-virt`, `--sata-ssd`, `--gui`, `--nic-promisc`, `--no-linked-clones` | `SetAsync(configPath, name, MachineTypeSettings)` |
| `type vboxmanage add` | `<name>` `<args...>` | | `AddVboxManageAsync(configPath, name, args)` |
| `type vboxmanage list` | `<name>` | | `ListVboxManageAsync(configPath, name)` |
| `type vboxmanage remove` | `<name>` `<index>` | | `RemoveVboxManageAsync(configPath, name, index)` |
| `type vboxmanage clear` | `<name>` | | `ClearVboxManageAsync(configPath, name)` |

### Machine commands (IVosMachineService)

| CLI command | Arguments | Options | Service method |
|---|---|---|---|
| `machine add` | `<name>` | `--type` (required), `--instances` (default 1) | `AddAsync(configPath, name, type, instances)` |
| `machine list` | | | `ListAsync(configPath)` |
| `machine remove` | `<name>` | | `RemoveAsync(configPath, name)` |
| `machine enable` | `<name>` | | `EnableAsync(configPath, name)` |
| `machine disable` | `<name>` | | `DisableAsync(configPath, name)` |

### Instance commands (IVosInstanceService)

| CLI command | Arguments | Options | Service method |
|---|---|---|---|
| `instance add` | `<machine>` `<instance-name>` | `--ip`, `--memory`, `--cpus` | `AddAsync(configPath, machine, instanceName, ip?, memory?, cpus?)` |
| `instance remove` | `<machine>` `<instance-name>` | | `RemoveAsync(configPath, machine, instanceName)` |
| `instance list` | | | `ListAsync(configPath)` |

### Network commands (IVosNetworkService)

| CLI command | Arguments | Options | Service method |
|---|---|---|---|
| `network generate` | | `--subnet` (default 192.168.56.0/24), `--start-at` (default 10) | `GenerateAsync(configPath, subnet, startAt)` |
| `network show` | | | `ShowAsync(configPath)` |

### Box management (IVosBoxService)

| CLI command | Arguments | Vagrant-mapped options | Service method |
|---|---|---|---|
| `box list` | | `--box-info` | `ListAsync(boxInfo?)` |
| `box add` | `<name>` | `--force`, `--insecure`, `--cacert`, `--capath`, `--cert`, `--provider`, `--box-version`, `--checksum`, `--checksum-type`, `--name`, `--location-trusted`, `--architecture`, `--clean` | `AddAsync(name, VosBoxAddOptions?)` |
| `box remove` | `<name>` | `--force`, `--provider`, `--box-version`, `--all`, `--all-providers`, `--all-architectures`, `--architecture` | `RemoveAsync(name, VosBoxRemoveOptions?)` |
| `box update` | `<name>` | `--box`, `--provider`, `--force`, `--insecure`, `--cacert`, `--capath`, `--cert`, `--architecture` | `UpdateAsync(configPath, name, VosBoxUpdateOptions?)` |
| `box prune` | | `--force`, `--dry-run`, `--name`, `--provider`, `--keep-active-boxes` | `PruneAsync(VosBoxPruneOptions?)` |
| `box outdated` | `<name>` | `--global`, `--force`, `--insecure`, `--cacert`, `--capath`, `--cert` | `OutdatedAsync(configPath, name, VosBoxOutdatedOptions?)` |
| `box repackage` | `<name>` `<provider>` `<version>` | | `RepackageAsync(name, provider, version)` |

#### Option records

```csharp
[Builder]
public sealed class VosBoxAddOptions
{
    public bool Force { get; init; }
    public bool Insecure { get; init; }
    public bool Clean { get; init; }
    public bool LocationTrusted { get; init; }
    public string? Cacert { get; init; }
    public string? Capath { get; init; }
    public string? Cert { get; init; }
    public string? Provider { get; init; }
    public string? BoxVersion { get; init; }
    public string? Checksum { get; init; }
    public string? ChecksumType { get; init; }  // validated: md5|sha1|sha256|sha512
    public string? Name { get; init; }
    public string? Architecture { get; init; }
}

[Builder]
public sealed class VosBoxRemoveOptions
{
    public bool Force { get; init; }
    public bool All { get; init; }
    public bool AllProviders { get; init; }
    public bool AllArchitectures { get; init; }
    public string? Provider { get; init; }
    public string? BoxVersion { get; init; }
    public string? Architecture { get; init; }
}

[Builder]
public sealed class VosBoxUpdateOptions
{
    public bool Force { get; init; }
    public bool Insecure { get; init; }
    public string? Box { get; init; }
    public string? Provider { get; init; }
    public string? Cacert { get; init; }
    public string? Capath { get; init; }
    public string? Cert { get; init; }
    public string? Architecture { get; init; }
}

[Builder]
public sealed class VosBoxPruneOptions
{
    public bool Force { get; init; }
    public bool DryRun { get; init; }
    public bool KeepActiveBoxes { get; init; }
    public string? Name { get; init; }
    public string? Provider { get; init; }
}

[Builder]
public sealed class VosBoxOutdatedOptions
{
    public bool Global { get; init; }
    public bool Force { get; init; }
    public bool Insecure { get; init; }
    public string? Cacert { get; init; }
    public string? Capath { get; init; }
    public string? Cert { get; init; }
}
```

### Packer commands (IVosPackerService)

| CLI command | Arguments | Packer-mapped options | Service method |
|---|---|---|---|
| `box init` | `<name>` | `--output` (default ".") | `InitAsync(boxName, outputPath)` |
| `box build` | `<path>` | `--force`, `--var` (key=value, multi), `--var-file`, `--only`, `--except`, `--on-error` (cleanup/abort/ask/run-cleanup-provisioner), `--parallel-builds <n>` | `BuildAsync(projectPath, VosPackerBuildOptions?)` |

#### Option class

```csharp
[Builder]
public sealed class VosPackerBuildOptions
{
    public bool Force { get; init; }                          // --force
    public IReadOnlyDictionary<string, string>? Vars { get; init; } // --var key=value
    public string? VarFile { get; init; }                     // --var-file
    public string? Only { get; init; }                        // --only
    public string? Except { get; init; }                      // --except
    public string? OnError { get; init; }                     // validated: cleanup|abort|ask|run-cleanup-provisioner
    public int? ParallelBuilds { get; init; }                 // --parallel-builds
}
```

### Diagnostics (IVosVmService)

| CLI command | Arguments | Vagrant-mapped options | Service method |
|---|---|---|---|
| `global-status` | | | `GlobalStatusAsync()` |
| `vagrant-validate` | | `--ignore-provider` | `VagrantValidateAsync(ignoreProvider?)` |

---

## 3b. IVosBackend changes required

The current `IVosBackend` interface must be updated to accept option records. Example:

```csharp
// Before (too restrictive):
Task<VosActionResult> UpAsync(ResolvedInstance instance, CancellationToken ct = default);

// After (full Vagrant surface):
Task<VosActionResult> UpAsync(ResolvedInstance instance, VosUpOptions? options = null, CancellationToken ct = default);
```

Same pattern for all commands that have options. Methods without Vagrant-specific options keep their current signature.

**VagrantBackend bugs to fix**: upload (pass source/destination), snapshots (pass name), box add/remove (pass name), box repackage (pass name/provider/version).

---

## 3c. YAML schema (new, C#-native) + new Vagrantfile

**Decision**: Nouveau schema C# propre. Rupture avec PoSh. Nouveau Vagrantfile Ruby adapté.

### New YAML schema — `config-vos.yaml`

```yaml
schema_version: 1

vagrant:
  naming_pattern: "{machine}-{index:D2}"
  plugins:
    vagrant-hostmanager:
      enabled: true
      config:
        hostmanager.enabled: true
        hostmanager.manage_host: false
        hostmanager.manage_guest: true
    vagrant-vbguest:
      enabled: true
      config:
        vbguest.auto_update: false
        vbguest.auto_reboot: false
  virtualbox:                          # Global VirtualBox defaults (deep-merged into machine types)
    linked_clones: true
    check_guest_additions: false
    manage:                            # modifyvm key-value pairs
      ioapic: "on"
      hwvirtex: "on"
      nested_hw_virt: "on"
      nestedpaging: "on"
    storagectl:
      - ["--name", "SATA Controller", "--hostiocache", "on"]
    storageattach:
      - ["--storagectl", "SATA Controller", "--port", "0", "--nonrotational", "on"]

machine_types:
  docker-host:
    box: "frenchexdev/alpine-3.21-dockerhost"
    box_version: "1.0.0"
    provider:
      type: "virtualbox"
      memory: 2048
      cpus: 2
      video_memory: 64
      gui: false
      vboxmanage:                      # raw VBoxManage commands (array of arg arrays)
        - ["modifyvm", "{{ .Name }}", "--nicpromisc2", "allow-all"]
    network:
      private:
        ip: null                       # assigned per-instance or via network generate
        mac: null
      public:
        bridge: null
    provisioning_path: "provisioning/alpine/3.21"
    provisioning:                      # ordered dictionary
      install-docker:
        enabled: true
        version: "1.0"
        extension: "sh"
        privileged: true
        reload_before: false
        reload_after: false
        env:
          DOCKER_VERSION: "latest"
    files:                             # file copy provisioning
      docker-daemon-json:
        enabled: true
        source: "./files/daemon.json"
        destination: "/etc/docker/daemon.json"
    shared_folders:
      docker-compose:
        enabled: true
        host_path: "./docker-compose"
        guest_path: "/opt/docker-compose"
        type: "virtualbox"
        disabled: false
      data:
        enabled: true
        host_path: "./data"
        guest_path: "/data"
    disks:
      data-disk:
        size: "20GB"
        primary: false
    plugins:
      - "vagrant-hostmanager"
      - "vagrant-vbguest"
    variables:
      ALPINE_VERSION: "3.21"
      DOCKER_BRIDGE: "docker0"
    commands: {}                       # custom commands with variable substitution

machines:
  main:
    enabled: true
    machine_type: "docker-host"        # reference to machine_types key
    instances:
      - name: "main-01"
        hostname: "main-01.local"
        networking:
          - kind: "private"
            ip: "192.168.56.10"
            mac: null
          - kind: "public"
            bridge: "Intel(R) Wi-Fi"
            mac: null
        memory: null                   # override, null = inherit from type
        cpus: null
      - name: "main-02"
        hostname: "main-02.local"
        networking:
          - kind: "private"
            ip: "192.168.56.11"
```

### Key differences from PoSh schema

| PoSh (v1) | C# (new) | Reason |
|---|---|---|
| `machines_types` | `machine_types` | Underscore convention consistency |
| `base:` wrapper | Flat (no wrapper) | Simpler, no deep nesting |
| `box_name` | `box` | Shorter, aligned with Vagrant terminology |
| `ram_mb` / `vcpus` / `vram_mb` | `provider.memory` / `provider.cpus` / `provider.video_memory` | Grouped under provider |
| `is_enabled` | `enabled` | Shorter |
| `machine_type_name` | `machine_type` | Shorter |
| `provider: "virtualbox"` (string) | `provider.type: "virtualbox"` (nested) | Provider is a structured object |
| `ssh_insert_key` / `ssh_key_path` | TBD — keep if needed | |
| `reload:before` / `reload:after` (colon keys) | `reload_before` / `reload_after` | Underscores — no YAML quoting needed |
| `ext` | `extension` | Explicit |
| Instances inside `base:` | Instances inside `machines.*` | Instances belong to machine declarations, not types |

### New Vagrantfile (Ruby, reads C# schema)

Shipped as embedded resource in `Vos.Bundle`, copied by `VosBundleWriter.WriteAsync`.

```ruby
# -*- mode: ruby -*-
# vi: set ft=ruby :
# Vos — Vagrant On Steroids
# Generated by FrenchExDev.Net.Vos.Bundle

VAGRANT_VERSION = 2

ENV["LC_ALL"] = "en_US.UTF-8"
debug = ENV["VOS_DEBUG"] == "true"

require 'pathname'
require 'yaml'

dir = File.dirname(File.expand_path(__FILE__))

# Load config layers: base + local override (deep-merged)
config_vos = YAML.load_file(File.join(dir, "config-vos.yaml"))
local_path = File.join(dir, "local", "config-vos-local.yaml")
if File.exist?(local_path)
  local_config = YAML.load_file(local_path)
  config_vos = Vagrant::Util::DeepMerge.deep_merge(config_vos, local_config)
end

Vagrant.configure(VAGRANT_VERSION) do |config|

  # ── Global plugins ──────────────────────────────────────────────────
  if config_vos['vagrant'] && config_vos['vagrant']['plugins']
    plugins = config_vos['vagrant']['plugins']

    if Vagrant.has_plugin?('vagrant-hostmanager') && plugins.dig('vagrant-hostmanager', 'enabled')
      pc = plugins['vagrant-hostmanager']['config'] || {}
      config.hostmanager.enabled       = pc['hostmanager.enabled'] || false
      config.hostmanager.manage_host   = pc['hostmanager.manage_host'] || false
      config.hostmanager.manage_guest  = pc['hostmanager.manage_guest'] || false
    end

    if Vagrant.has_plugin?('vagrant-vbguest') && plugins.dig('vagrant-vbguest', 'enabled')
      pc = plugins['vagrant-vbguest']['config'] || {}
      config.vbguest.auto_update         = pc['vbguest.auto_update'] || false
      config.vbguest.auto_reboot         = pc['vbguest.auto_reboot'] || false
    end
  end

  # ── Global VirtualBox defaults ──────────────────────────────────────
  vbox_defaults = config_vos.dig('vagrant', 'virtualbox') || {}

  # ── Machines ────────────────────────────────────────────────────────
  (config_vos['machines'] || {}).each do |machine_name, machine|
    next unless machine['enabled']

    type_name = machine['machine_type']
    mt = (config_vos['machine_types'] || {})[type_name]
    raise "Machine '#{machine_name}' references unknown type '#{type_name}'" unless mt

    # Deep-merge: vbox_defaults ← machine_type ← machine overrides
    merged = Vagrant::Util::DeepMerge.deep_merge(vbox_defaults, mt)

    (machine['instances'] || []).each do |instance|
      vm_name = instance['name']
      puts "Defining VM: #{vm_name}" if debug

      config.vm.define vm_name, primary: false do |srv|
        srv.vm.hostname = instance['hostname'] || vm_name
        srv.vm.box = merged['box']
        srv.vm.box_version = merged['box_version'] if merged['box_version']
        config.vm.box_check_update = false

        # ── Networking ──────────────────────────────────────────────
        (instance['networking'] || []).each do |net|
          if net['kind'] == 'private'
            opts = { ip: net['ip'] }
            opts[:mac] = net['mac'] if net['mac']
            srv.vm.network 'private_network', **opts
          elsif net['kind'] == 'public'
            opts = {}
            opts[:bridge] = net['bridge'] if net['bridge']
            opts[:mac] = net['mac'] if net['mac']
            srv.vm.network 'public_network', **opts
          end
        end

        # ── Disks ───────────────────────────────────────────────────
        (merged['disks'] || {}).each do |disk_name, disk|
          srv.vm.disk :disk, name: disk_name, size: disk['size'], primary: disk['primary'] || false
        end

        # ── Provider ────────────────────────────────────────────────
        provider = merged.dig('provider', 'type') || 'virtualbox'
        if provider == 'virtualbox'
          srv.vm.provider "virtualbox" do |vb|
            vb.memory = instance['memory'] || merged.dig('provider', 'memory') || 2048
            vb.cpus   = instance['cpus'] || merged.dig('provider', 'cpus') || 2
            vb.gui    = merged.dig('provider', 'gui') || false

            vb.linked_clone = merged.fetch('linked_clones', true)
            vb.check_guest_additions = merged.fetch('check_guest_additions', false)

            # modifyvm from global manage + type manage
            (merged['manage'] || {}).each do |key, value|
              vb.customize ["modifyvm", :id, "--#{key}", value.to_s]
            end

            # raw vboxmanage from provider
            (merged.dig('provider', 'vboxmanage') || []).each do |cmd|
              resolved = cmd.map { |arg| arg.gsub("{{ .Name }}", ":id") }
              vb.customize resolved
            end

            # storagectl / storageattach from global
            (merged['storagectl'] || []).each { |args| vb.customize ["storagectl", :id] + args }
            (merged['storageattach'] || []).each { |args| vb.customize ["storageattach", :id] + args }
          end
        end

        # ── File provisioning ───────────────────────────────────────
        (merged['files'] || {}).each do |_, f|
          next if f['enabled'] == false
          srv.vm.provision "file", source: f['source'], destination: f['destination']
        end

        # ── Shared folders ──────────────────────────────────────────
        (merged['shared_folders'] || {}).each do |_, sf|
          next if sf['enabled'] == false
          srv.vm.synced_folder sf['host_path'], sf['guest_path'],
            type: sf['type'], disabled: sf['disabled'] || false
        end

        # ── Shell provisioning ──────────────────────────────────────
        prov_path = merged['provisioning_path'] || 'provisioning'
        (merged['provisioning'] || {}).each do |key, step|
          next if step['enabled'] == false
          puts "  Provisioning: #{key}" if debug

          ext = step['extension'] || 'sh'
          version = step['version']
          path = version ? "./#{prov_path}/#{version}/#{key}.#{ext}" : "./#{prov_path}/#{key}.#{ext}"

          srv.vm.provision :reload if step['reload_before']
          srv.vm.provision "shell", name: key, path: path,
            env: step['env'] || {}, privileged: step['privileged'] || false
          srv.vm.provision :reload if step['reload_after']
        end

      end # config.vm.define
    end # instances
  end # machines
end
```

### VagrantfileRenderer → replaced

`VagrantfileRenderer.Render()` is replaced by:
1. New Vagrantfile is embedded resource in `Vos.Bundle`
2. `VosBundleWriter.WriteAsync` copies it to output dir
3. All config management via YAML — Vagrantfile reads it at `vagrant up` time

---

## 3d. Provisioning config management — fully config-driven

Today `VosConfigManager` has only `AddProvisioningStep()`. Everything else (remove, reorder, enable/disable, env vars, script file creation) is missing. The CLI has no `vos type provision *` subcommands.

**Principle**: scripts live on disk as files, the config references them by key. The Lib creates, manages, and validates these files.

### New commands on `IVosMachineTypeService`

| CLI command | Arguments | Options | Service method |
|---|---|---|---|
| `type provision add` | `<type>` `<key>` | `--version`, `--extension` (default .sh), `--privileged` (default true), `--reload-before`, `--reload-after`, `--env <key=value>` (multi) | `AddProvisioningStepAsync(configPath, type, key, VosProvisioningStepOptions?)` |
| `type provision remove` | `<type>` `<key>` | | `RemoveProvisioningStepAsync(configPath, type, key)` |
| `type provision list` | `<type>` | | `ListProvisioningStepsAsync(configPath, type)` |
| `type provision show` | `<type>` `<key>` | | `ShowProvisioningStepAsync(configPath, type, key)` |
| `type provision enable` | `<type>` `<key>` | | `EnableProvisioningStepAsync(configPath, type, key)` |
| `type provision disable` | `<type>` `<key>` | | `DisableProvisioningStepAsync(configPath, type, key)` |
| `type provision move` | `<type>` `<key>` | `--before <other>` / `--after <other>` | `MoveProvisioningStepAsync(configPath, type, key, before?, after?)` |
| `type provision env set` | `<type>` `<key>` `<env-key>` `<env-value>` | | `SetProvisioningEnvAsync(configPath, type, key, envKey, envValue)` |
| `type provision env remove` | `<type>` `<key>` `<env-key>` | | `RemoveProvisioningEnvAsync(configPath, type, key, envKey)` |

### Script file management on `IVosProjectService`

| CLI command | Arguments | Options | Service method |
|---|---|---|---|
| `provision create` | `<key>` | `--version`, `--extension` (default .sh), `--template <path>` | `CreateProvisioningScriptAsync(configPath, key, version?, extension?, templatePath?)` |
| `provision validate` | | | `ValidateProvisioningScriptsAsync(configPath)` — checks all referenced scripts exist on disk |

`CreateProvisioningScriptAsync` creates the file at the resolved path (`provisioning/{version}/{key}{ext}`) with a default shebang template or copies from `--template`. `ValidateProvisioningScriptsAsync` iterates all enabled machine types → all provisioning steps → checks `File.Exists` for each resolved path.

### Shared folder / Plugin management on `IVosMachineTypeService`

| CLI command | Arguments | Options | Service method |
|---|---|---|---|
| `type shared-folder add` | `<type>` | `--host-path`, `--guest-path`, `--type`, `--disabled` | `AddSharedFolderAsync(configPath, type, hostPath, guestPath, sfType?, disabled?)` |
| `type shared-folder remove` | `<type>` `<index>` | | `RemoveSharedFolderAsync(configPath, type, index)` |
| `type shared-folder list` | `<type>` | | `ListSharedFoldersAsync(configPath, type)` |
| `type plugin add` | `<type>` `<name>` | | `AddPluginAsync(configPath, type, name)` |
| `type plugin remove` | `<type>` `<name>` | | `RemovePluginAsync(configPath, type, name)` |
| `type plugin list` | `<type>` | | `ListPluginsAsync(configPath, type)` |

### VosProvisioningStepOptions

```csharp
[Builder]
public sealed class VosProvisioningStepOptions
{
    public string? Version { get; init; }
    public string? Extension { get; init; }       // default .sh
    public bool Privileged { get; init; } = true;
    public bool ReloadBefore { get; init; }
    public bool ReloadAfter { get; init; }
    public IReadOnlyDictionary<string, string>? Env { get; init; }
}
```

---

## 3e. Autocompletion — PowerShell cmdlets + System.CommandLine

### Existing pattern

`VosMachineNameCompleter : IArgumentCompleter` already exists in `Vos.Infra.PowerShell`. It reads `config-vos.yaml`, resolves all instances, and returns `CompletionResult` entries with name + tooltip (box, memory).

`VosCmdletBase` uses `[ArgumentCompleter(typeof(VosMachineNameCompleter))]` on the `MachineName` parameter.

### Completers to add

We need completers for every argument that references a config entity:

| Completer class | Completes | Source | Used by |
|---|---|---|---|
| `VosMachineNameCompleter` | Instance names | `VosConfigMerger.ResolveAll()` | `up`, `halt`, `destroy`, `reload`, `provision`, `ssh`, `ssh-command`, `ssh-config`, `upload`, `port`, `package`, `rdp`, `powershell`, `winrm`, `winrm-config`, `suspend`, `resume`, `snapshot *`, `box update`, `box outdated` |
| `VosMachineTypeNameCompleter` | Machine type names | `config.MachineTypes.Keys` | `type show`, `type remove`, `type set`, `type vboxmanage *`, `type provision *`, `type shared-folder *`, `type plugin *` |
| `VosMachineGroupNameCompleter` | Machine names | `config.Machines.Keys` | `machine remove`, `machine enable`, `machine disable`, `instance add`, `instance remove` |
| `VosSnapshotNameCompleter` | Snapshot names | `IVosBackend.SnapshotListAsync()` | `snapshot restore`, `snapshot delete` |
| `VosBoxNameCompleter` | Installed box names | `IVosBackend.ImageListAsync()` | `box remove`, `box repackage` |
| `VosProvisioningKeyCompleter` | Provisioning step keys | `machineType.Provisioning[*].Key` | `type provision remove/show/enable/disable/move/env *` |
| `VosProviderNameCompleter` | Provider names | static: `virtualbox`, `hyperv`, `parallels`, `docker` | `--provider` option on `up`, `box add`, `box update` |

### PowerShell integration

Each completer implements `IArgumentCompleter` (same pattern as existing `VosMachineNameCompleter`). Cmdlet parameters use `[ArgumentCompleter(typeof(...))]`.

All completers share a private `LoadConfig()` helper (read `config-vos.yaml` from CWD, catch errors, return null).

### System.CommandLine integration (CLI)

System.CommandLine supports completions via `AddCompletions()` on arguments/options:

```csharp
// In CLI Program.cs — instance name argument with tab completion
var nameArg = new Argument<string>("name") { Description = "VM instance name" };
nameArg.AddCompletions((ctx) =>
{
    var config = LoadConfigForCompletion();
    if (config is null) return [];
    return VosConfigMerger.ResolveAll(config)
        .Select(x => new CompletionItem(x.Instance.Name, documentation: $"{x.Instance.Box}, {x.Instance.Memory}MB"));
});
```

Same approach for machine type names, machine names, snapshot names, etc. The completion delegates read the config from the `--config` option value (or default `config-vos.yaml`).

### Completion table — per argument

| Argument / Option | Completer logic | CLI (`AddCompletions`) | PowerShell (`IArgumentCompleter`) |
|---|---|---|---|
| `<name>` (instance) | Resolve all instances from config | `nameArg.AddCompletions(...)` | `VosMachineNameCompleter` |
| `<name>` (machine type) | `config.MachineTypes.Keys` | `typeNameArg.AddCompletions(...)` | `VosMachineTypeNameCompleter` |
| `<name>` (machine) | `config.Machines.Keys` | `machineNameArg.AddCompletions(...)` | `VosMachineGroupNameCompleter` |
| `<machine>` (for instance add/remove) | `config.Machines.Keys` | `instMachineArg.AddCompletions(...)` | `VosMachineGroupNameCompleter` |
| `<snapshot-name>` | Parse output of `vagrant snapshot list` | `snapNameArg.AddCompletions(...)` | `VosSnapshotNameCompleter` |
| `<name>` (box) | Parse output of `vagrant box list` | `boxNameArg.AddCompletions(...)` | `VosBoxNameCompleter` |
| `<key>` (provisioning) | `machineType.Provisioning[*].Key` | `provKeyArg.AddCompletions(...)` | `VosProvisioningKeyCompleter` |
| `--provider` | Static: virtualbox, hyperv, parallels, docker | `providerOption.AddCompletions(...)` | `VosProviderNameCompleter` |
| `--type` (machine add) | `config.MachineTypes.Keys` | `typeOption.AddCompletions(...)` | `VosMachineTypeNameCompleter` |
| `--provision-with` | `machineType.Provisioning[*].Key` (enabled only) | `provisionWithOption.AddCompletions(...)` | `VosProvisioningKeyCompleter` |

---

## 3f. Logging + streaming output

### Logging

All services inject `ILogger<T>` (from `Microsoft.Extensions.Logging`). Debug/trace for internal operations. Events are business-level (user-facing); logs are developer-level (debugging).

```csharp
public abstract class VosServiceBase(
    IVosConfigSerializer serializer,
    IVosEventEmitter emitter,
    ILogger logger)  // added
```

Lib.csproj adds `Microsoft.Extensions.Logging.Abstractions`. CLI wires console logging:
```csharp
services.AddLogging(b => b.AddConsole().SetMinimumLevel(
    verbose ? LogLevel.Debug : LogLevel.Information));
```

### Streaming output for long operations

The existing `IOutputParser<TEvent>` + `Channel<OutputLine>` pattern in BinaryWrapper already streams Vagrant/Packer stdout line-by-line. The Lib must **forward these as events** so the CLI/subscriber can display real-time progress:

```csharp
// New streaming events
public sealed record VagrantOutput(string InstanceName, string Line) : VosEvent;
public sealed record PackerOutput(string Line) : VosEvent;
```

`VosVmService.UpAsync` subscribes to `VagrantOutputLine` events from the backend and re-emits them as `VagrantOutput` via `IVosEventEmitter`. Same for `VosPackerService.BuildAsync` with `PackerOutputLine`.

CLI subscribes:
```csharp
emitter.Subscribe<VagrantOutput>(e => Console.WriteLine($"[{e.InstanceName}] {e.Line}"));
emitter.Subscribe<PackerOutput>(e => Console.WriteLine(e.Line));
```

### `--verbose` / `--quiet` global options

| Flag | Behavior |
|---|---|
| (default) | Events: start/complete only. No streaming. |
| `--verbose` / `-v` | Events: all including `VagrantOutput`/`PackerOutput` streaming. Log level: Debug. |
| `--quiet` / `-q` | Events: errors only. Log level: Warning. |

---

## 3g. Dry-run, diff, and health checks

### `--dry-run` on destructive operations

Supported on: `destroy`, `halt`, `network generate`, `box prune`, `box remove`.

```csharp
// Global option
var dryRunOption = new Option<bool>("--dry-run") { Description = "Show what would happen without executing" };
```

Lib services accept `bool dryRun = false` parameter. When true:
- Load config, resolve targets, emit `DryRun*` events showing what would happen
- Do NOT call backend or mutate config
- Return `Result.Success()` with the planned actions

New events:
```csharp
public sealed record DryRunAction(string Operation, string Target, string Description) : VosEvent;
```

### `vos diff` — show pending config changes

Compare current config-vos.yaml with the resolved state. Show what `vagrant up` would apply vs what's currently running.

```csharp
// IVosProjectService
Task<Result<IReadOnlyList<VosConfigDiff>>> DiffAsync(string configPath, CancellationToken ct = default);
```

```csharp
public sealed record VosConfigDiff(string InstanceName, string Property, string? CurrentValue, string? NewValue);
```

### `vos check` — post-up health verification

After `vagrant up`, verify each instance is actually reachable:

```csharp
// IVosVmService
Task<Result<IReadOnlyList<VosHealthCheckResult>>> CheckAsync(string configPath, string? instanceName = null, CancellationToken ct = default);
```

Steps per instance:
1. SSH connection test (`ssh-command "echo ok"`)
2. Hostname matches config
3. IP matches config (via `ip addr`)
4. If Docker host: `docker info` succeeds

```csharp
public sealed record VosHealthCheckResult(string InstanceName, bool SshOk, bool HostnameOk, bool IpOk, bool? DockerOk);
public sealed record HealthCheckStarting(string InstanceName) : VosEvent;
public sealed record HealthCheckCompleted(string InstanceName, bool AllPassed) : VosEvent;
public sealed record HealthCheckFailed(string InstanceName, string Check, string Expected, string Actual) : VosEvent;
```

---

## 3h. Secrets, file locking, and partial failure rollback

### Secrets — environment variable substitution in YAML

YAML values can reference env vars via `${VAR_NAME}` syntax:

```yaml
provisioning:
  install-gitlab-runner:
    env:
      REGISTRATION_TOKEN: "${GITLAB_RUNNER_TOKEN}"
      CI_SERVER_URL: "${GITLAB_URL}"
```

The config loader resolves `${...}` patterns from `Environment.GetEnvironmentVariable()` at load time. Unresolved vars → `Result.Failure` with `SecretNotFound` validation error.

```csharp
public sealed record SecretResolved(string VarName) : VosEvent;
public sealed record SecretNotFound(string VarName, string ConfigPath) : VosEvent;
```

Additionally, `config-vos-local.yaml` (gitignored) is the recommended place for secrets. The deep-merge loads it automatically.

### File locking — concurrent access safety

All config mutations (load → mutate → save) must be atomic. Use a file-based lock:

```csharp
// In VosServiceBase
protected async Task<Result> WithConfigLockAsync(string configPath, Func<VosConfig, Task<Result>> action, CancellationToken ct)
{
    var lockPath = configPath + ".lock";
    using var lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    // load → action → save inside lock
}
```

Lock timeout: 5 seconds. If another process holds the lock → `Result.Failure` with `ConfigLocked(path, timeout)` error.

### Partial failure rollback

When `vos up` with N instances fails on instance K:

1. Track which instances were started successfully (via events)
2. On failure, offer rollback: destroy the successfully-started instances
3. Configurable via `VosLibOptions`:

```csharp
public sealed class VosLibOptions
{
    // ... existing ...
    public FailureStrategy OnPartialFailure { get; set; } = FailureStrategy.Stop;
}

public enum FailureStrategy
{
    Stop,           // Stop at first failure, leave started VMs running
    Continue,       // Try all instances, report failures at end
    Rollback        // Destroy successfully-started VMs on any failure
}
```

Events:
```csharp
public sealed record PartialFailureDetected(string FailedInstance, int SucceededCount, FailureStrategy Strategy) : VosEvent;
public sealed record RollbackStarting(int InstanceCount) : VosEvent;
public sealed record RollbackCompleted(int DestroyedCount) : VosEvent;
```

---

## 3i. Pipeline + Unit of Work + Specification pattern

### Request/Response pipeline

Replace the repeated load→lock→validate→execute→save→emit pattern with a composable pipeline. Each service method becomes a handler, cross-cutting concerns are behaviors:

```csharp
// In Lib.Abstractions
public interface IVosRequest<TResult> { }

public interface IVosHandler<in TRequest, TResult> where TRequest : IVosRequest<TResult>
{
    Task<Result<TResult>> HandleAsync(TRequest request, CancellationToken ct = default);
}

public interface IVosBehavior<in TRequest, TResult> where TRequest : IVosRequest<TResult>
{
    Task<Result<TResult>> HandleAsync(TRequest request, Func<Task<Result<TResult>>> next, CancellationToken ct = default);
}
```

Built-in behaviors (ordered, outermost → innermost):
1. `LoggingBehavior` — structured log entry/exit with duration
2. `TracingBehavior` — OpenTelemetry `Activity` span
3. `ValidationBehavior` — runs `IValidationRule<TRequest>` specs, short-circuits on failure
4. `ConfigLoadBehavior` — loads config for requests that implement `IConfigAware { string ConfigPath }`
5. `LockingBehavior` — acquires `VosFileLock` for requests that implement `IConfigMutating`
6. `SaveBehavior` — saves config after handler completes for `IConfigMutating` requests
7. `EventBehavior` — emits Starting/Completed events

The handler only contains business logic. No boilerplate.

### Unit of Work — batched config mutations

```csharp
// In Lib.Abstractions
public interface IVosUnitOfWork : IAsyncDisposable
{
    VosConfig Config { get; }
    VosConfigManager Manager { get; }
    Task<Result> CommitAsync(CancellationToken ct = default);  // save + emit ConfigSaved
    void Rollback();  // discard changes
}

// In Lib
public sealed class VosUnitOfWork : IVosUnitOfWork
{
    // Loads config once, holds lock, commits once on CommitAsync
}
```

Usage for batch operations:
```csharp
await using var uow = await unitOfWorkFactory.CreateAsync(configPath, ct);
uow.Manager.AddMachineType("docker-host", "alpine/3.21", 2048, 2);
uow.Manager.AddMachine("main", "docker-host", 1);
uow.Manager.AddProvisioningStep("docker-host", "install-docker");
await uow.CommitAsync(ct);  // single write, single lock acquire/release
```

### Specification pattern for validation

Replace static `VosConfigValidator` with composable rules:

```csharp
// In Lib.Abstractions
public interface IValidationRule<in T>
{
    IEnumerable<string> Validate(T target);
}

public interface IValidationRuleProvider<T>
{
    IEnumerable<IValidationRule<T>> GetRules();
}
```

Built-in rules (each its own class):
- `MachineTypeReferenceRule` — every machine references an existing machine type
- `UniqueInstanceNameRule` — no duplicate instance names
- `MachineTypeHasBoxRule` — enabled types must have a box
- `InstanceIpFormatRule` — IPs are valid
- `InstanceNameNotEmptyRule` — names not blank
- `ProvisioningScriptExistsRule` — scripts exist on disk
- `NoIpConflictRule` — no two instances share an IP

Contributors can register custom rules via DI.

---

## 3j. Resilience + disposal + graceful shutdown

### Resilience — Polly (Microsoft.Extensions.Resilience)

Vagrant and Packer are external CLI processes. They can fail transiently (VirtualBox lock, network timeout, disk I/O). Use **Polly v8** via `Microsoft.Extensions.Resilience`:

```csharp
// In Lib — configure resilience pipelines
services.AddResiliencePipeline("vagrant-lifecycle", builder =>
{
    builder.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential
    });
    builder.AddTimeout(TimeSpan.FromMinutes(10));
});

services.AddResiliencePipeline("packer-build", builder =>
{
    builder.AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 2 });
    builder.AddTimeout(TimeSpan.FromMinutes(30));
});

// In handlers — inject ResiliencePipelineProvider<string>
var pipeline = pipelineProvider.GetPipeline("vagrant-lifecycle");
var result = await pipeline.ExecuteAsync(async ct => await backend.UpAsync(instance, options, ct), ct);
```

No custom `IResiliencePolicy` abstraction needed — Polly's `ResiliencePipeline` is the abstraction.

**Lib.csproj** adds: `Microsoft.Extensions.Resilience`.

Configurable via `VosLibOptions`:
```csharp
public sealed class VosResilienceOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan BuildTimeout { get; set; } = TimeSpan.FromMinutes(30);
}
```

### Disposal — IAsyncDisposable

Services that hold resources implement `IAsyncDisposable`:
- `VosUnitOfWork` — releases file lock
- `VosFileLock` — releases lock file
- `VosEventEmitter` — clears subscriptions

### Graceful shutdown — Ctrl+C handling

```csharp
// In CLI Program.cs
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;  // don't kill immediately
    cts.Cancel();     // propagate CancellationToken
};
```

When `CancellationToken` fires during multi-instance `vos up`:
1. **Current VM**: wait for Vagrant process to finish (don't kill mid-provisioning)
2. **Remaining VMs**: skip (don't start new VMs)
3. **Already started**: leave running (user can `vos destroy` later)
4. Emit `OperationCancelled(startedCount, skippedCount)` event

---

## 3k. Tracing + structured logging

### OpenTelemetry tracing

Each service method creates an `Activity` span:

```csharp
// In Lib
private static readonly ActivitySource ActivitySource = new("FrenchExDev.Net.Vos");

public async Task<Result<...>> UpAsync(...)
{
    using var activity = ActivitySource.StartActivity("vos.vm.up");
    activity?.SetTag("vos.instance", instanceName);
    activity?.SetTag("vos.config", configPath);
    // ... handler ...
    activity?.SetTag("vos.result", result.IsSuccess ? "success" : "failure");
}
```

Multi-VM operations create child spans:
```
vos.vm.up (parent)
  ├─ vos.vm.up.instance[main-01]
  ├─ vos.vm.up.instance[main-02]
  └─ vos.vm.up.instance[main-03]
```

### Structured logging with `LoggerMessage.Define`

```csharp
// In Lib — per service
internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Loading config from {ConfigPath}")]
    public static partial void ConfigLoading(ILogger logger, string configPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "VM {InstanceName} started in {ElapsedMs}ms")]
    public static partial void VmStarted(ILogger logger, string instanceName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "VM {InstanceName} failed: {Error}")]
    public static partial void VmFailed(ILogger logger, string instanceName, string error);
}
```

### Correlation ID

Each top-level operation generates a `Guid` correlation ID, passed through all events and log scopes:

```csharp
public abstract record VosEvent
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
```

Log scope wraps each operation:
```csharp
using var scope = logger.BeginScope(new Dictionary<string, object>
{
    ["CorrelationId"] = correlationId,
    ["Operation"] = "up",
    ["ConfigPath"] = configPath
});
```

---

## 3l. Caching + idempotency + null object

### Config cache with file watcher invalidation

Tab completion and repeated CLI invocations deserialize YAML on every call. Add an in-memory cache:

```csharp
// In Lib
public interface IVosConfigCache
{
    Task<Result<VosConfig>> GetOrLoadAsync(string configPath, CancellationToken ct = default);
    void Invalidate(string configPath);
    void InvalidateAll();
}

public sealed class VosConfigCache : IVosConfigCache, IDisposable
{
    private readonly ConcurrentDictionary<string, (VosConfig Config, DateTime LoadedAt)> _cache = new();
    private readonly FileSystemWatcher _watcher;  // invalidates on file change
}
```

Cache is used by:
- Completers (hot path — tab key)
- Read-only services (list, show, resolve)

Mutating services bypass cache and invalidate after save.

### Idempotent CRUD operations

Replace throw-on-duplicate with idempotent semantics:

```csharp
// Current (throws):
public void AddMachineType(string name, ...) {
    if (_config.MachineTypes.ContainsKey(name))
        throw new InvalidOperationException($"Already exists.");
}

// Enterprise-grade (idempotent, returns Result):
public Result AddOrUpdateMachineType(string name, ...) {
    if (_config.MachineTypes.TryGetValue(name, out var existing))
        return Result.Success();  // already exists, no-op (or update if properties differ)
    // ... create ...
    return Result.Success();
}
```

Each CRUD method returns `Result`:
- `Add*` → creates if not exists, no-op if already exists with same config, `Result.Failure` if exists with different config
- `Remove*` → removes if exists, no-op if already gone
- `Enable*`/`Disable*` → no-op if already in target state

### Null object pattern — fallback services

```csharp
// In Lib
public sealed class NullVosBackend : IVosBackend
{
    public string Name => "none";
    public IReadOnlySet<string> SupportedActions => new HashSet<string>();
    public Task<VosActionResult> UpAsync(...) =>
        Task.FromResult(new VosActionResult(false, "", "No backend configured. Register IVosBackend via DI."));
    // ... all methods return failure with clear message ...
}
```

Registered as fallback in DI:
```csharp
// In Lib.DependencyInjection
services.TryAddSingleton<IVosBackend, NullVosBackend>();  // only if no backend registered
```

Same for `IFileSystem`:
```csharp
services.TryAddSingleton<IFileSystem, NullFileSystem>();  // throws clear message if used without real FS
```

---

## 4. VosEvent hierarchy

| Group | CLI command | Starting event | Completed event | Extra properties |
|---|---|---|---|---|
| **Config** | *(shared)* | `ConfigLoading` | `ConfigLoaded` | Path, MachineTypeCount, MachineCount |
| | *(shared)* | | `ConfigNotFound` | Path |
| | *(shared)* | | `ConfigSaved` | Path |
| **Project** | `init` | `ProjectInitializing` | `ProjectInitialized` | OutputDir |
| | | `FileCreated` | | Path |
| | | `VagrantfileGenerated` | | Path |
| **Validation** | `validate` | `ValidationStarted` | `ValidationCompleted` | Path, ErrorCount, ResolvedInstanceCount |
| | | `ValidationError` | | Message |
| **Config show** | `config show` | `ConfigShowStarted` | `ConfigShowCompleted` | Path, InstanceCount |
| **Resolve** | `resolve` | `ResolveStarted` | `ResolveCompleted` | Path, InstanceName?, Count |
| | | `InstanceResolved` | | MachineName, InstanceName |
| **Machine type** | `type add` | `MachineTypeAdding` | `MachineTypeAdded` | Name, Box |
| | `type remove` | `MachineTypeRemoving` | `MachineTypeRemoved` | Name |
| | `type list` | | `MachineTypeListed` | Count |
| | `type show` | | `MachineTypeShown` | Name |
| | `type set` | `MachineTypeUpdating` | `MachineTypeUpdated` | Name |
| **VBoxManage** | `type vboxmanage add` | `VboxManageCommandAdding` | `VboxManageCommandAdded` | MachineTypeName, Args |
| | `type vboxmanage remove` | `VboxManageCommandRemoving` | `VboxManageCommandRemoved` | MachineTypeName, Index |
| | `type vboxmanage clear` | `VboxManageCommandsClearing` | `VboxManageCommandsCleared` | MachineTypeName |
| | `type vboxmanage list` | | `VboxManageCommandsListed` | MachineTypeName, Count |
| **Provisioning** | `type provision add` | `ProvisioningStepAdding` | `ProvisioningStepAdded` | TypeName, Key |
| | `type provision remove` | `ProvisioningStepRemoving` | `ProvisioningStepRemoved` | TypeName, Key |
| | `type provision list` | | `ProvisioningStepsListed` | TypeName, Count |
| | `type provision show` | | `ProvisioningStepShown` | TypeName, Key |
| | `type provision enable` | `ProvisioningStepEnabling` | `ProvisioningStepEnabled` | TypeName, Key |
| | `type provision disable` | `ProvisioningStepDisabling` | `ProvisioningStepDisabled` | TypeName, Key |
| | `type provision move` | `ProvisioningStepMoving` | `ProvisioningStepMoved` | TypeName, Key |
| | `type provision env set` | `ProvisioningEnvSetting` | `ProvisioningEnvSet` | TypeName, Key, EnvKey |
| | `type provision env remove` | `ProvisioningEnvRemoving` | `ProvisioningEnvRemoved` | TypeName, Key, EnvKey |
| | `provision create` | `ProvisioningScriptCreating` | `ProvisioningScriptCreated` | Key, Path |
| | `provision validate` | `ProvisioningValidating` | `ProvisioningValidated` | ErrorCount |
| | | `ProvisioningScriptMissing` | | Key, ExpectedPath |
| **SharedFolder** | `type shared-folder add` | `SharedFolderAdding` | `SharedFolderAdded` | TypeName, HostPath, GuestPath |
| | `type shared-folder remove` | `SharedFolderRemoving` | `SharedFolderRemoved` | TypeName, Index |
| | `type shared-folder list` | | `SharedFoldersListed` | TypeName, Count |
| **Plugin** | `type plugin add` | `PluginAdding` | `PluginAdded` | TypeName, Name |
| | `type plugin remove` | `PluginRemoving` | `PluginRemoved` | TypeName, Name |
| | `type plugin list` | | `PluginsListed` | TypeName, Count |
| **Machine** | `machine add` | `MachineAdding` | `MachineAdded` | Name, TypeName, InstanceCount |
| | `machine remove` | `MachineRemoving` | `MachineRemoved` | Name |
| | `machine list` | | `MachineListed` | Count |
| | `machine enable` | `MachineEnabling` | `MachineEnabled` | Name |
| | `machine disable` | `MachineDisabling` | `MachineDisabled` | Name |
| **Instance** | `instance add` | `InstanceAdding` | `InstanceAdded` | MachineName, InstanceName |
| | `instance remove` | `InstanceRemoving` | `InstanceRemoved` | MachineName, InstanceName |
| | `instance list` | | `InstanceListed` | Count |
| **Network** | `network generate` | `NetworkGenerating` | `NetworkGenerated` | Subnet, StartAt, AssignedCount |
| | | `NetworkConflictDetected` | | Message |
| | `network show` | `NetworkShowing` | `NetworkShown` | Count |
| | | `NetworkConflictDetected` | | Message |
| **VM lifecycle** | `up` | `VmStarting` | `VmStarted` | InstanceName |
| | `halt` | `VmHalting` | `VmHalted` | InstanceName, Force |
| | `destroy` | `VmDestroying` | `VmDestroyed` | InstanceName, Force |
| | `reload` | `VmReloading` | `VmReloaded` | InstanceName |
| | `provision` | `VmProvisioning` | `VmProvisioned` | InstanceName |
| | `status` | `VmStatusQuerying` | `VmStatusQueried` | InstanceName, Output |
| | `suspend` | `VmSuspending` | `VmSuspended` | InstanceName |
| | `resume` | `VmResuming` | `VmResumed` | InstanceName |
| | *(any fail)* | | `VmOperationFailed` | Operation, InstanceName, Error |
| **SSH/Remote** | `ssh` | `SshConnecting` | `SshConnected` | InstanceName |
| | `ssh-command` | `SshCommandExecuting` | `SshCommandExecuted` | InstanceName, Command, Output |
| | `ssh-config` | `SshConfigQuerying` | `SshConfigQueried` | InstanceName |
| | `upload` | `FileUploading` | `FileUploaded` | InstanceName, Source, Destination |
| | `rdp` | `RdpConnecting` | `RdpConnected` | InstanceName |
| | `powershell` | `PowershellConnecting` | `PowershellConnected` | InstanceName |
| | `winrm` | `WinrmConnecting` | `WinrmConnected` | InstanceName |
| | `winrm-config` | `WinrmConfigQuerying` | `WinrmConfigQueried` | InstanceName |
| | `port` | `PortQuerying` | `PortQueried` | InstanceName |
| | `package` | `PackageCreating` | `PackageCreated` | InstanceName |
| **Snapshots** | `snapshot save` | `SnapshotSaving` | `SnapshotSaved` | InstanceName, SnapshotName |
| | `snapshot restore` | `SnapshotRestoring` | `SnapshotRestored` | InstanceName, SnapshotName |
| | `snapshot delete` | `SnapshotDeleting` | `SnapshotDeleted` | InstanceName, SnapshotName |
| | `snapshot list` | `SnapshotListing` | `SnapshotListed` | InstanceName |
| | `snapshot push` | `SnapshotPushing` | `SnapshotPushed` | InstanceName |
| | `snapshot pop` | `SnapshotPopping` | `SnapshotPopped` | InstanceName |
| **Box** | `box list` | `BoxListing` | `BoxListed` | Output |
| | `box add` | `BoxAdding` | `BoxAdded` | Name |
| | `box remove` | `BoxRemoving` | `BoxRemoved` | Name |
| | `box update` | `BoxUpdating` | `BoxUpdated` | InstanceName |
| | `box prune` | `BoxPruning` | `BoxPruned` | |
| | `box outdated` | `BoxOutdatedChecking` | `BoxOutdatedChecked` | InstanceName |
| | `box repackage` | `BoxRepackaging` | `BoxRepackaged` | Name, Provider, Version |
| **Packer** | `box init` | `BoxInitializing` | `BoxInitialized` | BoxName, OutputPath |
| | `box build` | `BoxBuilding` | `BoxBuilt` | ProjectPath, Force, ArtifactCount |
| | | | `BoxBuildFailed` | ProjectPath, Error |
| | | `BoxArtifactProduced` | | BuilderType, Name, Path |
| **Diagnostics** | `global-status` | `GlobalStatusQuerying` | `GlobalStatusQueried` | Output |
| | `vagrant-validate` | `VagrantValidating` | `VagrantValidated` | Output |

All events are `sealed record` inheriting from `abstract record VosEvent`.

---

## 4. `Vos.Lib` — implementations + DI

### `VosEventEmitter` — default impl

```csharp
public sealed class VosEventEmitter : IVosEventEmitter
{
    private readonly List<Action<VosEvent>> _handlers = [];

    public void Emit(VosEvent evt) { foreach (var h in _handlers) h(evt); }

    public IDisposable Subscribe(Action<VosEvent> handler)
    {
        _handlers.Add(handler);
        return new Subscription(() => _handlers.Remove(handler));
    }

    public IDisposable Subscribe<T>(Action<T> handler) where T : VosEvent
        => Subscribe(evt => { if (evt is T typed) handler(typed); });
}
```

### DI registration — `[Injectable]` source-generated, no hand-written wiring

Each service class is annotated with `[Injectable]`. The Injectable SG auto-generates one extension method per project assembly:

```csharp
// In Vos.Lib — service classes annotated:
[Injectable(Scope = Scope.Singleton, As = typeof(IVosEventEmitter))]
public sealed class VosEventEmitter : IVosEventEmitter { ... }

[Injectable(Scope = Scope.Transient, As = typeof(IVosProjectService))]
public sealed class VosProjectService : IVosProjectService { ... }

[Injectable(Scope = Scope.Transient, As = typeof(IVosVmService))]
public sealed class VosVmService : IVosVmService { ... }
// ... etc.

// In Vos.Infra.FileSystem:
[Injectable(Scope = Scope.Singleton, As = typeof(IFileSystem))]
public sealed class PhysicalFileSystem : IFileSystem { ... }

// In Vos.Infra.Vagrant:
[Injectable(Scope = Scope.Singleton, As = typeof(IVosBackend))]
public sealed class VagrantBackend : IVosBackend { ... }

// In Vos.VosFile:
[Injectable(Scope = Scope.Singleton, As = typeof(IVosFileReader))]
public sealed class VosFileReader : IVosFileReader { ... }
```

The SG generates per-assembly extension methods (Microsoft DI):
```csharp
// Auto-generated by Injectable.Microsoft.SourceGenerator
services.AddFrenchExDevNetVosLibInjectables();           // all Lib services
services.AddFrenchExDevNetVosVosFileInjectables();       // VosFileReader, Writer, Validator
services.AddFrenchExDevNetVosInfraFileSystemInjectables(); // PhysicalFileSystem
services.AddFrenchExDevNetVosInfraVagrantInjectables();  // VagrantBackend
```

**DI-agnostic**: for DryIoc, reference `Injectable.DryIoc.SourceGenerator` instead — same `[Injectable]` attributes, different generated code.

**No `Vos.Lib.DependencyInjection` project needed.**

---

## 5. CLI architecture — System.CommandLine v2 patterns

Follows the 4-class pattern from `Skills/Net/Programming/SYSTEM.COMMANDLINE/`:

### 4 class types in the CLI project

| Class type | Role | DI? |
|---|---|---|
| `VosCliSymbols` (static) | Owns all `Option<T>`, `Argument<T>` instances. Shared by reference. | No |
| `VosCliThrow` (static) + exception hierarchy | Named exceptions + `[DoesNotReturn]` throw methods. | No |
| `*CommandResolver` | Bridges `ParseResult` → typed input record. Validates, throws if missing. | Yes |
| `*Command : Command` | Each leaf = own class. Receives resolver + Lib services via ctor DI. `SetAction(async (pr, ct) => ...)`. | Yes |

### Key rules

- **Arguments vs Options** — `Argument<T>` required → throw `RequiredArgumentMissingException` if null. `Option<T>` with default → use `!` (guaranteed non-null by DefaultValueFactory). `Option<T>` nullable → stays `null`, no throw. `Option<T>` semantically required (e.g. `--box` on `type add`) → throw `RequiredOptionMissingException`.
- **No silent failures** — no `if (x is null) return`
- **Named exceptions** — not `InvalidOperationException("message")`
- **ParseResult never leaks** past the resolver into domain code
- **Program.cs is wiring only** — ServiceCollection + RootCommand assembly + top-level catch
- **Group commands have no action** — they only collect children via DI
- **The pipeline (IVosRequest/IVosHandler/IVosBehavior) lives in Lib** — not in CLI. Commands call Lib services, which internally use the pipeline.

### Example: HaltCommand

```csharp
// VosCliSymbols.cs (static)
public static class VosCliSymbols
{
    public static Option<string> Config { get; } = new("--config") { DefaultValueFactory = _ => "config-vos.yaml" };
    public static Option<bool> Force { get; } = new("--force");
    public static Option<bool> Local { get; } = new("--local") { Description = "Write to local override" };
    public static Argument<string> Name { get; } = new("name");
    // ... all other symbols
}

// Resolvers/HaltCommandResolver.cs
public record HaltInput(string Name, string ConfigPath, bool Force);

public class HaltCommandResolver
{
    public HaltInput Resolve(ParseResult pr)
    {
        // Argument<string> — required, throw if missing
        var name = pr.GetValue(VosCliSymbols.Name)
            ?? throw new RequiredArgumentMissingException("name");

        // Option<string> with DefaultValueFactory — always has a value, never null
        var config = pr.GetValue(VosCliSymbols.Config)!;

        // Option<bool> — optional, defaults to false
        return new HaltInput(name, config, pr.GetValue(VosCliSymbols.Force));
    }
}

// Commands/HaltCommand.cs
public class HaltCommand : Command
{
    public HaltCommand(HaltCommandResolver resolver, IVosVmService vmService)
        : base("halt", "Stop VM(s)")
    {
        Arguments.Add(VosCliSymbols.Name);
        Options.Add(VosCliSymbols.Config);
        Options.Add(VosCliSymbols.Force);

        SetAction(async (pr, ct) =>
        {
            var input = resolver.Resolve(pr);
            var result = await vmService.HaltAsync(input.ConfigPath, input.Name, input.Force, ct);
            result.Match(
                onSuccess: r => ResultPrinter.Print(input.Name, r),
                onFailure: ex => throw ex);
        });
    }
}
```

### Program.cs — wiring only

```csharp
var services = new ServiceCollection();

// Lib + infra
services.AddFrenchExDevNetVosLibInjectables();            // SG-generated from [Injectable] in Lib
services.AddFrenchExDevNetVosVosFileInjectables();        // SG-generated from [Injectable] in VosFile
services.AddFrenchExDevNetVosInfraFileSystemInjectables();// SG-generated from [Injectable] in Infra.FileSystem
services.AddVosBackend<VagrantBackend>();
services.AddLogging(b => b.AddConsole());

// CLI-specific: resolvers + commands
services.AddTransient<HaltCommandResolver>();
services.AddTransient<HaltCommand>();
// ... all resolvers + commands ...
services.AddTransient<SnapshotGroupCommand>();
services.AddTransient<TypeGroupCommand>();

var provider = services.BuildServiceProvider();

// Subscribe to events
var emitter = provider.GetRequiredService<IVosEventEmitter>();
emitter.Subscribe<VmOperationFailed>(e => Console.Error.WriteLine($"ERROR: {e.Error}"));

// Assemble command tree
var root = new RootCommand("vos - Vagrant On Steroids");
root.Subcommands.Add(provider.GetRequiredService<HaltCommand>());
root.Subcommands.Add(provider.GetRequiredService<SnapshotGroupCommand>());
root.Subcommands.Add(provider.GetRequiredService<TypeGroupCommand>());
// ... all root-level commands ...

// Run with top-level exception catch
try { return await root.Parse(args).InvokeAsync(); }
catch (VosCliException ex) { Console.Error.WriteLine($"Error: {ex.Message}"); return 1; }
```

### CLI files to create

| Directory | Files |
|---|---|
| `Symbols/` | `VosCliSymbols.cs` |
| `Exceptions/` | `VosCliException.cs`, `VosCliThrow.cs`, `ConfigExceptions.cs`, `LookupExceptions.cs`, `NetworkExceptions.cs`, `BackendExceptions.cs`, `CliExceptions.cs` (`RequiredArgumentMissingException`, `RequiredOptionMissingException`) |
| `Resolvers/` | One per command with >2 parsed values: `HaltCommandResolver.cs`, `TypeSetCommandResolver.cs`, `TypeAddCommandResolver.cs`, `MachineAddCommandResolver.cs`, `InstanceAddCommandResolver.cs`, etc. |
| `Commands/` | One class per leaf command. Groups: `Commands/Snapshot/`, `Commands/Type/`, `Commands/Type/VboxManage/`, `Commands/Machine/`, `Commands/Instance/`, `Commands/Network/`, `Commands/Box/`, `Commands/Config/` |
| `Output/` | `ResultPrinter.cs` — shared output formatting |
| root | `Program.cs` — wiring only |

---

## 6. Tests

### `Vos.Lib.Tests` — unit tests with fakes

Fakes (in `Fakes/` folder):
- `FakeVosConfigSerializer : IVosConfigSerializer` — in-memory dict keyed by path
- `FakeVosBackend : IVosBackend` — returns configurable `VosActionResult`
- `TestVosEventCollector` — subscribes to all events, exposes `List<VosEvent>` for assertions

Test classes (one per service, 8 total):
- `VosProjectServiceTests` — init/validate/resolve/config show
- `VosMachineTypeServiceTests` — CRUD + VBoxManage generation
- `VosMachineServiceTests` — add/remove/enable/disable
- `VosInstanceServiceTests` — add/remove/list
- `VosNetworkServiceTests` — generate/show + conflict detection
- `VosVmServiceTests` — VM lifecycle delegates to backend, emits events
- `VosBoxServiceTests` — box management delegates to backend
- `VosPackerServiceTests` — image build delegates to backend
- `VosEventEmitterTests` — subscribe/unsubscribe/typed filtering

### `Vos.IntegrationTests` — E2E Packer → Vos

**Trait**: `[Trait("Category", "Integration")]` (skipped in CI, requires VirtualBox)

**Test**: `PackerAlpineDockerHostToVosWorkflow`

```csharp
[Trait("Category", "Integration")]
public class PackerToVosIntegrationTests : IAsyncLifetime
{
    private readonly string _packerDir = Path.Combine(Path.GetTempPath(), $"vos-packer-{Guid.NewGuid():N}");
    private readonly string _vosDir = Path.Combine(Path.GetTempPath(), $"vos-test-{Guid.NewGuid():N}");
    private readonly List<VosEvent> _events = [];
    private ServiceProvider _provider = null!;
    private string? _boxName;
    private string? _boxFilePath;
    private string? _configPath;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_packerDir);
        Directory.CreateDirectory(_vosDir);

        var services = new ServiceCollection();
        services.AddVosLib();
        services.AddVosBackend<VagrantBackend>();
        _provider = services.BuildServiceProvider();
        _provider.GetRequiredService<IVosEventEmitter>().Subscribe(_events.Add);
    }

    [Fact]
    public async Task Build_Alpine_DockerHost_Image_And_Start_Vos_Instance()
    {
        // 1. Generate Packer project
        var packerConfig = new AlpinePackerConfig();
        var bundle = new PackerBundle();
        bundle.Apply(new AlpineBaseContributor(packerConfig), new DockerContributor());
        await new PackerBundleWriter().WriteAsync(bundle, _packerDir);

        // 2. Build image (packer init + packer build)
        var packerSvc = _provider.GetRequiredService<IVosPackerService>();
        var buildResult = await packerSvc.BuildAsync(_packerDir);
        buildResult.IsSuccess.ShouldBeTrue();
        _events.ShouldContain(e => e is BoxBuilt);

        // 3. Locate .box artifact
        var built = buildResult.Value;
        built.Artifacts.ShouldNotBeEmpty();
        _boxFilePath = built.Artifacts[0].Path;
        File.Exists(_boxFilePath).ShouldBeTrue();

        // 4. Add box to Vagrant
        _boxName = $"test/alpine-dockerhost-{Guid.NewGuid():N}";
        var boxSvc = _provider.GetRequiredService<IVosBoxService>();
        var addResult = await boxSvc.AddAsync(_boxFilePath, await new VosBoxAddOptionsBuilder()
            .WithName(_boxName)
            .BuildAsync());
        addResult.IsSuccess.ShouldBeTrue();
        _events.ShouldContain(e => e is BoxAdded);

        // 5. Create Vos config with DockerHostContributor
        var projectSvc = _provider.GetRequiredService<IVosProjectService>();
        await projectSvc.InitAsync(_vosDir);
        _configPath = Path.Combine(_vosDir, "config-vos.yaml");

        var mtSvc = _provider.GetRequiredService<IVosMachineTypeService>();
        await mtSvc.AddAsync(_configPath, "docker-host", _boxName, 2048, 2);
        // Apply DockerHostContributor defaults
        var machineType = new VosMachineType();
        new DockerHostContributor().Contribute(machineType);

        var machineSvc = _provider.GetRequiredService<IVosMachineService>();
        await machineSvc.AddAsync(_configPath, "main", "docker-host", 1);

        // 6. Validate
        var validateResult = await projectSvc.ValidateAsync(_configPath);
        validateResult.IsSuccess.ShouldBeTrue();

        // 7. Start VM
        var vmSvc = _provider.GetRequiredService<IVosVmService>();
        var upResult = await vmSvc.UpAsync(_configPath, "main-01");
        upResult.IsSuccess.ShouldBeTrue();
        _events.ShouldContain(e => e is VmStarted);

        // 8. Verify status
        var statusResult = await vmSvc.StatusAsync(_configPath);
        statusResult.IsSuccess.ShouldBeTrue();

        // 9. SSH echo
        var echoResult = await vmSvc.SshCommandAsync(_configPath, "main-01", "echo hello world");
        echoResult.IsSuccess.ShouldBeTrue();
        echoResult.Value.Output.ShouldContain("hello world");

        // 10. Docker hello-world
        var dockerResult = await vmSvc.SshCommandAsync(_configPath, "main-01", "docker run --rm hello-world");
        dockerResult.IsSuccess.ShouldBeTrue();
        dockerResult.Value.Output.ShouldContain("Hello from Docker!");

        // 11. Cleanup: destroy VM
        var destroyResult = await vmSvc.DestroyAsync(_configPath, "main-01", force: true);
        destroyResult.IsSuccess.ShouldBeTrue();
        _events.ShouldContain(e => e is VmDestroyed);

        // 12. Cleanup: deregister box from Vagrant
        var removeResult = await boxSvc.RemoveAsync(_boxName!);
        removeResult.IsSuccess.ShouldBeTrue();
        _events.ShouldContain(e => e is BoxRemoved);

        // 13. Cleanup: delete .box file
        if (File.Exists(_boxFilePath)) File.Delete(_boxFilePath);
    }

    public async Task DisposeAsync()
    {
        // Defensive cleanup (in case test fails mid-way)
        try
        {
            if (_configPath is not null)
            {
                var vmSvc = _provider.GetRequiredService<IVosVmService>();
                await vmSvc.DestroyAsync(_configPath, null, force: true);
            }
        }
        catch { /* best effort */ }

        try
        {
            if (_boxName is not null)
            {
                var boxSvc = _provider.GetRequiredService<IVosBoxService>();
                await boxSvc.RemoveAsync(_boxName, await new VosBoxRemoveOptionsBuilder()
                    .WithForce(true).WithAll(true).BuildAsync());
            }
        }
        catch { /* best effort */ }

        if (_boxFilePath is not null && File.Exists(_boxFilePath)) 
            File.Delete(_boxFilePath);
        if (Directory.Exists(_packerDir)) Directory.Delete(_packerDir, true);
        if (Directory.Exists(_vosDir)) Directory.Delete(_vosDir, true);

        await _provider.DisposeAsync();
    }
}
```

---

## 7. Files to create

### `Vos.Lib.Abstractions`

| File | Content |
|---|---|
| `FrenchExDev.Net.Vos.Lib.Abstractions.csproj` | net10.0, refs Vos + Result + Builder + Builder.Attributes + Builder.SourceGenerator |
| **IO** | |
| `IO/IFile.cs` | Interface: Name, Extension, FullPath, ReadContentAsync, WriteContentAsync, Exists |
| `IO/IFileSystem.cs` | Interface: GetFile, GetFiles, CreateDirectory, DirectoryExists, DeleteDirectory, DeleteFile |
| **Pipeline** | |
| `Pipeline/IVosRequest.cs` | `IVosRequest<TResult>`, marker interfaces `IConfigAware`, `IConfigMutating` |
| `Pipeline/IVosHandler.cs` | `IVosHandler<TRequest, TResult>` |
| `Pipeline/IVosBehavior.cs` | `IVosBehavior<TRequest, TResult>` — middleware around handler |
| **Validation** | |
| `Validation/IValidationRule.cs` | `IValidationRule<T>` — composable spec |
| `Validation/IValidationRuleProvider.cs` | `IValidationRuleProvider<T>` — collects rules |
| **Unit of Work** | |
| `IVosUnitOfWork.cs` | Config, Manager, CommitAsync, Rollback. `IAsyncDisposable` |
| `IVosUnitOfWorkFactory.cs` | `CreateAsync(configPath)` → `IVosUnitOfWork` |
| **Resilience** | |
| ~~`IResiliencePolicy.cs`~~ | Removed — using Polly `ResiliencePipeline` directly |
| **Cache** | |
| `IVosConfigCache.cs` | `GetOrLoadAsync`, `Invalidate`, `InvalidateAll` |
| **Events** | |
| `VosEvents.cs` | `abstract record VosEvent { Guid CorrelationId }` + all sealed records |
| `IVosEventEmitter.cs` | Emit, Subscribe, Subscribe<T> |
| **Services** | |
| `IVosProjectService.cs` | init, validate, resolve, config show, version, provision create/validate |
| `IVosMachineTypeService.cs` | type CRUD + VBoxManage + provisioning + shared-folder + plugin |
| `IVosMachineService.cs` | machine CRUD + enable/disable |
| `IVosInstanceService.cs` | instance CRUD |
| `IVosNetworkService.cs` | network generate/show |
| `IVosVmService.cs` | VM lifecycle, SSH/remote, snapshots, port, package |
| `IVosBoxService.cs` | box list/add/remove/update/prune/outdated/repackage |
| `IVosPackerService.cs` | box init, box build |
| `Options/VosUpOptions.cs` | `[Builder]` — VM up options |
| `Options/VosDestroyOptions.cs` | `[Builder]` — VM destroy options |
| `Options/VosProvisionOptions.cs` | `[Builder]` — provision/reload/resume options |
| `Options/VosSshOptions.cs` | `[Builder]` — SSH options |
| `Options/VosUploadOptions.cs` | `[Builder]` — upload options |
| `Options/VosPackageOptions.cs` | `[Builder]` — package options |
| `Options/VosWinrmOptions.cs` | `[Builder]` — WinRM options |
| `Options/VosSnapshotRestoreOptions.cs` | `[Builder]` — snapshot restore options |
| `Options/VosSnapshotPopOptions.cs` | `[Builder]` — snapshot pop options |
| `Options/VosBoxAddOptions.cs` | `[Builder]` — box add options |
| `Options/VosBoxRemoveOptions.cs` | `[Builder]` — box remove options |
| `Options/VosBoxUpdateOptions.cs` | `[Builder]` — box update options |
| `Options/VosBoxPruneOptions.cs` | `[Builder]` — box prune options |
| `Options/VosBoxOutdatedOptions.cs` | `[Builder]` — box outdated options |
| `Options/VosPackerBuildOptions.cs` | `[Builder]` — packer build options |
| `Options/MachineTypeSettings.cs` | `[Builder]` — type set options |
| `Options/VosProvisioningStepOptions.cs` | `[Builder]` — provisioning step options |
| `VosLibOptions.cs` | Configuration binding |

### `Vos.VosFile`

| File | Content |
|---|---|
| `FrenchExDev.Net.Vos.VosFile.csproj` | refs Vos, YamlDotNet, Result, Builder, Builder.Attributes, Builder.SG |
| `IVosFileReader.cs` | `ReadAsync(path, ct)` → `Result<VosConfig>` |
| `IVosFileWriter.cs` | `WriteAsync(config, path, ct)` |
| `IVosFileValidator.cs` | `Validate(config)` → `Result<IReadOnlyList<string>>` |
| `VosFileReader.cs` | Schema detection (v1/v2), env var substitution, local override deep-merge |
| `VosFileWriter.cs` | Atomic write with underscore naming, omit nulls, set schema_version: 1 |
| `VosFileValidator.cs` | All validation rules (references, IPs, duplicates, script existence) |
| `VosFileLock.cs` | File-based lock (`path.lock`, 5s timeout, IDisposable) |
| `VosSchemaVersion.cs` | Constant: Current = 1 (reserved for future evolution) |
| `EnvVarResolver.cs` | Scans YAML strings for `${VAR}`, resolves from env |
| `Schema/VosFileConfig.cs` | `[Builder]` — root config: schema_version, format, vagrant, machine_types, machines |
| `Schema/VosFileVagrantConfig.cs` | `[Builder]` — vagrant section: plugins, virtualbox defaults, naming-pattern |
| `Schema/VosFilePluginConfig.cs` | `[Builder]` — plugin: enabled, config dict |
| `Schema/VosFileVirtualBoxDefaults.cs` | `[Builder]` — linked_clones, check_guest_additions, manage, setproperty, storagectl, storageattach |
| `Schema/VosFileMachineType.cs` | `[Builder]` — full machine type: box, provider, provisioning, shared_folders, files, disks, commands, variables |
| `Schema/VosFileMachine.cs` | `[Builder]` — machine: is_enabled, machine_type_name, overrides |
| `Schema/VosFileInstance.cs` | `[Builder]` — instance: name, networking array |
| `Schema/VosFileNetworkInterface.cs` | `[Builder]` — kind, ip, mac, network_bridge |
| `Schema/VosFileProvisioningStep.cs` | `[Builder]` — key, version, ext, enabled, privileged, reload:before/after, env |
| `Schema/VosFileSharedFolder.cs` | `[Builder]` — host_path, guest_path, type, enabled, disabled |
| `Schema/VosFileFileProvision.cs` | `[Builder]` — source, destination, enabled |
| `Schema/VosFileDisk.cs` | `[Builder]` — name, size, primary |

### `Vos.VosFile.Tests`

| File | Content |
|---|---|
| `FrenchExDev.Net.Vos.VosFile.Tests.csproj` | refs VosFile + xUnit + Shouldly |
| `VosFileReaderTests.cs` | Read config, env var substitution, local override merge |
| `VosFileWriterTests.cs` | Write + read round-trip, atomic write, schema_version |
| `VosFileValidatorTests.cs` | All validation rules |
| `VosFileLockTests.cs` | Concurrent access, timeout |
| `EnvVarResolverTests.cs` | `${VAR}` substitution, missing var → failure |
| `Schema/VosFileConfigBuilderTests.cs` | Builder fluent API for root config |
| `Schema/VosMachineTypeBuilderTests.cs` | Builder for machine type |

### `Vos.Bundle`

| File | Content |
|---|---|
| `FrenchExDev.Net.Vos.Bundle.csproj` | refs Vos, Builder, Builder.Attributes, Builder.SG |
| `VosBundle.cs` | Mutable workspace: Config + Files, Apply() contributor pipeline |
| `VosBundleFile.cs` | `record VosBundleFile(Directory, FileName, Content)` |
| `IVosBundleContributor.cs` | `void Contribute(VosBundle)` |
| `VosBundleWriter.cs` | Materializes to disk: YAML + Vagrantfile + scripts + files |
| `VosBundleMachineType.cs` | `[Builder]` — machine type with all properties |
| `VosBundleInstance.cs` | `[Builder]` — instance with networking array |
| `VosBundleProvisioningStep.cs` | `[Builder]` — provisioning step |
| `VosBundleSharedFolder.cs` | `[Builder]` — shared folder |
| `VosBundleNetworkInterface.cs` | Record: kind, ip, mac, bridge |
| `VosBundleVboxManageCommand.cs` | Record: args list |
| `Resources/Vagrantfile` | Static Vagrantfile (embedded resource, copied by writer) |

### `Vos.Bundle.Tests`

| File | Content |
|---|---|
| `FrenchExDev.Net.Vos.Bundle.Tests.csproj` | refs Bundle + xUnit + Shouldly |
| `VosBundleTests.cs` | Apply contributors, verify config + files |
| `VosBundleWriterTests.cs` | Write to temp dir, verify YAML + Vagrantfile + scripts on disk |
| `VosBundleMachineTypeBuilderTests.cs` | Builder fluent API + validation |

### `Vos.Lib`

| File | Content |
|---|---|
| `FrenchExDev.Net.Vos.Lib.csproj` | refs Abstractions + VosFile + Bundle + M.E.Logging.Abstractions + System.Diagnostics.DiagnosticSource. **No M.E.DI** |
| **Pipeline** | |
| `Pipeline/VosPipeline.cs` | Composes behaviors around handler, executes in order |
| `Pipeline/LoggingBehavior.cs` | Structured log entry/exit with duration |
| `Pipeline/TracingBehavior.cs` | OpenTelemetry `Activity` span with tags |
| `Pipeline/ValidationBehavior.cs` | Runs `IValidationRule<TRequest>` specs, short-circuits on failure |
| `Pipeline/ConfigLoadBehavior.cs` | Loads config for `IConfigAware` requests |
| `Pipeline/LockingBehavior.cs` | Acquires `VosFileLock` for `IConfigMutating` requests |
| `Pipeline/SaveBehavior.cs` | Saves config + invalidates cache for `IConfigMutating` requests |
| `Pipeline/EventBehavior.cs` | Emits Starting/Completed events |
| **Infrastructure** | |
| `VosEventEmitter.cs` | Thread-safe impl (ConcurrentBag + lock-free emit) |
| `VosUnitOfWork.cs` | Load once → batch mutations → commit once |
| `VosConfigCache.cs` | ConcurrentDictionary + FileSystemWatcher invalidation |
| `VosResiliencePolicy.cs` | Retry (3x exponential), timeout, circuit breaker |
| `NullVosBackend.cs` | Fallback: all methods return `Result.Failure("No backend")` |
| `NullFileSystem.cs` | Fallback: throws clear message if used |
| `Log.cs` | `LoggerMessage.Define` — all structured log messages |
| **Validation rules** | |
| `Validation/MachineTypeReferenceRule.cs` | Every machine refs existing type |
| `Validation/UniqueInstanceNameRule.cs` | No duplicate instance names |
| `Validation/MachineTypeHasBoxRule.cs` | Enabled types have a box |
| `Validation/InstanceIpFormatRule.cs` | IPs are valid |
| `Validation/InstanceNameNotEmptyRule.cs` | Names not blank |
| `Validation/ProvisioningScriptExistsRule.cs` | Scripts exist on disk |
| `Validation/NoIpConflictRule.cs` | No shared IPs |
| **Handlers** | |
| `Handlers/VosProjectHandler.cs` | init, validate, resolve, config show |
| `Handlers/VosMachineTypeHandler.cs` | type CRUD + VBoxManage + provisioning + shared-folder + plugin |
| `Handlers/VosMachineHandler.cs` | machine CRUD |
| `Handlers/VosInstanceHandler.cs` | instance CRUD |
| `Handlers/VosNetworkHandler.cs` | network generate/show |
| `Handlers/VosVmHandler.cs` | VM lifecycle, SSH/remote, snapshots |
| `Handlers/VosBoxHandler.cs` | box management |
| `Handlers/VosPackerHandler.cs` | packer init/build |
| `Completions/VosCompletionHelper.cs` | Shared config loading for completions |
| `Completions/VosMachineTypeNameCompleter.cs` | Completes machine type names from config |
| `Completions/VosMachineGroupNameCompleter.cs` | Completes machine names from config |
| `Completions/VosSnapshotNameCompleter.cs` | Completes snapshot names via backend |
| `Completions/VosBoxNameCompleter.cs` | Completes installed box names via backend |
| `Completions/VosProvisioningKeyCompleter.cs` | Completes provisioning step keys from config |
| `Completions/VosProviderNameCompleter.cs` | Static: virtualbox, hyperv, parallels, docker |

### `Vos.Lib.Tests`

| File | Content |
|---|---|
| `FrenchExDev.Net.Vos.Lib.Tests.csproj` | refs Lib + Abstractions + xUnit + Shouldly |
| `Fakes/InMemoryFile.cs` | `IFile` impl backed by `string` in memory |
| `Fakes/InMemoryFileSystem.cs` | `IFileSystem` impl backed by `Dictionary<string, string>` |
| `Fakes/FakeVosFileReader.cs` | In-memory reader (returns preconfigured VosConfig) |
| `Fakes/FakeVosFileWriter.cs` | Captures writes in memory dict keyed by path |
| `Fakes/FakeVosBackend.cs` | Configurable backend |
| `Fakes/TestVosEventCollector.cs` | Event collector for assertions |
| `VosProjectServiceTests.cs` | Tests |
| `VosMachineTypeServiceTests.cs` | Tests |
| `VosMachineServiceTests.cs` | Tests |
| `VosInstanceServiceTests.cs` | Tests |
| `VosNetworkServiceTests.cs` | Tests |
| `VosVmServiceTests.cs` | Tests |
| `VosBoxServiceTests.cs` | Tests |
| `VosPackerServiceTests.cs` | Tests |
| `VosEventEmitterTests.cs` | Tests |

### `Vos.IntegrationTests`

| File | Content |
|---|---|
| `FrenchExDev.Net.Vos.IntegrationTests.csproj` | refs Lib + Infra.Vagrant + Packer.Alpine + Packer.Alpine.DockerHost + Packer.Bundle + Vos.Alpine.DockerHost |
| `PackerToVosIntegrationTests.cs` | E2E workflow (see below) |

## 8. Files to modify

| File | Change |
|---|---|
| `Net/FrenchExDev/Directory.Packages.props` | Add M.E.Configuration.Abstractions, M.E.Configuration, M.E.Configuration.Json, M.E.Options.ConfigurationExtensions, M.E.Logging, M.E.Logging.Abstractions, M.E.Logging.Console |
| `Vos/src/FrenchExDev.Net.Vos/IVosBackend.cs` | Add option record parameters to methods (VosUpOptions?, VosDestroyOptions?, etc.) |
| `Vos/src/FrenchExDev.Net.Vos/VosConfigManager.cs` | Add provisioning step remove/enable/disable/move, shared folder remove, plugin remove |
| `Vos/src/FrenchExDev.Net.Vos.Infra.Vagrant/VagrantBackend.cs` | Fix bugs (upload, snapshots, box name) + pass new option records to Vagrant client |
| `Vos/src/FrenchExDev.Net.Vos.Infra.FileSystem/` | Repurpose: remove VosConfigSerializer + VagrantfileRenderer, add `PhysicalFile : IFile` + `PhysicalFileSystem : IFileSystem`. Ref Lib.Abstractions |
| `Vos/src/FrenchExDev.Net.Vos.Infra.PowerShell/VosCmdletBase.cs` | Inject Lib services via DI instead of `new VagrantBackend()` |
| `Vos/src/FrenchExDev.Net.Vos.Cli/FrenchExDev.Net.Vos.Cli.csproj` | Ref Vos.Lib + Infra.Vagrant + Infra.FileSystem + M.E.DI + M.E.Configuration + System.CommandLine (SG generates `AddXxxInjectables` from each) |
| `Vos/src/FrenchExDev.Net.Vos.Cli/Program.cs` | Rewrite as thin DI wrapper with `AddCompletions()` on all args/options |
| `Vos/FrenchExDev.Net.Vos.slnx` | Add 8 new projects (VosFile, VosFile.Tests, Bundle, Bundle.Tests, Lib.Abstractions, Lib, Lib.Tests, IntegrationTests) |

---

## 9. Integration test: Packer Alpine DockerHost → Vos E2E

**Trait**: `[Trait("Category", "Integration")]` — skipped in CI, requires VirtualBox + Vagrant + Packer installed.

### Workflow steps

| Step | Action | Asserts | Events expected |
|---|---|---|---|
| **1. Generate Packer project** | `PackerBundle.Apply(new AlpineBaseContributor(config), new DockerContributor())` then `PackerBundleWriter.WriteAsync(bundle, tempDir)` | HCL files exist on disk | — (Packer layer, not Vos events) |
| **2. Packer init + build** | `IVosPackerService.BuildAsync(tempDir, vars: null, force: false)` | `result.IsSuccess`, artifacts non-empty, `.box` file exists | `BoxBuilding`, `BoxArtifactProduced`, `BoxBuilt` |
| **3. Add box** | `IVosBoxService.AddAsync(boxFilePath)` | `result.IsSuccess` | `BoxAdding`, `BoxAdded` |
| **4. Create Vos config** | `IVosProjectService.InitAsync(testDir)` then apply `DockerHostContributor` to machine type, set box to built artifact name | config-vos.yaml + Vagrantfile exist | `ProjectInitializing`, `FileCreated`(x2), `VagrantfileGenerated`, `ProjectInitialized` |
| **5. Validate config** | `IVosProjectService.ValidateAsync(configPath)` | `result.IsSuccess`, 0 errors | `ValidationStarted`, `ValidationCompleted` |
| **6. Start VM** | `IVosVmService.UpAsync(configPath, instanceName)` | `result.IsSuccess` | `VmStarting`, `VmStarted` |
| **7. Check status** | `IVosVmService.StatusAsync(configPath)` | Output contains "running" | `VmStatusQuerying`, `VmStatusQueried` |
| **8. SSH echo** | `IVosVmService.SshCommandAsync(configPath, instanceName, "echo hello world")` | Output contains "hello world" | `SshCommandExecuting`, `SshCommandExecuted` |
| **9. Docker hello-world** | `IVosVmService.SshCommandAsync(configPath, instanceName, "docker run --rm hello-world")` | Output contains "Hello from Docker!" | `SshCommandExecuting`, `SshCommandExecuted` |
| **10. Destroy VM** | `IVosVmService.DestroyAsync(configPath, instanceName, force: true)` | `result.IsSuccess` | `VmDestroying`, `VmDestroyed` |
| **11. Remove box** | `IVosBoxService.RemoveAsync(boxName)` | `result.IsSuccess` | `BoxRemoving`, `BoxRemoved` |
| **12. Cleanup** | Delete tempDir + testDir | Dirs removed | — |

### Test structure

See section 6 → `Vos.IntegrationTests` for the full `PackerToVosIntegrationTests` class with `IAsyncLifetime`, 13 steps, and defensive `DisposeAsync` cleanup.

---

## 10. Verification + QualityGate

### Build + test

```pwsh
cd Net/FrenchExDev/Vos
dotnet build FrenchExDev.Net.Vos.slnx
dotnet test --filter "Category!=Integration"          # unit tests only
dotnet test --filter "Category=Integration"            # E2E (requires VirtualBox)
```

### QualityGate — 100% branch/line coverage, quality score 1.0

Run via existing QualityGate CLI:

```pwsh
dotnet run --project ../QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test
```

Existing `quality-gate.yml` already configured. Gates enforced:

| Gate | Target |
|---|---|
| Line coverage | 100% |
| Branch coverage | 100% |
| Test quality score | 1.0 |
| Max cyclomatic complexity | 25 |
| Max cognitive complexity | 25 |
| Max class coupling | 70 |
| Max LCOM | 50 |
| Max duplication | 5% |
| Min maintainability index | 60 |

### Coverage configuration

Each test project needs `coverlet.collector` + `coverage.runsettings`:

```xml
<!-- coverage.runsettings (at Vos/ root, shared by all test projects) -->
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Include>[FrenchExDev.Net.Vos*]*</Include>
          <Exclude>[*Tests]*,[*Fakes]*</Exclude>
          <ExcludeByAttribute>GeneratedCodeAttribute,ExcludeFromCodeCoverage</ExcludeByAttribute>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### Test project coverage mapping

| Test project | Covers |
|---|---|
| `Vos.Tests` | `Vos` (core domain) |
| `Vos.VosFile.Tests` | `Vos.VosFile` |
| `Vos.Bundle.Tests` | `Vos.Bundle` |
| `Vos.Lib.Tests` | `Vos.Lib`, `Vos.Lib.Abstractions` (VosEventEmitter, services) |
| `Vos.IntegrationTests` | E2E (not counted for coverage — Category=Integration excluded) |

All source-generated `[Builder]` code is excluded via `GeneratedCodeAttribute`.
