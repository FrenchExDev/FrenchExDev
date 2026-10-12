# Architecture

## Project decomposition

```
Traefik/
├── src/
│   ├── FrenchExDev.Net.Traefik.Bundle.Attributes/      Marker attributes
│   ├── FrenchExDev.Net.Traefik.Bundle.SourceGenerator/ Roslyn SG + analyzer
│   ├── FrenchExDev.Net.Traefik.Bundle/                 Models + builders + serializer
│   └── FrenchExDev.Net.Traefik.Bundle.Design/          Schema downloader CLI
├── samples/                                             Realistic Traefik configs
└── test/
    ├── FrenchExDev.Net.Traefik.Bundle.Tests/           Runtime tests
    └── FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Tests/ SG + analyzer tests
```

### Dependency flow

```
Attributes ─────────────────────────────► Bundle (runtime ref)
                                       └► SourceGenerator (compile-time ref to mark generated unions)

Builder.SourceGenerator.Lib ─────────────► SourceGenerator (BuilderEmitter, BuilderEmitModel)
                                        └► packed inside SourceGenerator nupkg

SourceGenerator ─► Bundle (analyzer, OutputItemType="Analyzer", ReferenceOutputAssembly=false)

Result + Builder + YamlDotNet + JsonSchema.Net ─► Bundle (runtime deps)

Design ─► Wrapper.Versioning ─► schemas/ (manual download)
```

## Source generator pipeline

`TraefikBundleGenerator.Initialize()` is an `IIncrementalGenerator` with three cacheable stages:

```
AdditionalTextsProvider
   │  filter: traefik-v*.json
   ▼
Select(static (file, ct) => TraefikSchemaReader.Parse(file))   ── Stage 1: PARSE
   │      (returns SchemaModel — value-equal IR)
   ▼
Collect()
   ▼
Select(static (schemas, ct) => Merge(schemas))                  ── Stage 2: MERGE
   │      (returns UnifiedSchema — value-equal IR; union merge
   │       across versions stamps SinceVersion on new properties)
   ▼
RegisterSourceOutput((ctx, unified) => Emit(ctx, unified))      ── Stage 3: EMIT
```

Each stage's input and output is structurally equal via `IEquatable<T>` on the IR types ([SchemaModels.cs](../src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/SchemaModels.cs)). When a `.cs` edit elsewhere in the consumer fires the generator, Roslyn skips Stage 2 and Stage 3 entirely as long as the parsed schema graph is unchanged. The IR equality helpers live in [IrEquality.cs](../src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/IrEquality.cs).

### Stage 1: parse

`TraefikSchemaReader.Parse(json, version, kind)` walks the JSON schema with `System.Text.Json` and produces a `SchemaModel`:

| Field | Source |
|---|---|
| `Version` | extracted from filename (`traefik-v3.1-...` → `3.1`) |
| `Kind` | filename contains `file-provider` → `Dynamic`, else `Static` |
| `Definitions` | walked from `$defs` or `definitions` |
| `RootProperties` | walked from `properties` (Static: flat; Dynamic: sectioned by http/tcp/udp/tls) |

`oneOf` blocks where every branch has exactly one `$ref` property are detected as discriminated unions and stored as `DiscriminatedBranch[]`.

### Stage 2: merge

Schemas are sorted by version (`StringComparer.Ordinal`) and merged into a single `UnifiedSchema`:

- **Definitions**: union merge across versions. Existing properties are preserved, new ones are appended.
- **SinceVersion stamping**: `firstSeen[(definition, property)] = version` is recorded on first appearance. If the first-seen version is *not* the earliest loaded schema, the property is stamped with `SinceVersion = firstSeen`.
- **Root properties**: static and dynamic root sections are concatenated; the emit stage re-splits them by inspecting the `InlineClassName` prefix (`TraefikDynamic*`).

### Stage 3: emit

| Emitter | Output | Description |
|---|---|---|
| `VersionMetadataEmitter` | `TraefikSchemaVersions.g.cs` | `Available`/`Latest`/`Oldest`, `SinceVersionAttribute`, `UntilVersionAttribute` |
| `TraefikModelClassEmitter.EmitRootClass` | `TraefikStaticConfig.g.cs`, `TraefikDynamicConfig.g.cs` | Root model classes |
| `TraefikModelClassEmitter.EmitDefinitions` | `Traefik{Definition}.g.cs` | One class per definition (recursive for inline objects) |
| `TraefikModelClassEmitter.EmitDiscriminatedClass` | (same path) | Stamps `[TraefikDiscriminatedUnion]` so the analyzer can find it |
| `TraefikBuilderHelper` + `BuilderEmitter` | `*Builder.g.cs` | Fluent builder per class; discriminated builders carry an `ValidateAsyncEpilogue` |
| Generator | `DebugInfo.g.cs` | Generation statistics |

When the generator runs with **zero** parsed schemas, it reports diagnostic **TFK004** instead of emitting any source.

## IR type hierarchy

All IR types are mutable classes (the parser uses property setters) with hand-rolled `IEquatable<T>` so the incremental pipeline can cache them. The helpers in `IrEquality.cs` provide structural `ListEqual`/`DictEqual`/`Combine`.

```
SchemaModel
├── Version : string
├── Kind : SchemaKind { Static, Dynamic }
├── Definitions : Dictionary<string, DefinitionModel>
└── RootProperties : List<PropertyModel>

DefinitionModel
├── Name, Description
├── Properties : List<PropertyModel>
├── IsOneOfDiscriminated : bool
└── Branches : List<DiscriminatedBranch>?

PropertyModel
├── JsonName, CSharpName, Description, IsDeprecated, IsRequired, IsNullable
├── Type : PropertyType { String, Integer, Number, Boolean, Array,
│                        Object, InlineObject, Ref, DictOfRef,
│                        DictOfObject, PatternPropsInline }
├── Ref, Items, EnumValues
├── InlineClassName, InlineObjectProperties (for inline objects)
├── DictOfRefTarget (for Dictionary<string, T>)
└── PatternPropsInlineClassName, PatternPropsInlineProperties

UnifiedSchema / UnifiedDefinition / UnifiedProperty
└── Adds SinceVersion / UntilVersion to the merged view
```

## Discriminated unions

Schema types like `httpMiddleware` use `oneOf` with single-property discriminants. They're emitted as a flat class:

```csharp
[global::FrenchExDev.Net.Traefik.Bundle.Attributes.TraefikDiscriminatedUnion]
public partial class TraefikHttpMiddleware
{
    public TraefikAddPrefixMiddleware? AddPrefix { get; set; }
    public TraefikBasicAuthMiddleware? BasicAuth { get; set; }
    public TraefikChainMiddleware? Chain { get; set; }
    // ... one nullable property per branch
}
```

The `[TraefikDiscriminatedUnion]` marker is what `DiscriminatedUnionAnalyzer` looks for.

**Three layers of enforcement:**

1. **Compile time** — `DiscriminatedUnionAnalyzer` (TFK001) walks `ObjectCreationExpressionSyntax` and `ImplicitObjectCreationExpressionSyntax`, counts non-null branch assignments in the initializer, and reports the second-and-later assignments.
2. **Build time (runtime)** — `TraefikBuilderHelper.CreateDiscriminatedBuilderModel` injects a `ValidateAsyncEpilogue` into the generated builder. The epilogue counts non-null branches and adds a `ValidationResult` if `count != 1`. The runtime check catches dynamic construction patterns the analyzer can't see.
3. **Schema time** — `additionalProperties: false` in the schema (combined with the strict YAML validation flow) catches invalid combinations the user reads from disk.

## Builder integration

`TraefikBuilderHelper` translates IR `PropertyModel`s to `BuilderPropertyModel`s and hands them to the shared `BuilderEmitter` from `Builder.SourceGenerator.Lib`. Two extension points are exercised:

- `BuilderEmitModel.ValidateAsyncEpilogue` — raw C# inserted at the end of the generated `ValidateAsync`. Discriminated builders use this to enforce the exactly-one-branch rule.
- `BuilderPropertyModel.WithMethodAttributes` — used to stamp `[SinceVersion("...")]` and `[UntilVersion("...")]` on `With*` methods for properties tracked across schema versions.

## Serialization & schema validation

[TraefikSerializer](../src/FrenchExDev.Net.Traefik.Bundle/TraefikSerializer.cs) is a static facade with three layers of API:

```
                                ┌─ Deserialize<T>(yaml)        Existing throwing API
                                ├─ Serialize<T>(obj)            (back-compat)
                                │
TraefikSerializer ──────────────┼─ TryDeserializeStatic(yaml) : Result<T>
                                ├─ TrySerializeStatic(cfg)   : Result<string>
                                │     YAML → JsonNode → schema → typed POCO
                                │
                                ├─ ReadStaticFromFileAsync(path)  : Task<Result<T>>
                                ├─ WriteStaticToFileAsync(path,…) : Task<Result>
                                │     Atomic via .tmp + File.Replace,
                                │     retried 3× on IOException
                                │
                                └─ SerializeJson<T>/DeserializeJson<T>  System.Text.Json
```

### Strict YAML validation flow

The `Try*` methods do **not** trust YamlDotNet's typed deserializer for shape validation. Instead they go through [YamlToJson.cs](../src/FrenchExDev.Net.Traefik.Bundle/YamlToJson.cs):

```
yaml string
   │
   ▼
YamlStream  (YamlDotNet representation model)
   │
   ▼
YamlToJson.Convert  ── per YAML 1.2 core schema:
   │                    plain "true"/"false" → bool
   │                    plain "42"/"-1"/"0xFF"/"0o77" → long
   │                    plain "3.14"/".inf" → double
   │                    quoted scalar / unrecognized → string
   ▼
JsonNode (preserves the ORIGINAL YAML shape, including unknown keys)
   │
   ▼
schema.Evaluate(node)   ── catches typo'd keys (additionalProperties: false)
   │                       AND wrong types AND missing required
   ▼
typed POCO  (only deserialized after the schema is happy)
```

This is the only way to make `dashbaord: true` (a real-world typo) fail loudly: the typed deserializer would silently drop it.

## Generated code layout

All generated files land in the consumer's `obj/Generated/` (the `EmitCompilerGeneratedFiles=true` switch in `Bundle.csproj` materializes them on disk for inspection):

```
Generated/
├── TraefikSchemaVersions.g.cs           Versions class + SinceVersion/UntilVersion attrs
├── TraefikStaticConfig.g.cs             Root static model
├── TraefikStaticConfigBuilder.g.cs      Builder for static
├── TraefikDynamicConfig.g.cs            Root dynamic model
├── TraefikDynamicConfigBuilder.g.cs     Builder for dynamic
├── TraefikDynamicHttp.g.cs              Section: HTTP
├── TraefikDynamicTcp.g.cs               Section: TCP
├── TraefikDynamicUdp.g.cs               Section: UDP
├── TraefikDynamicTls.g.cs               Section: TLS
├── Traefik{Definition}.g.cs             ~100 definition classes
├── Traefik{Definition}Builder.g.cs      ~100 builder classes
└── DebugInfo.g.cs                       Stats
```

## Packaging

All three projects publish as separate NuGet packages:

| Package | Contents | Notes |
|---|---|---|
| `FrenchExDev.Net.Traefik.Bundle` | Runtime DLL + embedded schemas + transitive dep on Attributes | What consumers reference |
| `FrenchExDev.Net.Traefik.Bundle.Attributes` | `[TraefikBundle]`, `[TraefikDiscriminatedUnion]` | Pulled in by Bundle |
| `FrenchExDev.Net.Traefik.Bundle.SourceGenerator` | SG + Builder.SG.Lib DLLs under `analyzers/dotnet/cs/` | `DevelopmentDependency=true`; ships analyzer payload only |

The SG package uses `IncludeBuildOutput=false` and adds the analyzer DLLs via explicit `<None Pack="true" PackagePath="analyzers/dotnet/cs">` items. Verified by inspecting the `.nupkg` after `dotnet pack`.
