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
