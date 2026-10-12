# COMPOSE-BUNDLE — Philosophy

A "compose bundle" is a strongly-typed, schema-driven C# object model for an
external configuration format (Docker Compose, Traefik, GitLab CI, GitLab
Omnibus, etc.). Models and fluent builders are emitted at compile time by a
Roslyn incremental source generator that reads the **upstream JSON schemas as
the source of truth**. Hand-written model code is not allowed.

This skill captures the recurring pattern. It applies any time you want a typed
.NET surface for a YAML/JSON configuration file whose schema is published
upstream and evolves across versions.

## Schema is the source of truth

There is exactly one source of truth: the upstream JSON schemas. The Design
project downloads them. The source generator parses them. No human transcribes
field lists into C#. When upstream adds a property, you re-run the Design
project and rebuild — the new property appears on the model and on the builder
automatically with the correct `[SinceVersion]` attribute.

This rules out:

- Hand-written models that lag the upstream schema.
- Doc-scraping (HTML, Markdown) — too brittle, drifts under whitespace changes.
- Reflection-based runtime models — they erase compile-time safety.
- Adapter classes that wrap a `Dictionary<string, object?>` — no fluent API,
  no IDE help, no validation.

## Version awareness is non-negotiable

Every configuration format evolves. Docker Compose added `models`, `watch`,
`develop`, and `provider` over time. Traefik split static and dynamic config
across versions. GitLab CI changed `rules` semantics multiple times. The
generated code MUST tell consumers which versions a property was available in:

```csharp
[SinceVersion("2.7.1")]
public Dictionary<string, ComposeModel>? Models { get; set; }

[Obsolete("Deprecated by compose-spec.")]
public string? Version { get; set; }
```

The generator computes these by **merging N per-version schemas into one
unified schema** and tracking first/last appearance of every definition and
property. Versions where a property is present in the oldest and newest schema
get null bounds (always available).

## Single-stage generation

One generator reads schemas and emits both the model and its builder. There is
no intermediate codegen step, no `T4`, no `dotnet run` build step that writes
files into source control. The schemas are declared as `<AdditionalFiles>` in
the consumer `.csproj`; the generator runs as an analyzer; the build is
hermetic.

```xml
<AdditionalFiles Include="schemas\compose-spec-*.json" />
```

```
SchemaReader → SchemaVersionMerger → ModelClassEmitter
                                  → BuilderHelper → BuilderEmitter (shared)
                                  → VersionMetadataEmitter
```

## Builders are emitted via the shared BuilderEmitter

Every model class gets a corresponding `{ClassName}Builder` extending
`AbstractBuilder<T>`. The bundle generator does **not** reinvent builder
emission — it delegates to `FrenchExDev.Net.Builder.SourceGenerator.Lib.BuilderEmitter`.
This is the same library used by BinaryWrapper, Diem, and the other
schema-driven generators in the monorepo. Consistency is mandatory.

Each generated builder has:

- `protected {Type} {Prop} { get; private set; }` for every input
- `public {BuilderType} With{Prop}({Type} value)` fluent setters
- `protected virtual IEnumerable<Exception>? Validate{Prop}({Type} value)` hooks
- `protected virtual IEnumerable<Exception>? Validate{Prop}Item(...)` hooks
  for collections
- `Result<Reference<T>>` from `BuildAsync()`

`[SinceVersion]` / `[UntilVersion]` attributes are propagated from the property
to the corresponding `With*()` method.

## Union types are flattened

JSON schema `oneOf` is the hardest part. Compose-spec uses it heavily:
`[string, integer]` for ports, `[string, object]` for volumes,
`[$ref(list_of_strings), object]` for environment, `[null, $ref]` for nullable
references. The generator detects each shape in priority order and maps it to
the most useful C# type:

| oneOf shape | C# type |
|-------------|---------|
| `[string, boolean]` | `bool?` |
| `[string, integer]` | `int?` |
| `[string, array]` | `List<string>?` |
| `[string, object{props}]` | inline class |
| `[null, $ref]` | nullable ref |

When the union includes an inline object, the generator emits a dedicated class
`{Parent}{Prop}Config` so the builder can offer a typed nested API rather than
forcing the user to construct an `object`.

## YAML round-trips, but the model is the source

The bundle library ships a serializer (YamlDotNet) that round-trips models to
the upstream YAML format. This is for I/O at the edges only. **The C# model is
the canonical representation** — never reach for raw YAML strings inside
business code. If a property cannot be expressed in the typed model, the model
is wrong; fix the schema mapping in the source generator.

Nulls are omitted on serialization. Underscore vs camelCase naming is dictated
by the upstream format (compose uses snake_case, Traefik uses camelCase, GitLab
CI uses snake_case).

## Contributor pattern for composition

Configurations are rarely built in one shot. They are composed from many
sources: a base template, environment-specific overrides, integration
fragments, etc. The bundle exposes an `IXxxContributor` interface with a single
`Contribute(XxxFile bundle)` method. Bundles are mutable; contributors mutate
in place; the final bundle is serialized once.

```csharp
public interface IComposeFileContributor
{
    void Contribute(ComposeFile bundle);
}

var compose = new ComposeFile()
    .Apply(new TraefikContributor())
    .Apply(new GitLabContributor())
    .Apply(new PostgresContributor());
```

This is the same shape as `IPackerBundleContributor` and `IVosBundleContributor`
elsewhere in the monorepo. The pattern is intentional.

## What this is not

- Not a DSL. The point is fidelity to the upstream schema, not invention.
- Not opinionated about deployment. The bundle emits valid YAML; running it
  belongs to a separate binary wrapper (e.g. `DockerCompose` BinaryWrapper).
- Not a validation framework. Per-property `Validate*` hooks exist for
  domain-specific rules, but schema validity is enforced by the type system,
  not by runtime checks.
- Not extension-friendly at the model level. To add a property, update the
  upstream schema mapping. Never edit generated `.g.cs` files.
