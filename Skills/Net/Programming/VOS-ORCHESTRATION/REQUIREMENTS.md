# VOS-ORCHESTRATION — Requirements

A VM-orchestration library following this pattern MUST satisfy the following.

## Layered project structure

- [ ] Projects are organized into Layers 0–4 with a strict DAG (no project
      in layer N depends on layer ≥ N).
- [ ] Layer 0: core domain (`Vos`).
- [ ] Layer 1: `Vos.VosFile` (YAML I/O), `Vos.Bundle` (in-memory workspace).
- [ ] Layer 2: `Vos.Lib.Abstractions` (interfaces + option records).
- [ ] Layer 3: `Vos.Lib` (implementations) + at least two `Vos.Infra.*`
      backend projects.
- [ ] Layer 4: `Vos.Cli` (System.CommandLine v2).
- [ ] All package versions are declared in `Directory.Packages.props`.

## YAML config contract

- [ ] User-facing config is a YAML file (`config-vos.yaml` by convention).
- [ ] The file always carries a `schema_version` field (currently `1`).
- [ ] A local override file (`local/config-vos-local.yaml`) is supported and
      deep-merged into the base.
- [ ] The reader exposes both layers (`VosConfigLayers`) and the resolved
      result.
- [ ] `${ENV_VAR}` substitution runs on all string values before
      deserialization.
- [ ] The writer is atomic (temp file + rename) and acquires a file lock.

## Service interfaces

- [ ] At least 6 service interfaces exist, each narrow:
      `IVosMachineService`, `IVosBoxService`, `IVosNetworkService`,
      `IVosSnapshotService`, `IVosPackerService`, `IVosVmService`.
- [ ] Each method takes either a primitive arg or a `[Builder]` option
      record. No method has more than 2 positional args (besides
      `CancellationToken`).
- [ ] Every implementation in `Vos.Lib` is annotated `[Injectable]`.
- [ ] An auto-generated extension method
      (`AddFrenchExDevNet{Lib}Injectables`) registers all services.

## Option records

- [ ] All multi-parameter options are `[Builder]`-decorated classes.
- [ ] All `bool` properties are `bool?` (SG limitation).
- [ ] Dictionary properties are concrete `Dictionary<,>`, not
      `IReadOnlyDictionary<,>`.

## Backend abstraction

- [ ] `IVosBackend` exists with at least: `UpAsync`, `HaltAsync`,
      `DestroyAsync`, `StatusAsync`, `SshAsync`, plus snapshot ops.
- [ ] `IVosBackend.SupportedActions` is exposed as `IReadOnlySet<VosAction>`.
- [ ] At least two backends exist (`VagrantBackend`, `PodmanMachineBackend`).
- [ ] Both backends delegate to a typed CLI client (BinaryWrapper-generated),
      never to `Process.Start` directly.
- [ ] Unsupported actions return `Result.Failure(NotSupported)` — never
      silently ignored.

## Machine type contributors

- [ ] `IMachineTypeContributor` exists with a single
      `void Contribute(VosMachineType machineType)` method.
- [ ] At least one downstream package (e.g. `Vos.Alpine`) ships a
      contributor.
- [ ] Contributors are non-destructive by default (`??=`, `Contains` checks
      before adding).

## Bundle pattern

- [ ] `VosBundle` is a mutable class, not a record.
- [ ] `IVosBundleContributor` exists with a single
      `void Contribute(VosBundle bundle)` method.
- [ ] `VosBundleWriter` materializes a bundle: `config-vos.yaml`, embedded
      `Vagrantfile`, provisioning scripts, shared files.
- [ ] The Vagrantfile is shipped as an **embedded resource** in
      `Vos.Bundle`. It is static — it reads `config-vos.yaml` at runtime.

## Event system

- [ ] `IVosEventEmitter` exists with one `void Emit(VosEvent evt)` method.
- [ ] All events are `record` types deriving from `VosEvent`.
- [ ] Every `VosEvent` carries a `CorrelationId` (Guid) and a `DateTimeOffset At`.
- [ ] Each service emits events for state transitions: `*Requested`,
      `*Started`, `*Completed`, `*Failed`.
- [ ] At least one console subscriber (CLI) and one recording subscriber
      (tests) exist.

## Validation

- [ ] `IVosFileValidator` returns `Result<IReadOnlyList<string>>` from
      `Validate(VosConfig)`.
- [ ] Validation rules cover at minimum: machine names unique, IP ranges
      valid, provisioning scripts exist on disk, network configuration
      consistent.
- [ ] Pre-flight capability checks against `IVosBackend.SupportedActions`
      run before invoking any backend method.

## CLI

- [ ] Built on `System.CommandLine` v2 using a Symbols/Throw/Resolver/Command
      pattern.
- [ ] Each subcommand is its own class.
- [ ] Mutating commands accept `--local` to target the local override layer.
- [ ] Completers are registered for `--machine`, `--provider`, `--snapshot`.

## PowerShell

- [ ] `Vos.Infra.PowerShell` exists with cmdlets that inject Lib services
      via constructor.
- [ ] An alias (e.g. `gvm`) is shipped for legacy ergonomic compatibility.
- [ ] Cmdlets bind `IVosEventEmitter` to `WriteVerbose` / `WriteProgress`.

## Tests

- [ ] Hand-written `Fakes` exist in `test/.../Fakes/` for every service
      interface and `IVosBackend`.
- [ ] No mocking framework is referenced anywhere in the test projects.
- [ ] An integration test exists that exercises a real backend end-to-end:
      `up → ssh → destroy`.

## DI

- [ ] All Lib classes accept dependencies as **optional** constructor
      parameters with sensible defaults.
- [ ] No DI container is required to construct services.
- [ ] Production tooling can opt into DI via the SG-generated extension.

## Anti-requirements

- [ ] No invention of a new VM API. The backends ARE Vagrant / Podman Machine.
- [ ] No mocking framework (Moq, NSubstitute, FakeItEasy).
- [ ] No `Process.Start` calls outside of BinaryWrapper-generated clients.
- [ ] No silent ignoring of unsupported actions.
- [ ] No `bool` (non-nullable) in `[Builder]` option records.
- [ ] No `IReadOnlyDictionary` in `[Builder]` option records.
- [ ] No edits to the embedded Vagrantfile from C# code.
- [ ] No hand-written backend that bypasses `IVosBackend.SupportedActions`.
- [ ] No hand-written DI registration code (use `[Injectable]`).
