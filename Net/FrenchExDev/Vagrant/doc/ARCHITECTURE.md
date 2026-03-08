# Architecture

## Overview

The Vagrant wrapper is built on the BinaryWrapper framework -- a generic source-generator-driven system for wrapping CLI binaries. The framework handles the mechanical work (command classes, builders, client API, version gating) while the Vagrant project provides domain-specific parsers, events, and the scraping pipeline.

```
                      Design-time                          Runtime
                 ┌─────────────────────┐           ┌────────────────────┐
  vagrant -h ──> │  Design Tool        │           │  VagrantClient     │
  (in podman)    │  ┌─────────────┐    │           │  (generated)       │
                 │  │ HelpParser  │    │  JSON     │                    │
                 │  │ + Scraper   │────┼──────>    │  ┌──────────────┐  │
                 │  └─────────────┘    │  files    │  │ Command      │  │
                 └─────────────────────┘           │  │ Builder      │  │
                                                   │  │ Client       │  │
                 ┌─────────────────────┐           │  └──────┬───────┘  │
                 │  Source Generator   │           │         │          │
                 │  (BinaryWrapper SG) │           │  ┌──────▼───────┐  │
                 │  reads JSON ──> C#  │           │  │ Executor     │  │
                 └─────────────────────┘           │  │ Parser       │  │
                                                   │  │ Collector    │  │
                                                   │  └──────────────┘  │
                                                   └────────────────────┘
```

## Project structure

```
Vagrant/
├── FrenchExDev.Net.Vagrant.slnx
├── src/
│   ├── FrenchExDev.Net.Vagrant/           # Main library
│   │   ├── FrenchExDev.Net.Vagrant.csproj
│   │   ├── VagrantDescriptor.cs           # [BinaryWrapper("vagrant")] trigger
│   │   ├── VagrantEvents.cs               # Event record hierarchy
│   │   ├── VagrantOutputParser.cs         # Standard output parser
│   │   ├── VagrantMachineReadableParser.cs # CSV machine-readable parser
│   │   ├── VagrantUpResult.cs             # Result aggregator
│   │   └── scrape/
│   │       ├── vagrant-2.4.3.json
│   │       ├── ...
│   │       └── vagrant-2.4.9.json
│   └── FrenchExDev.Net.Vagrant.Design/    # Scraping tool
│       ├── Program.cs                     # CLI entry point (two-phase pipeline)
│       ├── VagrantHelpParser.cs           # Vagrant-specific help parser
│       └── VagrantVersionCollector.cs     # HashiCorp releases API client
└── test/
    └── FrenchExDev.Net.Vagrant.Tests/     # xUnit + CsCheck + Shouldly
```

## Source generator pipeline

The BinaryWrapper source generator (`FrenchExDev.Net.BinaryWrapper.SourceGenerator`) is an incremental Roslyn generator that runs at compile time:

### 1. Discovery

The generator finds classes annotated with `[BinaryWrapper("vagrant")]` -- in this case `VagrantDescriptor`. This triggers code generation.

### 2. JSON ingestion

The `.csproj` registers scrape files as `AdditionalFiles`:

```xml
<AdditionalFiles Include="scrape\vagrant-*.json" />
```

The generator reads all matching files and parses them into `CommandTreeModel` objects -- one per version.

### 3. Version differencing

When multiple JSON files exist (7 versions for Vagrant), `VersionDiffer` merges them into a single unified tree. Each command and option is annotated with the version range in which it exists:

- `[SinceVersion("2.4.3")]` -- available from this version onward
- `[UntilVersion("2.4.5")]` -- removed in this version

### 4. Code emission

Three emitters produce the generated C#:

**CommandClassEmitter** -- For each leaf command in the tree:

```csharp
[SinceVersion("2.4.3")]
public sealed partial class VagrantUpCommand : ICliCommand
{
    public bool? NoProvision { get; init; }
    public string? Provider { get; init; }
    // ...
    public IReadOnlyList<string> CommandPath => new[] { "up" };
    public IReadOnlyList<string> ToArguments() { /* serializes non-null props */ }
}
```

**BuilderClassEmitter** -- A fluent builder extending `AbstractBuilder<T>`:

```csharp
public partial class VagrantUpCommandBuilder : AbstractBuilder<VagrantUpCommand>
{
    protected bool? NoProvision { get; private set; }

    public VagrantUpCommandBuilder WithNoProvision(bool? value) { ... return this; }
    protected virtual IEnumerable<Exception>? ValidateNoProvision(bool? value) => null;

    protected override Task<Result<ValidationResult>> ValidateAsync(...) { /* calls all Validate*() */ }
    protected sealed override Task<...> Instantiate(...) { /* creates command from props */ }
}
```

**ClientClassEmitter** -- The public API surface:

```csharp
public static partial class Vagrant
{
    public static VagrantClient Create(BinaryBinding binding) => new(binding);
}

public partial class VagrantClient
{
    // Top-level commands
    public VagrantUpCommand Up(Action<VagrantUpCommandBuilder> configure) { ... }

    // Nested command groups
    public VagrantClientBoxGroup Box => new(this);

    public partial class VagrantClientBoxGroup
    {
        public VagrantBoxAddCommand Add(Action<VagrantBoxAddCommandBuilder> configure) { ... }
        public VagrantBoxListCommand List(Action<VagrantBoxListCommandBuilder> configure) { ... }
    }
}
```

## Runtime execution pipeline

```
VagrantClient.Up(configure)
    │
    ▼
VagrantUpCommandBuilder          Fluent configuration
    │ .WithProvider("vbox")
    │ .WithMachineReadable(true)
    ▼
ValidateAsync()                  Per-property validation
    │
    ▼
Instantiate()                    Creates VagrantUpCommand
    │
    ▼
CommandExecutor.ExecuteAsync()   Resolves binary, builds ProcessSpec
    │
    ▼
IProcessRunner.StreamAsync()    Spawns process, streams OutputLine
    │
    ▼
IOutputParser<VagrantEvent>     Converts lines to typed events
    │
    ▼
IResultCollector<VagrantEvent, VagrantUpResult>
    │                            Aggregates events into final result
    ▼
VagrantUpResult { MachinesReady, Errors, Success }
```

## Output parsing

### VagrantOutputParser

A regex-based stateful parser for standard Vagrant output. Pattern matching order matters -- error patterns are checked before general output to avoid false positives:

1. `==> machine (error): message` -> `VagrantMachineError`
2. `==> machine: Machine booted and ready!` -> `VagrantActionCompleted(Success: true)`
3. `==> machine: message` -> `VagrantMachineOutput`
4. `    machine: message` -> `VagrantProvisionerOutput`
5. Everything else -> `VagrantOutputLine`
6. Non-zero exit code -> `VagrantMachineError` in `Complete()`

### VagrantMachineReadableParser

Parses the CSV format produced by `--machine-readable`:

```
1234567890,default,ui,output,Starting machine...
```

Fields: `timestamp`, `target`, `type`, `data[0..N]`

Commas within data fields are escaped as `%!(VAGRANT_COMMA)` and unescaped during parsing. StdErr lines bypass CSV parsing entirely and become `VagrantOutputLine`.

### VagrantUpCollector

An `IResultCollector` that aggregates events into `VagrantUpResult`:

- Tracks `VagrantActionCompleted { Success: true }` -> adds machine to `MachinesReady`
- Tracks `VagrantActionCompleted { Success: false }` -> sets failure flag
- Tracks `VagrantMachineError` -> appends to `Errors`
- Final `Success` = `!hasFailure && errors.Count == 0`

## Event hierarchy

All events are C# `record` types for value equality:

```
VagrantEvent (abstract)
├── VagrantMachineOutput(MachineName, Message)
├── VagrantMachineError(MachineName, Message)
├── VagrantProvisionerOutput(MachineName, Message)
├── VagrantActionCompleted(MachineName, Success)
├── VagrantMachineReadableEvent(Timestamp, Target, EventType, Data[])
└── VagrantOutputLine(Text, Source)
```

## Naming conventions

The `NamingHelper` in the source generator handles CLI-to-C# name mapping:

| CLI Flag | C# Property |
|----------|-------------|
| `--no-provision` | `NoProvision` |
| `--machine-readable` | `MachineReadable` |
| `--[no-]color` | `NoColor` |
| `--provider VALUE` | `Provider` (string?) |
| `--debug` | `Debug` (bool?) |

Edge cases:
- Brackets `[]` are stripped: `--[no-]tty` -> `NoTty`
- Spaces are truncated: `--flag with space` -> `Flag`
- Duplicate PascalCase names are deduplicated (first wins)

## Version gating

Every generated command method includes a runtime version check:

```csharp
VersionGuard.EnsureCommandSupported(
    _detectedVersion,       // from BinaryBinding
    "up",                   // command path
    new SemanticVersion(2, 4, 3),  // SinceVersion
    null                    // UntilVersion (null = still present)
);
```

Throws `CommandNotSupportedException` or `OptionNotSupportedException` with a clear message if the detected binary version is outside the supported range.

## Dependencies

The Vagrant library depends on four sibling FrenchExDev.Net packages:

```
FrenchExDev.Net.Vagrant
├── FrenchExDev.Net.BinaryWrapper          Core: ICliCommand, CommandExecutor, IOutputParser
├── FrenchExDev.Net.BinaryWrapper.Attributes   [BinaryWrapper] attribute
├── FrenchExDev.Net.BinaryWrapper.SourceGenerator  (Analyzer, no runtime ref)
├── FrenchExDev.Net.Builder                AbstractBuilder<T>, fluent builders
└── FrenchExDev.Net.Result                 Result<T>, Result<T,TError>
```

The Design tool additionally depends on:
- `FrenchExDev.Net.BinaryWrapper.Design` -- scraping framework, `ScrapePipeline`, `MultiVersionScraper`
- `Microsoft.Extensions.Logging` / `.Console`

## Known quirks

### Vagrant 2.4.4--2.4.5 `server_mode?` bug

These versions crash when running `vagrant box -h`, `vagrant cloud -h`, or `vagrant plugin -h` due to a Ruby `server_mode?` method bug. The scraper handles this gracefully -- these sub-commands appear as empty leaves in those versions. Version differencing correctly marks their sub-commands with appropriate `[SinceVersion]` attributes.

### WSL platform detection

Vagrant's `platform.rb` sets `@_wsl = true` when it detects it's running under Windows Subsystem for Linux. Inside a podman container there is no actual WSL, causing crashes. The scraping pipeline patches this:

```bash
sed -i 's/@_wsl = true/@_wsl = false/' \
    /opt/vagrant/embedded/gems/gems/vagrant-*/lib/vagrant/util/platform.rb
```

### Skipped commands

Three commands are excluded from scraping to avoid hangs:

- `help` -- echoes root help (infinite recursion)
- `list-commands` -- also causes recursion
- `serve` -- starts a GRPC server and never returns
