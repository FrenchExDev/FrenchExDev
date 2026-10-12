# DockerCompose.Bundle - Architecture

## Overview

`FrenchExDev.Net.DockerCompose.Bundle` provides a strongly-typed, schema-driven C# object model for Docker Compose files. It reads the official [compose-spec](https://github.com/compose-spec/compose-go) JSON schemas (32 versions, from v1.0.9 to v2.10.1) and uses a **Roslyn incremental source generator** to emit model classes and fluent builders at compile time.

The key design goals are:

- **Schema fidelity** - every property, type, and constraint in the compose-spec is represented
- **Version awareness** - properties carry `[SinceVersion]` / `[UntilVersion]` metadata so consumers know when features were introduced or removed
- **Builder pattern** - every model gets a generated `AbstractBuilder<T>`-derived builder with fluent `With*()` methods, per-property validation hooks, and `BuildAsync()` returning `Result<T>`
- **Single-stage generation** - one source generator reads schemas and emits both models and builders (no intermediate codegen step)

## Project Layout

```
DockerCompose/
  src/
    FrenchExDev.Net.DockerCompose.Bundle/            # Consumer-facing library (net10.0)
      schemas/                                        # 32 compose-spec JSON schema files
        compose-spec-v1.0.9.json ... v2.10.1.json
      ComposeSchemaVersion.cs                         # Version record (Parse, Compare, operators)
      ComposeBundleDescriptor.cs                      # [ComposeBundle] marker class
      obj/Generated/                                  # 80 generated .g.cs files (models + builders)

    FrenchExDev.Net.DockerCompose.Bundle.Attributes/  # [ComposeBundle] attribute (netstandard2.0 + net10.0)
      ComposeBundleAttribute.cs

    FrenchExDev.Net.DockerCompose.Bundle.SourceGenerator/  # Roslyn incremental generator (netstandard2.0)
      ComposeBundleGenerator.cs                       # Generator entry point
      SchemaReader.cs                                 # JSON schema parser
      SchemaModels.cs                                 # Internal models (SchemaModel, PropertyModel, UnifiedSchema, ...)
      SchemaVersionMerger.cs                          # Merges 32 per-version schemas into a UnifiedSchema
      ModelClassEmitter.cs                            # Emits model partial classes
      BuilderHelper.cs                                # Creates BuilderEmitModel from schema properties
      NamingHelper.cs                                 # PascalCase, definition-to-class-name mapping, C# type mapping
      VersionMetadataEmitter.cs                       # Emits ComposeSchemaVersions + attribute definitions

    FrenchExDev.Net.DockerCompose.Bundle.Design/      # Schema downloader CLI (net10.0 console app)
      Program.cs                                      # Downloads compose-spec schemas from GitHub

  test/
    FrenchExDev.Net.DockerCompose.Bundle.Tests/       # xUnit tests
      BuilderTests.cs
      VersioningTests.cs
      SchemaLoadingTests.cs
      SerializerTests.cs
```

## Data Flow

```
                    +--------------------------+
                    |  compose-spec GitHub repo |
                    |  (compose-go/schema/)     |
                    +-----------+--------------+
                                |
                         dotnet run (Design CLI)
                         downloads JSON schemas
                                |
                                v
                    +------------------------+
                    |  schemas/*.json         |
                    |  (AdditionalFiles in    |
                    |   .csproj)              |
                    +----------+-------------+
                               |
                      ComposeBundleGenerator
                      (IIncrementalGenerator)
                               |
               +---------------+---------------+
               |               |               |
               v               v               v
        SchemaReader      SchemaReader     SchemaReader
        (per file)        (per file)       (per file)
               |               |               |
               +-------+-------+-------+-------+
                       |
                 SchemaVersionMerger
                 (32 SchemaModels -> 1 UnifiedSchema)
                       |
          +------------+-------------+
          |            |             |
          v            v             v
  VersionMetadata   ModelClass    BuilderHelper
  Emitter           Emitter       + BuilderEmitter
          |            |             |
          v            v             v
  ComposeSchema    ComposeFile    ComposeFile
  Versions.g.cs    .g.cs         Builder.g.cs
  SinceVersion     ComposeService ComposeServiceBuilder
  Attribute        .g.cs         .g.cs
  UntilVersion     ... (40 models) ... (40 builders)
  Attribute
```

## Source Generator Pipeline

### 1. Schema Ingestion

The `.csproj` declares schemas as `<AdditionalFiles>`:

```xml
<AdditionalFiles Include="schemas\compose-spec-*.json" />
```

`ComposeBundleGenerator.Initialize()` filters these by filename prefix `compose-spec-` and `.json` extension.

### 2. Schema Parsing (`SchemaReader`)

Each JSON schema is parsed into a `SchemaModel`:
- **Root properties** (`services`, `networks`, `volumes`, `secrets`, `configs`, `name`, `include`, `models`)
- **Definitions** (`service`, `network`, `volume`, `secret`, `config`, `deployment`, `healthcheck`, etc.)
- **Properties** with full type resolution: `$ref`, `oneOf`, inline objects, arrays, enums

The parser handles compose-spec-specific patterns:
- `oneOf[string, object]` -> `StringOrObject` with inline class generation
- `oneOf[string, integer]` -> `StringOrInteger` mapped to `int?`
- `oneOf[$ref(list_of_strings), object]` -> conditional map detection
- `oneOf[null, $ref]` -> nullable reference
- Inline objects (`type: "object"` with `properties`) -> dedicated class emission
- Array items with inline objects or oneOf

### 3. Version Merging (`SchemaVersionMerger`)

All 32 parsed schemas are merged into a single `UnifiedSchema`:
- **Definitions**: union of all definition names across versions; latest property definition wins
- **Properties**: union of all property names; each tracks `SinceVersion` (first appearance) and `UntilVersion` (last appearance)
- Versions sorted by semver; first/last version boundaries are nulled out (present in all versions)

### 4. Code Emission

Three emitters produce the final source:

| Emitter | Output | Count |
|---------|--------|-------|
| `VersionMetadataEmitter` | `ComposeSchemaVersions` static class, `SinceVersionAttribute`, `UntilVersionAttribute` | 1 file |
| `ModelClassEmitter` | `partial class` per definition + inline objects | ~40 files |
| `BuilderHelper` + `BuilderEmitter` (from `Builder.SourceGenerator.Lib`) | Builder per model class | ~40 files |

Total: **80 generated files** including `DebugInfo.g.cs` and `ComposeBundleMarker.g.cs`.

## Type Mapping

| JSON Schema Type | C# Type |
|-----------------|---------|
| `string` | `string?` |
| `integer` | `int?` |
| `number` | `double?` |
| `boolean` | `bool?` |
| `oneOf[string, boolean]` | `bool?` |
| `oneOf[string, integer]` | `int?` |
| `array` of `string` | `List<string>?` |
| `array` of `$ref` | `List<ClassName>?` |
| `array` of inline object | `List<InlineClassName>?` |
| `object` (untyped) | `Dictionary<string, object?>?` |
| `$ref` to definition | `ClassName?` |
| `list_or_dict` | `Dictionary<string, string?>?` |
| `string_or_list` / `command` | `List<string>?` |
| `oneOf[string, object{...}]` | Inline class (`ClassName?`) |
| `extra_hosts` / `ulimits` | `List<string>?` / `Dictionary<string, object?>?` |

## Builder Integration

Builders are emitted using the shared `BuilderEmitter` from `FrenchExDev.Net.Builder.SourceGenerator.Lib`. Each builder:

- Extends `AbstractBuilder<T>` (from `FrenchExDev.Net.Builder`)
- Has `With*()` fluent methods for every property
- Has `virtual Validate{Prop}(...)` methods for per-property validation
- Has `virtual Validate{Prop}Item(...)` for collection items
- Returns `Result<Reference<T>>` from `BuildAsync()`
- Version attributes (`[SinceVersion]`, `[UntilVersion]`) are propagated to `With*()` methods

## Dependency Graph

```
Bundle.Attributes  (netstandard2.0 + net10.0)
       |
       v
Bundle.SourceGenerator  (netstandard2.0, Roslyn component)
       |-- Builder.SourceGenerator.Lib  (shared builder emission)
       |-- Microsoft.CodeAnalysis.CSharp
       |-- System.Text.Json
       v
Bundle  (net10.0, consumer library)
       |-- Bundle.Attributes
       |-- Bundle.SourceGenerator  (OutputItemType="Analyzer")
       |-- Builder.SourceGenerator.Lib  (OutputItemType="Analyzer")
       |-- Builder  (AbstractBuilder<T>, ValidationResult)
       |-- Result  (Result<T>)
       |-- YamlDotNet
       |-- JsonSchema.Net
```

## Generated Class Hierarchy

```
ComposeFile
  |-- ComposeService (Dictionary<string, ComposeService>)
  |     |-- ComposeServiceBuildConfig
  |     |-- ComposeServiceBlkioConfig
  |     |-- ComposeServiceCredentialSpec
  |     |-- ComposeServiceDependsOnCondition
  |     |-- ComposeServiceDevicesConfig
  |     |-- ComposeServiceExtendsConfig
  |     |-- ComposeServiceLogging
  |     |-- ComposeServicePortsConfig
  |     |-- ComposeServiceVolumesConfig
  |     |     |-- ComposeServiceVolumesConfigBind
  |     |     |-- ComposeServiceVolumesConfigVolume
  |     |     |-- ComposeServiceVolumesConfigTmpfs
  |     |     |-- ComposeServiceVolumesConfigImage
  |     |-- ComposeServiceProvider
  |     |-- ComposeServiceHook
  |     |-- ComposeDeployment
  |     |     |-- ComposeDeploymentPlacement
  |     |     |     |-- ComposeDeploymentPlacementPreferencesItem
  |     |     |-- ComposeDeploymentResources
  |     |     |     |-- ComposeDeploymentResourcesLimits
  |     |     |     |-- ComposeDeploymentResourcesReservations
  |     |     |-- ComposeDeploymentRestartPolicy
  |     |     |-- ComposeDeploymentRollbackConfig
  |     |     |-- ComposeDeploymentUpdateConfig
  |     |-- ComposeHealthcheck
  |     |-- ComposeDevelopment
  |           |-- ComposeDevelopmentWatchItem
  |-- ComposeNetwork (Dictionary<string, ComposeNetwork?>)
  |     |-- ComposeNetworkIpam
  |           |-- ComposeNetworkIpamConfigItem
  |-- ComposeVolume (Dictionary<string, ComposeVolume?>)
  |-- ComposeSecret (Dictionary<string, ComposeSecret>)
  |-- ComposeConfig (Dictionary<string, ComposeConfig>)
  |-- ComposeModel (Dictionary<string, ComposeModel>) [since v2.7.1]
  |-- ComposeBlkioLimit
  |-- ComposeBlkioWeight
```

Every model class has a corresponding `{ClassName}Builder` class.

## Version Tracking

Properties and definitions that were introduced after v1.0.9 or removed before v2.10.1 are annotated:

```csharp
// Property introduced in v1.1.0
[SinceVersion("1.1.0")]
public string? Name { get; set; }

// Property introduced in v2.7.1
[SinceVersion("2.7.1")]
public Dictionary<string, ComposeModel>? Models { get; set; }

// Deprecated by compose-spec
[Obsolete("Deprecated by compose-spec.")]
public string? Version { get; set; }
```

`ComposeSchemaVersions` provides runtime access:
```csharp
ComposeSchemaVersions.Available  // IReadOnlyList<string>, all 32 versions
ComposeSchemaVersions.Latest     // "2.10.1"
ComposeSchemaVersions.Oldest     // "1.0.9"
```
