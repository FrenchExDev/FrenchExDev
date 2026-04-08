# COMPOSE-BUNDLE — Architecture

## Five-project layout

A compose-bundle for format `Xxx` always decomposes into five projects with
strict layering. Names follow the pattern `FrenchExDev.Net.Xxx.Bundle*`.

```
Xxx/
  src/
    FrenchExDev.Net.Xxx.Bundle/                # consumer library (net10.0)
      schemas/                                 # N upstream JSON schema files
      XxxBundleDescriptor.cs                   # [XxxBundle] marker class
      XxxSerializer.cs                         # YAML reader/writer
      obj/Generated/                           # generated .g.cs files
    FrenchExDev.Net.Xxx.Bundle.Attributes/     # marker attribute (multi-target)
      XxxBundleAttribute.cs
    FrenchExDev.Net.Xxx.Bundle.SourceGenerator/ # incremental generator (netstandard2.0)
      XxxBundleGenerator.cs                    # IIncrementalGenerator entry point
      SchemaReader.cs                          # JSON schema parser
      SchemaModels.cs                          # internal types
      SchemaVersionMerger.cs                   # N → 1 unification
      ModelClassEmitter.cs                     # records / partial classes
      BuilderHelper.cs                         # adapts to BuilderEmitModel
      NamingHelper.cs                          # PascalCase, type mapping
      VersionMetadataEmitter.cs                # SinceVersion / UntilVersion
    FrenchExDev.Net.Xxx.Bundle.Design/          # schema downloader CLI (net10.0)
      Program.cs
  test/
    FrenchExDev.Net.Xxx.Bundle.Tests/           # xUnit
```

The split is non-negotiable. The Attributes project must be standalone because
both the SG (netstandard2.0) and the consumer (net10.0) reference it. The SG
project must be netstandard2.0 because Roslyn analyzers run inside the Roslyn
host. The Design project is `net10.0` because it only runs at developer time.

## Dependency graph

```
Bundle.Attributes  (netstandard2.0 + net10.0)
       ▲
       │
Bundle.SourceGenerator  (netstandard2.0, Roslyn)
   ├── Builder.SourceGenerator.Lib   (shared builder emission)
   ├── Microsoft.CodeAnalysis.CSharp
   └── System.Text.Json
       ▲
       │
Bundle  (net10.0)
   ├── Bundle.Attributes
   ├── Bundle.SourceGenerator   (OutputItemType="Analyzer")
   ├── Builder.SourceGenerator.Lib  (OutputItemType="Analyzer")
   ├── Builder                  (AbstractBuilder<T>)
   ├── Result                   (Result<T>)
   ├── YamlDotNet
   └── JsonSchema.Net           (optional, runtime validation)
```

## Generator pipeline

1. **Schema discovery.** The consumer `.csproj` declares schemas as
   `<AdditionalFiles Include="schemas\xxx-spec-*.json" />`. The SG receives them
   via `AdditionalTextsProvider` and filters by filename prefix.

2. **Per-version parse (`SchemaReader`).** Each JSON schema becomes a
   `SchemaModel` carrying:
   - root properties
   - definitions (`#/definitions/*`)
   - per-property `PropertyModel` with full type info

   `$ref` is resolved to a `PropertyType.Ref` carrying the target definition
   name. Well-known refs (`list_or_dict`, `string_or_list`, `command`,
   `extra_hosts`, `ulimits`, etc.) are mapped to dedicated `PropertyType` enum
   values.

3. **`oneOf` resolution.** `ParseOneOf` matches union shapes in priority order
   and returns one `PropertyType`:

   ```
   [string, object{properties}]   → StringOrObject + inline class
   [string, integer]               → StringOrInteger
   [string, boolean]               → StringOrBoolean
   [string, array]                 → StringOrList
   [$ref(list_of_strings), object] → ListOrDict-like
   [null, $ref]                    → nullable ref
   fallback                        → String
   ```

4. **Multi-version merge (`SchemaVersionMerger`).** All `SchemaModel`s are
   sorted by semver and folded into a single `UnifiedSchema`. For each
   definition and each property:
   - take the **latest** version's definition (newest wins on conflict)
   - record `SinceVersion` = first version it appeared in
   - record `UntilVersion` = last version it appeared in
   - null out boundaries if it spans the oldest and newest

5. **Code emission.** Three emitters write `.g.cs` files via the SG context:

   | Emitter | Output |
   |---------|--------|
   | `VersionMetadataEmitter` | `XxxSchemaVersions` static class + `SinceVersion`/`UntilVersion` attribute definitions |
   | `ModelClassEmitter` | One `partial class` per definition + inline classes for nested objects |
   | `BuilderHelper` + `BuilderEmitter` (shared) | One builder per model |

6. **Error fallback.** The whole `Generate` method is wrapped in `try/catch`.
   On failure, a single `GenerateError.g.cs` is emitted containing the
   exception as a comment. The build does not fail silently.

## Type mapping

| JSON schema | C# |
|-------------|-----|
| `string` | `string?` |
| `integer` | `int?` |
| `number` | `double?` |
| `boolean` | `bool?` |
| `array<string>` | `List<string>?` |
| `array<$ref>` | `List<ClassName>?` |
| `array<inline object>` | `List<InlineClassName>?` |
| `object` (untyped) | `Dictionary<string, object?>?` |
| `$ref` to definition | `ClassName?` |
| `oneOf[string, integer]` | `int?` |
| `oneOf[string, object{...}]` | inline `ClassName?` |
| compose-style `list_or_dict` | `Dictionary<string, string?>?` |
| compose-style `string_or_list` | `List<string>?` |

Every model class also gets `Dictionary<string, object?>? Extensions` for
upstream schema's `x-*` extension fields.

## Naming

`NamingHelper` is the single place where any naming decision lives. Three
methods:

- `ToPascalCase(snake_case|kebab-case|dot.case)` → `PascalCase`
- `DefinitionToClassName("service")` → `ComposeService` (prefix per format)
- `MapCSharpType(PropertyModel)` → fully-qualified C# type string

Inline class names follow strict rules so they remain stable across schema
changes:

- Property-level inline object: `{Parent}{PropertyPascalCase}`
- Array item-level inline object: `{Parent}{PropertyPascalCase}Item`
- oneOf object branch: `{Parent}{PropertyPascalCase}Config`

## YAML serialization

`XxxSerializer` is hand-written, not generated. It uses `YamlDotNet` with:

- Naming convention dictated by the upstream format
- `OmitNull` to drop unset properties
- Custom converters for any type that does not round-trip naturally
  (versioned strings, list-or-dict, etc.)

A reader and a writer must both exist. The reader is essential for reading
existing files committed in users' repositories — without it, the bundle
becomes write-only and useless for editing flows.

## Contributor pattern

Every bundle exposes:

```csharp
public partial class XxxFile  // generated as partial
{
    public XxxFile Apply(params IXxxContributor[] contributors)
    {
        foreach (var c in contributors) c.Contribute(this);
        return this;
    }
}

public interface IXxxContributor
{
    void Contribute(XxxFile bundle);
}
```

Contributors are typically thin wrappers that mutate dictionaries (`Services`,
`Networks`, etc.) and add typed entries.

## Test layout

The Tests project covers four categories:

| Category | What it asserts |
|----------|-----------------|
| Schema loading | All N schemas parse without error |
| Versioning | `SinceVersion` / `UntilVersion` boundaries are correct |
| Builders | Every `With*()` round-trips through `BuildAsync()` |
| Serialization | YAML output validates against the upstream schema |
