# VOS-ORCHESTRATION — How-To

## Author a `.vosfile`

Drop a `config-vos.yaml` at the project root:

```yaml
schema_version: 1

machine_types:
  alpine-docker:
    box: frenchexdev/alpine-3.21-virt
    box_version: "1.0.0"
    memory: 4096
    cpus: 4
    provider: virtualbox
    plugins:
      - vagrant-hostmanager
      - vagrant-vbguest
    provisioning:
      - key: install-docker
        version: "1.0"
    shared_folders:
      - host_path: ./docker-compose
        guest_path: /opt/docker-compose

instances:
  - name: dev
    hostname: dev.local
    networking:
      - kind: private_network
        ip: 192.168.56.10

variables:
  ALPINE_VERSION: "3.21"
```

For per-developer overrides, add `local/config-vos-local.yaml` (gitignored):

```yaml
instances:
  - name: dev
    networking:
      - kind: private_network
        ip: 192.168.56.99   # personal IP, deep-merged over base
```

## Use the CLI

```bash
vos init                      # scaffold .vosfile + Vagrantfile
vos validate                  # static + schema validation
vos up dev                    # bring up the dev instance
vos status                    # list machines
vos ssh dev                   # ssh into dev
vos halt dev                  # graceful shutdown
vos destroy dev               # tear down
vos snapshot save dev pre-test
vos snapshot restore dev pre-test
```

Mutating commands take `--local` to target the local override layer:

```bash
vos config set instances.0.memory 8192 --local
```

## Use the Lib programmatically

```csharp
var reader   = new VosFileReader();
var executor = new VosMachineService(
    backend: new VagrantBackend(),
    fileReader: reader,
    eventEmitter: new ConsoleVosEventEmitter());

var configResult = await reader.ReadAsync("config-vos.yaml", ct);
var config = configResult.ValueOrThrow();

var upOptions = new VosUpOptionsBuilder()
    .WithMachineName("dev")
    .WithProvider("virtualbox")
    .WithProvision(true)
    .Build().Value;

var result = await executor.UpAsync(upOptions, ct);
```

## Use the PowerShell cmdlets

```powershell
Import-Module FrenchExDev.Net.Vos.Infra.PowerShell

Get-VosMachine
Start-VosMachine -Name dev -Verbose
Stop-VosMachine -Name dev
Remove-VosMachine -Name dev -Force

# gvm alias for muscle memory
gvm up dev
gvm ssh dev
```

## Write a machine type contributor

```csharp
public sealed class GitLabRunnerContributor : IMachineTypeContributor
{
    private readonly string _runnerVersion;

    public GitLabRunnerContributor(string runnerVersion = "latest")
        => _runnerVersion = runnerVersion;

    public void Contribute(VosMachineType machineType)
    {
        machineType.Box ??= "frenchexdev/alpine-3.21-virt";
        machineType.Memory = Math.Max(machineType.Memory, 4096);
        machineType.Cpus   = Math.Max(machineType.Cpus, 4);

        if (!machineType.Plugins.Contains("vagrant-vbguest"))
            machineType.Plugins.Add("vagrant-vbguest");

        machineType.Provisioning.Add(new VosProvisioningStep
        {
            Key = "install-gitlab-runner",
            Version = "1.0",
            Privileged = true,
            Env = { ["RUNNER_VERSION"] = _runnerVersion }
        });

        machineType.Variables["GITLAB_RUNNER_VERSION"] = _runnerVersion;
    }
}
```

Apply contributors:

```csharp
var machineType = new VosMachineType();
new AlpineVirtualBoxContributor("3.21").Contribute(machineType);
new DockerHostContributor().Contribute(machineType);
new GitLabRunnerContributor("17.5").Contribute(machineType);
```

## Write a backend

```csharp
public sealed class MultipassBackend : IVosBackend
{
    public string Name => "multipass";

    public IReadOnlySet<VosAction> SupportedActions { get; } =
        new HashSet<VosAction>
        {
            VosAction.Up, VosAction.Halt, VosAction.Destroy,
            VosAction.Status, VosAction.Ssh
            // no snapshots, no provisioning
        };

    public Task<Result> UpAsync(VosMachine m, VosBackendOptions o, CancellationToken ct) { /* ... */ }
    // ...
    public Task<Result> SnapshotSaveAsync(VosMachine m, string name, CancellationToken ct)
        => Task.FromResult(Result.Failure("Snapshots not supported by multipass backend"));
}
```

Returning `NotSupported` cleanly is mandatory. Silently ignoring breaks
Liskov.

## Write a fake for tests

```csharp
internal sealed class FakeVosBackend : IVosBackend
{
    public string Name => "fake";
    public IReadOnlySet<VosAction> SupportedActions { get; } =
        Enum.GetValues<VosAction>().ToHashSet();

    public List<string> Calls { get; } = [];

    public Task<Result> UpAsync(VosMachine m, VosBackendOptions o, CancellationToken ct)
    {
        Calls.Add($"Up:{m.Name}");
        return Task.FromResult(Result.Success);
    }
    // ... other methods
}

[Fact]
public async Task Up_invokes_backend()
{
    var backend = new FakeVosBackend();
    var svc = new VosMachineService(backend: backend, /* ... */);

    await svc.UpAsync(new VosUpOptionsBuilder().WithMachineName("dev").Build().Value, default);

    backend.Calls.Should().Contain("Up:dev");
}
```

## Author option classes

Every multi-parameter service method takes a `[Builder]` option record:

```csharp
[Builder]
public sealed class VosUpOptions
{
    public string MachineName { get; init; } = "";
    public string? Provider { get; init; }
    public bool? Provision { get; init; }                 // bool? not bool
    public bool? DestroyOnError { get; init; }
    public Dictionary<string, string> Env { get; init; } = [];  // concrete type
    public TimeSpan? Timeout { get; init; }
}
```

The Builder source generator emits `VosUpOptionsBuilder` with `WithMachineName`,
`WithProvider`, etc.

## Bump the schema

1. Add fields to the C# `VosConfig` graph.
2. Increment `VosSchemaVersion.Current` if the change is breaking.
3. Update `VosFileReader` if the deserializer needs new converters.
4. Add a migration step in `VosFileValidator` for the old version.
5. Add tests covering both old and new schema variants.

## Things to never do

- Never put `bool` (non-nullable) on a `[Builder]` option class. The SG
  cannot distinguish "set to false" from "default".
- Never use `IReadOnlyDictionary<,>` on a `[Builder]` option class. The SG
  cannot assign through the readonly indexer.
- Never silently ignore an unsupported action in a backend. Return
  `Result.Failure(NotSupported)`.
- Never bypass the event emitter — every state transition emits an event.
- Never reach into `Vagrantfile` text from C# code. The bundle ships an
  embedded static `Vagrantfile` that reads `config-vos.yaml` at runtime; do
  not parse or rewrite it.
- Never use a mocking framework. Hand-written fakes only.
