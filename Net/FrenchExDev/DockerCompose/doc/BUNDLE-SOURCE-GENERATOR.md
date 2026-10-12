# DockerCompose.Bundle - Source Generator Internals

## Overview

`ComposeBundleGenerator` is a Roslyn **incremental source generator** (`IIncrementalGenerator`) that reads compose-spec JSON schemas at compile time and emits strongly-typed C# model classes and fluent builders.

**Project**: `FrenchExDev.Net.DockerCompose.Bundle.SourceGenerator`
**Target**: `netstandard2.0` (required for Roslyn analyzers/generators)

## Generator Entry Point

```
ComposeBundleGenerator : IIncrementalGenerator
  Initialize(IncrementalGeneratorInitializationContext)
    -> filters AdditionalTexts matching "compose-spec-*.json"
    -> RegisterSourceOutput(files.Collect(), Generate)
```

The generator receives all schema files as a single `ImmutableArray<AdditionalText>` via `Collect()`, since merging requires seeing all versions at once.

## Internal Components

### SchemaModels.cs - Data Model

The internal representation has two layers:

**Per-version layer** (before merging):

| Class | Purpose |
|-------|---------|
| `SchemaModel` | One parsed schema: version string, root properties, definitions |
| `DefinitionModel` | A `definitions/*` entry: name, description, properties, nullable flag |
| `PropertyModel` | A single property with full type info |

**Unified layer** (after merging):

| Class | Purpose |
|-------|---------|
| `UnifiedSchema` | All versions merged: version list, unified definitions, unified root properties |
| `UnifiedDefinition` | Merged definition with `SinceVersion`/`UntilVersion` |
| `UnifiedProperty` | Merged property wrapping a `PropertyModel` with version bounds |

### PropertyType Enum

```
String, Integer, Number, Boolean,
StringOrBoolean, StringOrInteger,
Array, Object, InlineObject, Ref,
ListOrDict, StringOrList, StringOrObject,
Command, ServiceConfigOrSecret, ExtraHosts, Ulimits
```

The enum captures both primitive JSON schema types and compose-spec-specific union patterns.

### SchemaReader.cs - JSON Schema Parser

**Entry point**: `SchemaReader.Parse(string json, string version) -> SchemaModel`

Key parsing strategies:

#### `$ref` Resolution
References like `"$ref": "#/definitions/service"` are resolved to `PropertyType.Ref` with the definition name stored in `PropertyModel.Ref`. Well-known refs are mapped to specific types:

| Ref Name | PropertyType |
|----------|-------------|
| `list_or_dict` | `ListOrDict` |
| `string_or_list`, `list_of_strings`, `env_file`, `label_file`, `gpus` | `StringOrList` |
| `command` | `Command` |
| `service_config_or_secret` | `ServiceConfigOrSecret` |
| `extra_hosts` | `ExtraHosts` |
| `include`, `generic_resources`, `devices` | `Array` |
| Everything else | `Ref` (resolved to class name) |

#### `oneOf` Handling
The `ParseOneOf` method detects these patterns (in priority order):

1. **`[string, object{properties}]`** -> `StringOrObject` with inline class (`{Parent}{Prop}Config`)
2. **`[string, integer]`** -> `StringOrInteger`
3. **`[string, boolean]`** -> `StringOrBoolean`
4. **`[string, array]`** -> `StringOrList`
5. **`[$ref(list_of_strings), object{patternProperties}]`** -> conditional map detection
6. **`[null, $ref]`** -> nullable ref
7. **`[null, ...]`** (2 items) -> nullable form of non-null item
8. **Fallback** -> `String`

#### Inline Objects
When `type: "object"` has `properties`, a new class is generated:
- Property-level: `{ParentClass}{PropertyPascalCase}` (e.g., `ComposeServiceBuildConfig`)
- Array item-level: `{ParentClass}{PropertyPascalCase}Item` (e.g., `ComposeDevelopmentWatchItem`)
- oneOf object: `{ParentClass}{PropertyPascalCase}Config` or `...Condition`

### SchemaVersionMerger.cs - Multi-Version Unification

**Entry point**: `SchemaVersionMerger.Merge(List<SchemaModel>) -> UnifiedSchema`

Algorithm:
1. Sort schemas by version (semver comparison)
2. For each definition name across all versions:
   - Find first and last version containing this definition
   - Take the **latest** version's definition (newest property definitions win)
   - Set `SinceVersion` = first appearance (null if present in oldest schema)
   - Set `UntilVersion` = last appearance (null if present in newest schema)
3. Same logic for each property within each definition
4. Same logic for root properties

This produces a **superset** of all properties across all 32 versions, with version bounds for documentation and validation.

### NamingHelper.cs - Name Mapping

#### `ToPascalCase(string)`
Converts `snake_case`, `kebab-case`, and `dot.case` to `PascalCase`:
- `container_name` -> `ContainerName`
- `blkio-weight` -> `BlkioWeight`

#### `DefinitionToClassName(string)`
Maps compose-spec definition names to C# class names with a `Compose` prefix:

| Definition | Class Name |
|-----------|------------|
| `service` | `ComposeService` |
| `network` | `ComposeNetwork` |
| `volume` | `ComposeVolume` |
| `deployment` | `ComposeDeployment` |
| `healthcheck` | `ComposeHealthcheck` |
| `blkio_limit` | `ComposeBlkioLimit` |
| (any other) | `Compose` + PascalCase |

#### `MapCSharpType(PropertyModel)`
Maps `PropertyType` + context to fully-qualified C# type strings. See the type mapping table in [BUNDLE-ARCHITECUTURE.md](BUNDLE-ARCHITECUTURE.md#type-mapping).

### ModelClassEmitter.cs - Model Class Generation

Emits `partial class` definitions with:
- `/// <summary>` XML doc from schema `description`
- `[SinceVersion("x.y.z")]` / `[UntilVersion("x.y.z")]` attributes
- `[Obsolete("Deprecated by compose-spec.")]` for deprecated properties
- `Dictionary<string, object?>? Extensions` on every class

**Helper definitions** (mapped to built-in types, not emitted as classes):
`list_or_dict`, `string_or_list`, `list_of_strings`, `command`, `service_config_or_secret`, `extra_hosts`, `constraints`, `env_file`, `label_file`, `gpus`, `include`, `generic_resources`, `devices`

**Inline class collection**: `CollectInlineClasses` recursively walks properties to find and emit:
- Inline objects (`PropertyType.InlineObject`)
- oneOf string-or-object forms (`PropertyType.StringOrObject`)
- Array items with inline objects
- Array items with oneOf objects

### BuilderHelper.cs - Builder Model Creation

Converts `UnifiedProperty` lists into `BuilderEmitModel` (from `Builder.SourceGenerator.Lib`):
- Each property becomes a `BuilderPropertyModel` with C# type, nullable type, collection detection
- Version attributes are propagated to `WithMethodAttributes`
- An `Extensions` property is appended to every builder
- Collection detection: parses `List<T>` types to set `IsCollection = true` with extracted item type

### VersionMetadataEmitter.cs - Version Metadata

Emits a single file containing:

1. **`ComposeSchemaVersions`** static class:
   - `Available` (`IReadOnlyList<string>`) - all version strings
   - `Latest` / `Oldest` - boundary versions

2. **`SinceVersionAttribute`** - marks properties/methods/classes with introduction version
3. **`UntilVersionAttribute`** - marks properties/methods/classes with removal version

## Generated Output Summary

For 32 schema versions with ~40 definitions, the generator produces:

| Category | Files | Example |
|----------|-------|---------|
| Version metadata + attributes | 1 | `ComposeSchemaVersions.g.cs` |
| Root model | 1 | `ComposeFile.g.cs` |
| Root builder | 1 | `ComposeFileBuilder.g.cs` |
| Definition models | ~20 | `ComposeService.g.cs`, `ComposeNetwork.g.cs` |
| Definition builders | ~20 | `ComposeServiceBuilder.g.cs` |
| Inline object models | ~15 | `ComposeServiceBuildConfig.g.cs` |
| Inline object builders | ~15 | `ComposeServiceBuildConfigBuilder.g.cs` |
| Debug/marker | 2 | `DebugInfo.g.cs`, `ComposeBundleMarker.g.cs` |
| Error (if any) | 0-1 | `GenerateError.g.cs` |
| **Total** | **~80** | |

## Error Handling

The generator wraps the entire `Generate` method in a try/catch. If any exception occurs, it emits `GenerateError.g.cs` containing the exception details as a comment:

```csharp
// Generator error: JsonException: ...
// at SchemaReader.Parse(...) ...
```

This ensures the build doesn't fail silently and provides diagnostic information.

## Builder Emission via Shared Library

The generator delegates builder code emission to `BuilderEmitter.Emit(BuilderEmitModel)` from `FrenchExDev.Net.Builder.SourceGenerator.Lib`. This is the same library used by the BinaryWrapper source generator, ensuring consistent builder patterns across all FrenchExDev packages.

Each generated builder:
- Extends `AbstractBuilder<T>`
- Has `protected {Type} {Prop} { get; private set; }` input properties
- Has `public {BuilderType} With{Prop}({Type} value)` fluent setters
- Has `protected virtual IEnumerable<Exception>? Validate{Prop}({Type} value)` hooks
- Has `protected virtual IEnumerable<Exception>? Validate{Prop}Item({ItemType} item, int index)` for collections
- Overrides `ValidateAsync` to call all per-property validators
- Overrides `BuildException` and `Instantiate` / `CreateInstance`

## Extending the Generator

### Adding a New Property Type

1. Add a new `PropertyType` enum value in `SchemaModels.cs`
2. Handle it in `SchemaReader.ParseProperty` or `ParseOneOf`
3. Add C# type mapping in `NamingHelper.MapCSharpType`
4. If it's a collection, ensure `BuilderHelper.DetectCollection` handles it

### Supporting New Schema Patterns

The `ParseOneOf` method in `SchemaReader.cs` handles compose-spec's union type patterns. New patterns can be added by:
1. Detecting the pattern shape (combination of types, refs, properties)
2. Mapping to an appropriate `PropertyType`
3. Setting inline class names and properties if the pattern includes object forms
