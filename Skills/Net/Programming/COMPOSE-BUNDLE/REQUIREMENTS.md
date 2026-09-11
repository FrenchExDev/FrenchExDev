# COMPOSE-BUNDLE — Requirements

A compose-bundle implementation MUST satisfy the following constraints. Each
item is testable.

## Project structure

- [ ] Five projects exist: `Bundle`, `Bundle.Attributes`, `Bundle.SourceGenerator`,
      `Bundle.Design`, `Bundle.Tests`. Names exactly follow
      `FrenchExDev.Net.{Format}.Bundle*`.
- [ ] `Bundle.Attributes` multi-targets `netstandard2.0;net10.0`.
- [ ] `Bundle.SourceGenerator` targets `netstandard2.0` only.
- [ ] `Bundle.Design` targets `net10.0`.
- [ ] `Bundle` (consumer) targets `net10.0`.
- [ ] No `Version=` attribute on any `<PackageReference>` (Central Package
      Management is enforced).

## Marker attribute

- [ ] `[XxxBundle]` attribute lives in `Bundle.Attributes`.
- [ ] A class annotated with `[XxxBundle]` exists in `Bundle/` (the descriptor).
      The SG triggers on its presence.

## Schema management

- [ ] All upstream schemas live in `Bundle/schemas/` with filename pattern
      `{format}-spec-{version}.json`.
- [ ] Schemas are declared as `<AdditionalFiles>` in `Bundle.csproj` with a
      glob pattern, never one by one.
- [ ] At least 5 schemas of distinct versions are present so the version
      merger has data to work with.
- [ ] `Bundle.Design/Program.cs` downloads schemas from upstream and writes
      them into `Bundle/schemas/`. It is invoked manually (`dotnet run --project
      Bundle.Design`), never from the build.

## Source generator behavior

- [ ] The generator is `IIncrementalGenerator`, not the deprecated `ISourceGenerator`.
- [ ] All schemas are collected via `.Collect()` before merging — merging
      requires seeing every version at once.
- [ ] `SchemaReader` handles all of: `string`, `integer`, `number`, `boolean`,
      `array`, `object`, `$ref`, `oneOf`, inline objects, enums.
- [ ] `oneOf` resolution covers at least: `[string, integer]`, `[string, boolean]`,
      `[string, array]`, `[string, object{props}]`, `[null, $ref]`.
- [ ] `SchemaVersionMerger` produces a unified schema where every definition
      and property carries `SinceVersion`/`UntilVersion` (null if it spans
      from oldest to newest).
- [ ] Three emitters exist: `VersionMetadataEmitter`, `ModelClassEmitter`, and
      a builder emission step delegating to `Builder.SourceGenerator.Lib`.
- [ ] All exceptions in the generator are caught and emitted into a
      `GenerateError.g.cs` file as a comment.

## Generated code shape

- [ ] Every definition becomes a `partial class` with `Dictionary<string, object?>?
      Extensions` for `x-*` extension fields.
- [ ] Every property carries `[SinceVersion]` if not present in the oldest
      schema, and `[UntilVersion]` if not present in the newest.
- [ ] Every model has a corresponding `{ClassName}Builder` extending
      `AbstractBuilder<T>` with `With*()` methods, `Validate{Prop}` hooks,
      and `Validate{Prop}Item` hooks for collections.
- [ ] Inline classes follow naming rules: `{Parent}{Prop}`, `{Parent}{Prop}Item`,
      `{Parent}{Prop}Config`.
- [ ] `XxxSchemaVersions` static class exposes `Available`, `Latest`, `Oldest`.
- [ ] `SinceVersionAttribute` and `UntilVersionAttribute` are emitted as part
      of the generated output (not hand-written in `Bundle/`).

## Builder integration

- [ ] Builders are emitted via `BuilderEmitter.Emit(BuilderEmitModel)` from
      `Builder.SourceGenerator.Lib`. No locally-rolled builder emitter.
- [ ] Version attributes propagate from properties to `With*()` methods via
      `BuilderPropertyModel.WithMethodAttributes`.
- [ ] `Result<Reference<T>>` is the return type of `BuildAsync()`. `Reference<T>`
      and `VisitedObjects` are not exposed to consumers.

## Serialization

- [ ] A `XxxSerializer` (or `XxxYamlReader` + `XxxYamlWriter`) exists in
      `Bundle/`. Hand-written, not generated.
- [ ] Naming convention matches the upstream format (snake_case, camelCase,
      kebab-case as appropriate).
- [ ] Nulls are omitted on serialization.
- [ ] A round-trip test exists: parse a real upstream YAML file, serialize it
      back, assert structural equality.

## Contributor pattern

- [ ] An `IXxxContributor` (or `IXxxFileContributor`) interface exists with a
      single method `void Contribute(XxxFile bundle)`.
- [ ] The root model has an `Apply(params IXxxContributor[])` method that
      iterates and returns `this` for chaining.

## Tests

- [ ] At least one test loads every schema and asserts no parse errors.
- [ ] At least one test asserts `SinceVersion`/`UntilVersion` boundaries on a
      known property.
- [ ] At least one test exercises a builder end-to-end (`With*` →
      `BuildAsync` → assert).
- [ ] At least one test serializes and deserializes a real upstream sample.
- [ ] All tests pass under `dotnet test Bundle.slnx`.

## Anti-requirements

- [ ] No hand-written model classes for anything the schema covers.
- [ ] No reflection-based or `dynamic`-based access to properties anywhere
      in `Bundle/`.
- [ ] No edits to files under `obj/Generated/` checked into source control.
- [ ] No HTML or Markdown scraping in `Bundle.Design`.
- [ ] No build-time network access in `Bundle.SourceGenerator`.
- [ ] No dependency from `Bundle.Attributes` on Roslyn.
- [ ] No dependency from `Bundle.SourceGenerator` on the consumer `Bundle`
      project (the dependency points the other way, with `OutputItemType="Analyzer"`).
