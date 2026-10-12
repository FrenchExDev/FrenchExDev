# How-To

## Build a static configuration

```csharp
var result = await new TraefikStaticConfigBuilder()
    .WithEntryPoint("web", ep => ep.WithAddress(":80"))
    .WithEntryPoint("websecure", ep => ep.WithAddress(":443"))
    .WithApi(api => api.WithDashboard(true))
    .WithLog(log => log.WithLevel("DEBUG"))
    .BuildAsync();

if (result.IsFailure)
    throw new InvalidOperationException(result.ValidationResult?.ErrorMessage);

var config = result.ValueOrThrow().Resolved();
```

## Build a dynamic configuration

```csharp
var result = await new TraefikDynamicConfigBuilder()
    .WithHttp(http => http
        .WithRouter("api", r => r
            .WithRule("Host(`api.example.com`)")
            .WithService("api-backend")
            .WithEntryPoint("websecure"))
        .WithService("api-backend", s => s
            .WithLoadBalancer(lb => lb /* configure servers */))
        .WithMiddleware("auth", m => m
            .WithBasicAuth(new TraefikBasicAuthMiddleware { Realm = "secure" })))
    .BuildAsync();
```

The `WithBasicAuth` call sets one branch of the discriminated `TraefikHttpMiddleware`. Setting a second branch in the same builder chain (e.g. `.WithStripPrefix(...)`) makes `BuildAsync` fail with *"requires exactly one branch to be set; found 2"*.

## Read configuration with schema validation

`TryDeserializeStatic` / `TryDeserializeDynamic` validate the YAML against the embedded JSON schema **before** deserializing into the typed POCO. They catch:

- **Typo'd keys** (the schema sets `additionalProperties: false`)
- **Wrong types** (string where bool expected, etc.)
- **Missing required properties**

```csharp
var result = TraefikSerializer.TryDeserializeStatic(yaml);

if (result.IsSuccess)
{
    var config = result.Value!;
    // ... use config
}
else
{
    // result.ValidationResult.ErrorMessage contains schema error path + message,
    // e.g. "/api: Required properties are missing from object: [dashboard]"
    Console.Error.WriteLine(result.ValidationResult?.ErrorMessage);
}
```

The non-validating throwing API (`Deserialize<T>`, `DeserializeStatic`, `DeserializeDynamic`) is still available for back-compat. It uses `IgnoreUnmatchedProperties()` and silently drops unknown keys — use the `Try*` methods for anything that touches user input.

## Atomic, schema-validated file write

The Traefik file provider watches its dynamic config file. Half-written files crash it. `WriteDynamicToFileAsync` writes to a sibling `.tmp` file first, validates the schema, then atomically renames via `File.Replace` (or `File.Move` if the destination doesn't exist), with a 3× retry on `IOException` for the Windows file-watcher race.

```csharp
var write = await TraefikSerializer.WriteDynamicToFileAsync(
    "/etc/traefik/dynamic.yml",
    config,
    cancellationToken);

if (write.IsFailure)
{
    // The schema rejected the config. Nothing was written.
    // No partial file ever exists at the destination path.
}
```

The same atomicity applies to `WriteStaticToFileAsync`.

## Read from a file

```csharp
var result = await TraefikSerializer.ReadStaticFromFileAsync("traefik.yml", ct);
```

This is `File.ReadAllTextAsync` followed by `TryDeserializeStatic`, with file-system errors mapped to `Result.Failure`.

## JSON output

Traefik accepts JSON for both static and dynamic configuration. The serializer emits camelCase via `System.Text.Json`:

```csharp
string json = TraefikSerializer.SerializeJson(config);
var roundtripped = TraefikSerializer.DeserializeJson<TraefikStaticConfig>(json);
```

## Update schemas from SchemaStore

```bash
dotnet run --project src/FrenchExDev.Net.Traefik.Bundle.Design
```

Downloads `traefik-v3-static.json` and `traefik-v3-file-provider.json` from SchemaStore into `src/FrenchExDev.Net.Traefik.Bundle/schemas/`. Rebuild to regenerate models:

```bash
dotnet build FrenchExDev.Net.Traefik.slnx
```

The Design project hits external services and is run manually.

## Run tests

```bash
# All projects, both runtime and source-generator tests
dotnet test FrenchExDev.Net.Traefik.slnx

# With coverage
dotnet test FrenchExDev.Net.Traefik.slnx \
    --collect:"XPlat Code Coverage" \
    --settings coverage.runsettings
```

## Run quality gate

```bash
dotnet run --project ../QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- \
    test --config quality-gate.yml
```

The `quality-gate.yml` in this directory carries relaxed thresholds because the generated builders for properties-heavy schema definitions naturally exceed the defaults. Pass `--config` (the CLI does *not* auto-discover the file in the current directory).

## Pack for NuGet

```bash
dotnet pack src/FrenchExDev.Net.Traefik.Bundle.Attributes/*.csproj         -c Release -o ./artifacts
dotnet pack src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/*.csproj    -c Release -o ./artifacts
dotnet pack src/FrenchExDev.Net.Traefik.Bundle/*.csproj                    -c Release -o ./artifacts
```

Three packages are produced. The SourceGenerator nupkg ships both its own analyzer DLL **and** the shared `FrenchExDev.Net.Builder.SourceGenerator.Lib` DLL under `analyzers/dotnet/cs/`. Verify with:

```bash
unzip -l artifacts/FrenchExDev.Net.Traefik.Bundle.SourceGenerator.*.nupkg
```

## Add support for a new Traefik property

No code changes required for the typical case:

1. Run the Design project to refresh the schema (or hand-edit the JSON for testing)
2. `dotnet build` — the source generator picks up the new property
3. Add a test exercising it in `BuilderTests.cs` or `RealisticRoundTripTests.cs`

## Add a new schema version (multi-version pipeline)

The pipeline supports loading multiple versions of the same schema and stamping `[SinceVersion("...")]` on properties first introduced after the earliest loaded version.

1. Drop the new schema in `src/FrenchExDev.Net.Traefik.Bundle/schemas/` following the naming convention `traefik-v{version}-{kind}.json`
2. The csproj already globs `traefik-v*.json` for both `<AdditionalFiles>` and `<EmbeddedResource>`, so no edit is needed
3. Rebuild — the merge stage union-merges definitions across versions, and `[SinceVersion("{newVersion}")]` is stamped on any property that didn't exist in earlier schemas

The synthetic [traefik-v3.1-file-provider.json](../src/FrenchExDev.Net.Traefik.Bundle/schemas/traefik-v3.1-file-provider.json) demonstrates this end-to-end with a single `httpRouter.observability` property that is stamped `[SinceVersion("3.1")]`. See `BuilderTests.HttpRouter_Observability_HasSinceVersionAttribute` for the assertion.

## Extend the source generator

### Add a new emitter

1. Create `Foo Emitter.cs` in [SourceGenerator/](../src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/) following the `TraefikModelClassEmitter` pattern
2. Call it from `TraefikBundleGenerator.Emit()` after the existing emitters
3. Add the generated source via `ctx.AddSource("Foo.g.cs", ...)`
4. Add a snapshot test in [SourceGenerator.Tests/EmitterTests.cs](../test/FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Tests/EmitterTests.cs)

### Handle a new schema pattern

1. Add the variant to `PropertyType` in [SchemaModels.cs](../src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/SchemaModels.cs)
2. **Update the structural equality** in the same file (`PropertyModel.Equals` / `GetHashCode`) to include the new fields — otherwise the IDE incremental cache will produce stale output
3. Update `TraefikSchemaReader.ParseProperty` to detect the JSON shape and produce the variant
4. Update `TraefikNamingHelper.MapCSharpType` for the new C# type
5. Update `TraefikModelClassEmitter` and `TraefikBuilderHelper` to emit the right code
6. Add an IR equality test to [SourceGenerator.Tests/IrEqualityTests.cs](../test/FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Tests/IrEqualityTests.cs)

### Add a new analyzer rule

1. Append the descriptor to [Analyzers/TraefikDiagnostics.cs](../src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/Analyzers/TraefikDiagnostics.cs)
2. Add the rule ID + severity to [AnalyzerReleases.Unshipped.md](../src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/AnalyzerReleases.Unshipped.md)
3. Implement the analyzer (see [DiscriminatedUnionAnalyzer.cs](../src/FrenchExDev.Net.Traefik.Bundle.SourceGenerator/Analyzers/DiscriminatedUnionAnalyzer.cs) for the pattern)
4. Add positive + negative tests in [SourceGenerator.Tests/AnalyzerTests.cs](../test/FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Tests/AnalyzerTests.cs) using the hand-rolled `CSharpCompilation.WithAnalyzers` harness

## Inspect generated code

`Bundle.csproj` sets `EmitCompilerGeneratedFiles=true` so the generator output is materialized on disk:

```bash
find src/FrenchExDev.Net.Traefik.Bundle/obj -path "*Generated*" -name "*.g.cs"
```

The `DebugInfo.g.cs` file at the top of that tree carries generation statistics (definition count, root property counts).
