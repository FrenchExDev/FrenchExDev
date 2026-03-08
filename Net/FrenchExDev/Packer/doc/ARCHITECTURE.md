# Packer Wrapper Architecture

## Three-Layer Pipeline

```
LAYER 1: Design-Time (BinaryWrapper.Design)
  ScrapePipeline + PackerHelpParser
  Produces: scrape/packer-{version}.json

LAYER 2: Compile-Time (BinaryWrapper.SourceGenerator)
  Reads JSON from AdditionalFiles
  Generates: Command + Builder + Client classes

LAYER 3: Runtime (BinaryWrapper + manual code)
  Events, Parsers, Collectors
```

## Generated Code (per command)

The source generator reads `scrape/packer-*.json` and produces:

- **`PackerBuildCommand.g.cs`** - Sealed ICliCommand with `ToArguments()` serialization
- **`PackerBuildCommandBuilder.g.cs`** - `AbstractBuilder<T>` with `With*()` fluent API + validation
- **`PackerClient.g.cs`** - Static entry point + typed client with nested `Plugins` group

## Manual Code

| File | Purpose |
|---|---|
| `PackerDescriptor.cs` | `[BinaryWrapper("packer")]` trigger for source generation |
| `PackerEvents.cs` | Event hierarchy (BuildStarted, BuildOutput, BuildError, etc.) |
| `PackerBuildParser.cs` | `IOutputParser<PackerEvent>` for standard output |
| `PackerMachineReadableParser.cs` | `IOutputParser<PackerEvent>` for `-machine-readable` format |
| `PackerBuildResult.cs` | `PackerBuildResult` + `PackerBuildCollector` aggregation |

## Packer CLI Conventions

- Single-dash flags: `-flag` (Go-style)
- Value format: `-flag=value`
- Boolean format: `-flag=true` / `-flag=false`
- Multiple values: repeated flags (`-var=k1=v1 -var=k2=v2`)
- Positional arguments appear last

These conventions are configured via `[BinaryWrapper("packer", FlagPrefix = "-", FlagValueSeparator = "=", UseBoolEqualsFormat = true)]`.

## Multi-Version Support

Place multiple JSON files in `scrape/`:
```
scrape/packer-1.9.0.json
scrape/packer-1.10.0.json
scrape/packer-1.11.2.json
```

The source generator computes version diffs and emits `[SinceVersion]`/`[UntilVersion]` attributes with runtime guards.
