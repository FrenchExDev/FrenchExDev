# Traefik.Bundle — Improvements (P0–P3)

> Plan-mode note: per user feedback, the canonical home for this plan after approval is `Net/FrenchExDev/Traefik/doc/PLAN.md`. Plan mode only permits writing this single file, so the canonical copy will be created on the first implementation step.

## Context

`FrenchExDev.Net.Traefik.Bundle` generates strongly-typed Traefik static + dynamic config models, builders, and a YAML round-trip serializer from embedded JSON schemas. The pipeline works end-to-end (367 lines of tests, two minimal fixtures pass), but the user audit identified concrete gaps:

- **Correctness:** flat discriminated unions (e.g. `TraefikHttpMiddleware`) compile fine with two branches set — Traefik will reject that at runtime.
- **IDE perf:** the IR types in [SchemaModels.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/SchemaModels.cs) are reference-equal mutable classes, so the incremental generator pipeline cannot cache between keystrokes.
- **Dead deps & dead infra:** `JsonSchema.Net` is referenced but never invoked; `SinceVersion`/`UntilVersion` exist with only one schema version loaded.
- **Distribution:** no `PackageId`/`IsPackable` — currently unconsumable as a NuGet.
- **Test coverage gap:** runtime models are tested, but generator output is not snapshot-tested. A regression in an emitter is only caught indirectly.
- **Surface area:** serializer is sync-only, string-only, YAML-only — real consumers want file I/O and async.

User selected **all P0–P3**, **both** analyzer + builder validation, and asked which is best for JsonSchema.Net.

### Decision: JsonSchema.Net — use it, don't reimplement

Implementing JSON Schema (draft 7 / 2019-09 / 2020-12) ourselves would mean ~1.5k lines of validator code covering `$ref` resolution, `oneOf`/`anyOf`/`allOf` short-circuiting, format validators, pattern properties, conditional schemas — all already correct in `JsonSchema.Net`. Not worth duplicating. Wire it into the serializer and return validation errors as `Result<T>` failures.

---

## Critical files

### Source generator
- [SchemaModels.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/SchemaModels.cs) — IR types, needs value equality
- [TraefikBundleGenerator.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/TraefikBundleGenerator.cs) — pipeline entry, missing the "no AdditionalFiles" diagnostic
- [TraefikModelClassEmitter.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/TraefikModelClassEmitter.cs) — `EmitDiscriminatedClass` (line ~92) is the union shape that needs the runtime check
- [TraefikBuilderHelper.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/TraefikBuilderHelper.cs) — `CreateDiscriminatedBuilderModel` needs a custom `ValidateAsync` body via `BuilderEmitModel.Preamble`
- [VersionMetadataEmitter.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/VersionMetadataEmitter.cs) — fine; needs a real second version to exercise

### Runtime
- [TraefikSerializer.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle/TraefikSerializer.cs) — 30 lines; gets async, JSON, file I/O, and schema validation
- [FrenchExDev.Net.Traefik.Bundle.csproj](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle/FrenchExDev.Net.Traefik.Bundle.csproj) — packaging metadata
- [FrenchExDev.Net.Traefik.Bundle.SourceGenerator.csproj](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/FrenchExDev.Net.Traefik.Bundle.SourceGenerator.csproj) — packaging as analyzer

### Tests
- [Tests folder](Net/FrenchExDev/Traefik/test/FrenchExDev.Net.Traefik.Bundle.Tests/) — add Generator/, Analyzer/, Samples/ subfolders

### To create
- `Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle.Analyzer/` — new analyzer project
- `Net/FrenchExDev/Traefik/samples/` — realistic `traefik.yml` + round-trip program

---

## Reuse (do NOT reinvent)

- `BuilderEmitter` + `BuilderEmitModel.Preamble` from `FrenchExDev.Net.Builder.SourceGenerator.Lib` — already used by `TraefikBuilderHelper`. Use `Preamble` to inject the union check rather than emitting a parallel partial.
- `Result<T>` / `Result<T,TError>` from `FrenchExDev.Net.Result` — already a project ref. New `SerializeAsync` / `DeserializeAsync` return `Result<T, ValidationFailure>`, no exceptions on the happy path.
- `JsonSchema.Net` — already a package ref. Load embedded schemas via `Assembly.GetManifestResourceStream` and cache `JsonSchema` instances statically.

---

## Plan

### P0 — Correctness & perf (must ship)

**P0.1 — IR value equality (incremental cache fix)**
- File: [SchemaModels.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/SchemaModels.cs)
- Convert `SchemaModel`, `DefinitionModel`, `DiscriminatedBranch`, `PropertyModel`, `UnifiedSchema`, `UnifiedDefinition`, `UnifiedProperty` to `record` (or implement `IEquatable<T>` + structural `GetHashCode`).
- For `List<T>` members, wrap in `EquatableArray<T>` (small inline helper, ~30 lines, common pattern).
- Verify by adding a no-op edit to a consuming project and confirming `RegisterSourceOutput` doesn't re-fire (use a generation counter sentinel in `DebugInfo.g.cs`).

**P0.2 — "Exactly one branch" runtime check on builders**
- File: [TraefikBuilderHelper.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/TraefikBuilderHelper.cs), `CreateDiscriminatedBuilderModel`
- Set `BuilderEmitModel.Preamble` to inject an override of `ValidateAsync` that counts non-null branches and returns a validation failure if `count != 1`.
- Generated form (target):
  ```csharp
  protected override ValueTask<IEnumerable<ValidationResult>> ValidateAsync(...) {
      var set = new[] { AddPrefix is not null, BasicAuth is not null, /* ... */ }.Count(b => b);
      if (set != 1) return ValueTask.FromResult<IEnumerable<ValidationResult>>(
          new[] { new ValidationResult($"TraefikHttpMiddleware requires exactly one branch; got {set}.") });
      return base.ValidateAsync(...);
  }
  ```
- Tests: add to `BuilderTests.cs` — zero branches → fail; two branches → fail; one branch → pass.

### P1 — Distribution & guardrails

**P1.1 — Roslyn analyzer (compile-time union check)**
- New project: `FrenchExDev.Net.Traefik.Bundle.Analyzer/` (`netstandard2.0`, `IsRoslynComponent=true`)
- New attribute on the generated discriminated class: `[TraefikDiscriminatedUnion]` (emit it in `EmitDiscriminatedClass`).
- `DiagnosticAnalyzer` (`TFK001` "Multiple branches set on discriminated union"):
  - Walk `ObjectCreationExpressionSyntax` + `WithExpressionSyntax` + object-initializer assignments where the type carries `[TraefikDiscriminatedUnion]`.
  - Count non-null property assignments inside the initializer; if `> 1`, report on the second-and-later assignments.
- `TFK002` "Dangling router → service reference":
  - Walk dynamic config object graph; collect declared service keys; flag string assignments to `Router.Service` that don't match.
- `TFK003` "Use of deprecated property": driven off `[Obsolete]` attributes added by the emitter when `IsDeprecated == true` (also a small emitter change in `TraefikModelClassEmitter`).
- `TFK004` "[TraefikBundle] applied but no `<AdditionalFiles>` schemas found" — emitted from the **generator** (not analyzer) inside `TraefikBundleGenerator.Initialize` when `schemaFiles.Collect()` is empty AND a `[TraefikBundle]` symbol is present.
- Tests: new `Analyzer.Tests` project, `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit`, one test per diagnostic (positive + negative).

**P1.2 — Wire JsonSchema.Net into the serializer**
- File: [TraefikSerializer.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle/TraefikSerializer.cs)
- Static cache: `Lazy<JsonSchema> StaticSchema`, `Lazy<JsonSchema> DynamicSchema`, loaded from `EmbeddedResource` via `Assembly.GetManifestResourceStream("...traefik-v3-static.json")`.
- New API alongside the existing throwing methods:
  ```csharp
  Result<TraefikStaticConfig> TryDeserializeStatic(string yaml);
  Result<string> TrySerializeStatic(TraefikStaticConfig cfg);
  ```
- Validation flow: YAML → `JsonNode` (via YamlDotNet → `ISerializer` to JSON string → `JsonNode.Parse`) → `JsonSchema.Evaluate(node)` → on failure return `Result.Failure` with the evaluation result's errors mapped to `ValidationResult`. On success, deserialize the JSON node into the typed POCO.
- Keep the existing throwing API for back-compat; the new `Try*` methods are additive.

**P1.3 — NuGet packaging**
- [Bundle.csproj](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle/FrenchExDev.Net.Traefik.Bundle.csproj):
  ```xml
  <PackageId>FrenchExDev.Net.Traefik.Bundle</PackageId>
  <IsPackable>true</IsPackable>
  <Description>Strongly-typed Traefik v3 static + dynamic config builders, validator, and YAML/JSON round-trip serializer.</Description>
  <PackageTags>traefik;reverse-proxy;config;source-generator;yaml</PackageTags>
  ```
- Pack the analyzer + source generator as `analyzers/dotnet/cs/*.dll` so a single NuGet pull gives consumers everything (standard pattern: `<None Include="$(OutputPath)\*.dll" Pack="true" PackagePath="analyzers/dotnet/cs" />` with `IncludeBuildOutput=false`).
- Confirm by `dotnet pack` and inspecting the `.nupkg` contents.
- Push target: local registry at `C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__` (per project memory).

**P1.4 — Generator snapshot tests**
- New project: `FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Tests` with `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit`.
- Three snapshot tests (use `Verify.Xunit` or hand-rolled string compare against committed `.verified.cs`):
  1. Run `TraefikBundleGenerator` with both embedded schemas as `AdditionalText`. Snapshot the generated `TraefikHttpMiddleware.g.cs`, `TraefikStaticConfig.g.cs`, `TraefikSchemaVersions.g.cs`.
  2. Run with empty `AdditionalText` collection — assert `TFK004` diagnostic is reported.
  3. Run with both schemas — assert no warnings, ≥ N generated files (sanity gate).

### P2 — Surface area & realism

**P2.1 — Async + file I/O serializer surface**
- Add to [TraefikSerializer.cs](Net/FrenchExDev/Traefik/src/FrenchExDev.Net.Traefik.Bundle/TraefikSerializer.cs):
  ```csharp
  Task<Result<T>> DeserializeAsync<T>(Stream stream, CancellationToken ct);
  Task<Result<T>> ReadFromFileAsync<T>(string path, CancellationToken ct);
  Task<Result> WriteToFileAsync<T>(string path, T config, CancellationToken ct); // atomic: write to {path}.tmp, fsync, rename
  ```
- Atomic rename helper: `File.Replace` if target exists, else `File.Move`. Wrap in retry-on-`IOException` (3x, 50ms backoff) for the Windows/file-watcher race that bites Traefik file-provider users.
- Tests: file round-trip on a temp file, concurrent-write smoke (two tasks → no torn file).

**P2.2 — Samples project + realistic round-trip**
- New folder: `Net/FrenchExDev/Traefik/samples/` containing `realistic-dynamic.yaml` (HTTP routers, multiple middlewares including BasicAuth + StripPrefix on different middlewares, TLS options, file-provider style).
- Source: copy from upstream Traefik docs examples (attribution in a `README.md` next to the file).
- New test class `RealisticRoundTripTests` in the existing test project: deserialize → re-serialize → re-deserialize → assert structural equality. Expected to flush out edge cases (extra fields, unusual casing, integer-vs-string ports).

### P3 — Forward-looking

**P3.1 — JSON output path**
- Add `SerializeJson<T>(T)` / `DeserializeJson<T>(string)` overloads using `System.Text.Json`. Models are POCOs with camelCase via `JsonNamingPolicy.CamelCase`. Roughly 20 lines.
- Test: assert YAML → object → JSON → object → YAML round-trips equal.

**P3.2 — Exercise multi-version**
- Add a synthetic `traefik-v3.1-file-provider.json` with one extra property (e.g. a hypothetical `responseHeaderTimeout` on a service) — the cheapest way to prove the version-merging code path works.
- Confirm `UnifiedSchema` produces a `SinceVersion = "3.1"` on the new property and the emitter writes the attribute on the C# property.
- If exercising real v2 / v3.x schemas is desired later, swap the synthetic for the real ones — but that's out of scope for this iteration.

---

## Verification

Run from `Net/FrenchExDev/Traefik/`:

1. **Build**: `dotnet build Traefik.slnx -c Release` — must be warning-free.
2. **Tests**: `dotnet test Traefik.slnx` — expect existing tests to keep passing plus the new ones (target ≥ 30 new tests across builder validation, analyzer, generator snapshots, async serializer, realistic round-trip).
3. **Pack**: `dotnet pack src/FrenchExDev.Net.Traefik.Bundle/FrenchExDev.Net.Traefik.Bundle.csproj -c Release -o ./artifacts` — inspect `.nupkg` for `analyzers/dotnet/cs/*.dll`.
4. **Smoke (consumer)**: create a throwaway console project, reference the local `.nupkg`, build a minimal `TraefikDynamicConfig` with two-branch middleware → confirm `TFK001` analyzer warning at compile time AND `Result.Failure` from `TryBuild`.
5. **Generator perf sentinel**: tweak an unrelated source file in the consumer project; observe (via `DebugInfo.g.cs` counter or MSBuild binlog) that `TraefikBundleGenerator` does not re-emit.
6. **Quality gate**: `dotnet run --project ../QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test` from the Traefik subtree if/when wired in.

## Out of scope

- Real second Traefik schema version (v2 or v3.x delta) — not yet published in a usable form.
- Hot-reload / file-watcher for dynamic config — that's a consumer concern.
- Provider-specific helpers (KV, Consul, ECS, etc.) — only File provider is in scope.
