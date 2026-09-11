# Philosophy

## Core principles

### Schema is the single source of truth

All models, builders, and version metadata are generated from Traefik's official JSON schemas (SchemaStore). No hand-written model class exists in this package. The C# API surface matches the real Traefik configuration spec by construction, and bumping a schema version regenerates everything.

### Two separate root types

Traefik fundamentally separates static (startup) from dynamic (runtime) configuration. Rather than merging them into one object, the library preserves this distinction with `TraefikStaticConfig` and `TraefikDynamicConfig`. This mirrors how Traefik actually loads configuration and prevents mixing concerns that don't belong together.

### Validate at the boundary, trust the inside

User-supplied YAML or JSON is validated against the embedded schema **before** it becomes a typed POCO. Once it's a POCO, no further runtime checking is needed at the property-getter level — the typed shape is the contract. The validation cost is paid once at the I/O edge.

The throwing API (`Deserialize<T>`) skips validation for back-compat with code that already trusts its input. The `Try*` API is what new code should use.

### Three layers of correctness for discriminated unions

Traefik middleware/service types are flat unions with one nullable property per branch. Exactly one branch must be set per instance. The library enforces this in three places, deliberately:

| Layer | Mechanism | Catches |
|---|---|---|
| Compile time | `DiscriminatedUnionAnalyzer` (TFK001) | Object initializers with two branches in the same expression |
| Build time | Generated `ValidateAsync` epilogue | Dynamic builder construction (loops, conditionals, late-bound assignment) |
| Schema time | `additionalProperties: false` + strict YAML validation | Configs read from disk |

Each layer catches what the others can't. Removing any one of them leaves a real-world failure mode uncovered.

## Design decisions

### Discriminated unions as flat classes, not inheritance

Discriminated unions could be modelled as `abstract TraefikHttpMiddleware` with concrete subtypes. The library uses flat classes with nullable properties instead. The wins:

- **Serialization** maps trivially to YAML — no custom converters, no `$type` annotations, no inheritance hierarchy in the wire format.
- **Discoverability** — every available branch is visible via IntelliSense on a single class.
- **Generator simplicity** — no virtual dispatch, no abstract base classes, no visitor pattern.

The cost is that the type system alone doesn't enforce exactly-one-branch. The three-layer enforcement above closes that gap.

### Strict YAML validation via YamlToJson, not the typed deserializer

The naive validation flow — *deserialize to POCO, re-serialize to JSON, validate* — silently drops typo'd YAML keys, because the typed YamlDotNet deserializer uses `IgnoreUnmatchedProperties()`. Unknown keys never reach the schema.

Instead, [YamlToJson.cs](../src/FrenchExDev.Net.Traefik.Bundle/YamlToJson.cs) walks the YamlDotNet `YamlStream` representation model directly and produces a `JsonNode` honouring **YAML 1.2 core schema scalar resolution** (true/false → bool, integers, hex/octal, floats including `.inf`/`.nan`, null forms, explicit `!!str`/`!!bool` tags, quoted vs plain scalars). This preserves the original YAML structure including unknown keys, which the schema then catches via `additionalProperties: false`.

The trade-off: a small custom YAML→JSON converter that has to track YAML 1.2 spec rules. The win: a real-world `dashbaord: true` typo fails loudly instead of silently producing a config with `dashboard = false`.

### Atomic file writes via .tmp + File.Replace

Traefik's file provider watches its dynamic config file. A torn write — half the bytes of the new content overlapping half of the old — crashes Traefik. `WriteDynamicToFileAsync` writes the entire content to a sibling `.tmp` first, fsync via `WriteAllTextAsync`'s `Dispose`, then atomically renames via `File.Replace` (or `File.Move` if no destination exists). The replace step is retried up to 3× on `IOException` for the Windows file-watcher race that hits even atomic renames.

Schema validation happens *before* the temp write, so an invalid config never produces any file at all.

### Reuse Builder.SourceGenerator.Lib, don't fork

`TraefikBuilderHelper` translates schema models into `BuilderEmitModel` and delegates to the shared `BuilderEmitter`. The same emission pipeline produces builders for `DockerCompose.Bundle` and other bundles in the monorepo.

The discriminated-union runtime check is wired through a new `BuilderEmitModel.ValidateAsyncEpilogue` extension point — adding the feature to one shared library, not duplicating it. Other bundles can opt in by setting the same field.

### Incremental generator caching via structural IR equality

The IR types (`SchemaModel`, `PropertyModel`, etc.) are mutable classes (the parser uses property setters), but every type implements `IEquatable<T>` with structural `Equals`/`GetHashCode` via the `IrEquality` helper. The pipeline is split into `Select(parse) → Collect → Select(merge) → RegisterSourceOutput(emit)` so that an unrelated `.cs` edit in the consumer skips both merge and emit when the parsed schema graph hasn't changed.

This is the difference between the IDE re-running emission on every keystroke vs. only when the user actually edits a schema file.

### Generator + analyzer in one assembly

The Roslyn source generator and the `DiscriminatedUnionAnalyzer` ship in the same project ([Traefik.Bundle.SourceGenerator](../src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/)) and the same NuGet package. They share the same target framework (`netstandard2.0`), the same Roslyn version pin (5.3.0 via central package management), and the same release manifest. One ship vehicle, one DevelopmentDependency reference for the consumer.

### Embedded schemas

The JSON schemas live in [src/FrenchExDev.Net.Traefik.Bundle/schemas/](../src/FrenchExDev.Net.Traefik.Bundle/schemas/) as both `<AdditionalFiles>` (for the source generator at compile time) and `<EmbeddedResource>` (for `JsonSchema.Net` validation at runtime). The package is self-contained — consumers don't need to distribute schema files separately, and schema validation works without any network or filesystem dependency.

## Trade-offs

| Decision | Benefit | Cost |
|---|---|---|
| Schema-driven generation | Always in sync with the Traefik spec | Longer build, more generator complexity |
| Flat discriminated unions | Simple serialization, good IntelliSense | No type-system enforcement; needs analyzer + runtime + schema layers |
| Custom YAML→JSON converter | Catches typo'd keys, real types preserved | Owns YAML 1.2 scalar resolution code |
| Atomic file writes | Safe under Traefik's file provider watch | Extra `.tmp` write on every save |
| Incremental IR equality | IDE stays responsive on consumer-side edits | Hand-maintained `Equals`/`GetHashCode` (mutable classes) |
| Generator + analyzer in one DLL | Single ship vehicle, shared infra | All-or-nothing — can't disable one without the other |
| Three packages (Bundle, Attributes, SG) | Conventional layout, clean dep graph | Three nupkg pushes per release |
| `JsonSchema.Net` for validation | Standards-compliant, ~1.5k LOC saved | Extra runtime dep, double-conversion on validation path |
