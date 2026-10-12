# FrenchExDev.Net.Vagrant

A strongly-typed .NET wrapper for [HashiCorp Vagrant](https://www.vagrantup.com/), built on the BinaryWrapper source generator framework. Every Vagrant CLI command is represented as a typed C# class with fluent builder, version gating, and structured output parsing -- all generated at compile time from scraped help data across multiple Vagrant versions.

## Quick start

```csharp
using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.Vagrant;

// Create a binding to the local vagrant binary
var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("vagrant"),
    ExecutablePath = "/usr/bin/vagrant",
    DetectedVersion = SemanticVersion.Parse("2.4.9")
};

// Create the typed client
var client = Vagrant.Create(binding);

// Build a command using the fluent API
var upCommand = client.Up(b => b
    .WithProvider("virtualbox")
    .WithNoColor(true)
    .WithMachineReadable(true)
);

// Execute via CommandExecutor with structured output
var executor = new CommandExecutor(resolver);
var result = await executor.ExecuteAsync(
    new BinaryIdentifier("vagrant"),
    upCommand,
    new VagrantOutputParser(),
    new VagrantUpCollector()
);

// result.Success, result.MachinesReady, result.Errors
```

## Projects

| Project | Purpose |
|---------|---------|
| `FrenchExDev.Net.Vagrant` | Main library -- descriptor, parsers, events, result types |
| `FrenchExDev.Net.Vagrant.Design` | Design-time scraping tool (podman containers) |
| `FrenchExDev.Net.Vagrant.Tests` | xUnit test suite with fuzz and integration tests |

## How it works

1. **Scrape** -- The Design tool fetches every Vagrant release from HashiCorp, spins up podman containers for each version, and runs `vagrant <cmd> -h` recursively to capture the full command tree
2. **JSON** -- Scraped data is persisted as `scrape/vagrant-{version}.json` files (versions 2.4.3--2.4.9 included)
3. **Generate** -- The BinaryWrapper source generator reads the JSON at compile time and emits:
   - `VagrantXxxCommand` -- sealed `ICliCommand` with typed `init` properties
   - `VagrantXxxCommandBuilder` -- fluent builder with `With*()` methods and per-property validation
   - `VagrantClient` -- entry-point client with command groups (Box, Cloud, Plugin, Snapshot)
4. **Execute** -- `CommandExecutor` runs the binary, pipes output through `IOutputParser<VagrantEvent>`, and aggregates via `IResultCollector`

## Command groups

The generated `VagrantClient` exposes top-level commands and nested command groups:

```
client.Up(...)               -- vagrant up
client.Destroy(...)          -- vagrant destroy
client.Halt(...)             -- vagrant halt
client.Ssh(...)              -- vagrant ssh
client.Box.Add(...)          -- vagrant box add
client.Box.List(...)         -- vagrant box list
client.Cloud.Auth.Login(...) -- vagrant cloud auth login
client.Plugin.Install(...)   -- vagrant plugin install
client.Snapshot.Save(...)    -- vagrant snapshot save
```

## Output parsing

Two parsers handle Vagrant's output formats:

- **`VagrantOutputParser`** -- Parses standard human-readable output (`==> default: ...`) into typed events
- **`VagrantMachineReadableParser`** -- Parses `--machine-readable` CSV format (`timestamp,target,type,data...`) with `%!(VAGRANT_COMMA)` unescaping

Both implement `IOutputParser<VagrantEvent>` and produce the shared event hierarchy:

| Event | Description |
|-------|-------------|
| `VagrantMachineOutput` | Normal machine output (`==> default: Importing...`) |
| `VagrantMachineError` | Error output (`==> default (error): ...`) |
| `VagrantProvisionerOutput` | Provisioner messages (indented `    default: ...`) |
| `VagrantActionCompleted` | Machine booted and ready / failed |
| `VagrantMachineReadableEvent` | Raw machine-readable CSV row |
| `VagrantOutputLine` | Unrecognized output or stderr |

## Version support

Commands and options are annotated with `[SinceVersion]` / `[UntilVersion]` attributes. The client enforces these at runtime:

```csharp
// Throws CommandNotSupportedException if detected version < 2.4.3
client.Up(b => b.WithProvider("virtualbox"));
```

## Documentation

- [Architecture](doc/ARCHITECTURE.md) -- Source generator pipeline, code structure, design decisions
- [How-To Guide](doc/HOW-TO.md) -- Scraping new versions, extending parsers, running tests

## Requirements

- .NET 10.0 (preview)
- For scraping: podman (or docker via `--runtime docker`)

## Shared dependency and version images

The Design runner prepares the shared system dependencies once, then installs each
software version in an image derived from that base. `UseVersionImage().UseContainer()`
builds or reuses the image before collecting help. After collection, the container
and version image are removed; `--keep-images` retains the version image. The
shared base remains cached.

From the wrapper directory, with Podman running (or add `--runtime docker`):

```powershell
$design = './src/FrenchExDev.Net.Vagrant.Design/FrenchExDev.Net.Vagrant.Design.csproj'
dotnet run --project $design --framework net10.0 -- --help
dotnet run --project $design --framework net10.0 -- --build-base
dotnet run --project $design --framework net10.0 -- --list --missing
# Review the selection; optionally narrow it with --min-version.
dotnet run --project $design --framework net10.0 -- --build-images --missing --parallel 2
dotnet run --project $design --framework net10.0 -- --missing --parallel 2
dotnet run --project $design --framework net10.0 -- --reparse
dotnet run --project $design --framework net10.0 -- --clean-images
```

`--build-base` prepares only dependencies. `--build-images` installs selected versions
without scraping and keeps their images. `--clean-images` removes this wrapper's
version images before its base, without forcing removal. Base preparation and cleanup
do not query the version collector; `--list` and `--reparse` do not build images.
With `--missing`, selection is based on missing JSON files.

The three image operations are mutually exclusive and cannot be combined with
`--reparse` or known-missing management. `--build-base` and `--clean-images` also
reject `--list` and `--missing`.

The PowerShell launcher exposes `-BuildBase`, `-BuildImages`, `-CleanImages`,
`-KeepImages`, `-Reparse`, `-Missing`, `-List`, `-MinVersion`, `-Parallel`,
`-ScrapeParallel`, `-Runtime`, `-Output` and `-Framework`. It resolves its project
relative to the script, so it also works from another directory:

```powershell
./scripts/Find-Missing.ps1 -BuildBase -Framework net10.0
./scripts/Find-Missing.ps1 -BuildImages -Missing -Parallel 2 -Framework net10.0
```

See the [BinaryWrapper image pipeline guide](../BinaryWrapper/doc/UPGRADE-IMAGE-PIPELINES.md)
for each client's dependencies, cache identities, build logs, reuse and cleanup.
