# Architecture

## Project Decomposition

```
Traefik/
├── src/
│   ├── Traefik.Bundle.Attributes/       Marker attribute ([TraefikBundle])
│   ├── Traefik.Bundle.SourceGenerator/  Incremental SG (Roslyn analyzer)
│   ├── Traefik.Bundle/                  Consumer-facing: models, builders, serializer
│   └── Traefik.Bundle.Design/           Design-time schema downloader
└── test/
    └── Traefik.Bundle.Tests/            xUnit tests
```

### Dependency Flow

```
Attributes ──────────────────────────► Bundle (runtime)
SourceGenerator ─► Builder.SG.Lib ──► Bundle (analyzer, compile-time)
Design ─► Wrapper.Versioning ────────► schemas/ (design-time download)
```

- **Attributes** is referenced at compile time and runtime (marker for SG discovery)
- **SourceGenerator** runs as a Roslyn analyzer during build -- it reads the JSON schemas from `AdditionalFiles` and emits C# source
- **Design** is a standalone console app, run manually to refresh schemas from SchemaStore

## Source Generator Pipeline

`TraefikBundleGenerator.Initialize()` registers a pipeline that triggers on `AdditionalText` files matching `traefik-v3-*.json`.

### Step 1 -- Parse Schemas

Each schema file is processed by `TraefikSchemaReader.Parse()`:

1. Detect kind from filename (`static` vs `file-provider` = Dynamic)
2. Extract version string (e.g., `"3"` from `traefik-v3-static.json`)
3. Parse JSON via `System.Text.Json`
4. Extract definitions from `$defs` or `definitions`
5. Parse root properties (Static: flat properties; Dynamic: sectioned -- http, tcp, udp, tls)
6. Detect discriminated oneOf patterns in definitions

Output: `SchemaModel` per file containing `DefinitionModel` and `PropertyModel` trees.

### Step 2 -- Merge Into Unified Schema

Definitions and root properties are merged across schema files into a single `UnifiedSchema`. Properties get `SinceVersion`/`UntilVersion` annotations when they appear in only a subset of schema versions.

### Step 3 -- Emit Code

| Emitter | Output | Description |
|---|---|---|
| `VersionMetadataEmitter` | `TraefikSchemaVersions.g.cs` | Version list, `SinceVersion`/`UntilVersion` attributes |
| `TraefikModelClassEmitter` | `TraefikStaticConfig.g.cs`, `TraefikDynamicConfig.g.cs` | Root model classes |
| `TraefikModelClassEmitter` | `Traefik{Definition}.g.cs` | One class per schema definition |
| `TraefikBuilderHelper` + `BuilderEmitter` | `*Builder.g.cs` | Fluent builder per model class |
| Generator | `DebugInfo.g.cs` | Generation statistics |

## Schema Model Hierarchy

```
SchemaModel
├── Version: string
├── Kind: SchemaKind (Static | Dynamic)
├── Definitions: Dictionary<string, DefinitionModel>
│   ├── Name, Description
│   ├── Properties: List<PropertyModel>
│   ├── IsOneOfDiscriminated: bool
│   └── Branches: List<DiscriminatedBranch>
└── RootProperties: List<PropertyModel>
    ├── JsonName, CSharpName
    ├── Type: PropertyType (String | Integer | Boolean | Array | Ref | DictOfRef | ...)
    ├── InlineObjectProperties (nested objects)
    └── DictOfRefTarget (for Dictionary<string, T>)
```

## Generated Code Layout

All generated files land in the `obj/` tree under `Generated/`:

```
Generated/
├── TraefikSchemaVersions.g.cs           Version utilities + attributes
├── TraefikStaticConfig.g.cs             Root static config model
├── TraefikStaticConfigBuilder.g.cs      Builder for static config
├── TraefikDynamicConfig.g.cs            Root dynamic config model
├── TraefikDynamicConfigBuilder.g.cs     Builder for dynamic config
├── TraefikDynamicHttp.g.cs              Dynamic section: HTTP
├── TraefikDynamicTcp.g.cs               Dynamic section: TCP
├── TraefikDynamicUdp.g.cs               Dynamic section: UDP
├── TraefikDynamicTls.g.cs               Dynamic section: TLS
├── Traefik{Definition}.g.cs             ~100+ definition classes
├── Traefik{Definition}Builder.g.cs      ~100+ builder classes
└── DebugInfo.g.cs                       Generation statistics
```

## Two-Tier Configuration Model

Traefik separates configuration into two tiers:

| Tier | Root Class | Schema Source | Content |
|---|---|---|---|
| Static | `TraefikStaticConfig` | `traefik-v3-static.json` | Entry points, providers, API, logging, metrics, tracing |
| Dynamic | `TraefikDynamicConfig` | `traefik-v3-file-provider.json` | HTTP/TCP/UDP routers, services, middlewares, TLS config |

Static configuration is read once at startup. Dynamic configuration can be reloaded at runtime by Traefik's file provider.

## Discriminated Unions

Some schema types use `oneOf` with a single-property discriminant (e.g., middleware types). These are emitted as a flat class with one nullable property per branch:

```csharp
// Generated from oneOf in HTTP middleware definition
public partial class TraefikHttpMiddleware
{
    public TraefikAddPrefixMiddleware? AddPrefix { get; set; }
    public TraefikBasicAuthMiddleware? BasicAuth { get; set; }
    public TraefikChainMiddleware? Chain { get; set; }
    // ... one property per middleware type
}
```

Usage: set exactly one property to select the middleware type.

## Builder Integration

Builders are generated via the shared `BuilderEmitter` from `FrenchExDev.Net.Builder.SourceGenerator.Lib`. `TraefikBuilderHelper` bridges the schema model to `BuilderEmitModel`:

- Each property becomes a `With{Name}()` fluent method
- Collections get `With{SingularName}()` that accepts a builder action
- Dictionaries get `With{SingularName}(string key, Action<TBuilder> configure)`
- Discriminated unions get `With{Branch}()` methods

## Serialization

`TraefikSerializer` provides static methods for YAML round-trip:

- **Naming**: PascalCase C# properties serialize to camelCase YAML keys
- **Nulls**: omitted from output (`DefaultValuesHandling.OmitNull`)
- **Tolerance**: unknown YAML keys are ignored on deserialization (`IgnoreUnmatchedProperties`)
- **Library**: YamlDotNet with `CamelCaseNamingConvention`
