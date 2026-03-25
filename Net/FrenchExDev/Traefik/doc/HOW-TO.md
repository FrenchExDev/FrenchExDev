# How-To

## Build a Static Configuration

```csharp
var result = await new TraefikStaticConfigBuilder()
    .WithEntryPoint("web", ep => ep.WithAddress(":80"))
    .WithEntryPoint("websecure", ep => ep.WithAddress(":443"))
    .WithApi(api => api.WithDashboard(true))
    .WithLog(log => log.WithLevel("DEBUG"))
    .BuildAsync();

var config = result.ValueOrThrow().Value;
string yaml = TraefikSerializer.Serialize(config);
File.WriteAllText("traefik.yml", yaml);
```

## Build a Dynamic Configuration

```csharp
var result = await new TraefikDynamicConfigBuilder()
    .WithHttp(http => http
        .WithRouter("my-app", r => r
            .WithRule("Host(`app.example.com`)")
            .WithService("my-service")
            .WithEntryPoint("websecure")
            .WithMiddleware("auth"))
        .WithService("my-service", s => s
            .WithLoadBalancer(lb => lb /* configure servers */))
        .WithMiddleware("auth", m => m
            .WithBasicAuth(ba => ba /* configure users */)))
    .BuildAsync();

var config = result.ValueOrThrow().Value;
string yaml = TraefikSerializer.Serialize(config);
File.WriteAllText("dynamic.yml", yaml);
```

## Deserialize Existing Configuration

```csharp
// Static
string staticYaml = File.ReadAllText("traefik.yml");
var staticConfig = TraefikSerializer.DeserializeStatic(staticYaml);

// Dynamic
string dynamicYaml = File.ReadAllText("dynamic.yml");
var dynamicConfig = TraefikSerializer.DeserializeDynamic(dynamicYaml);

// Generic
var config = TraefikSerializer.Deserialize<TraefikStaticConfig>(yaml);
```

## Update Schemas from SchemaStore

The Design project downloads the latest schemas:

```bash
dotnet run --project src/FrenchExDev.Net.Traefik.Bundle.Design
```

This fetches `traefik-v3-static.json` and `traefik-v3-file-provider.json` from SchemaStore into `src/FrenchExDev.Net.Traefik.Bundle/schemas/`. After downloading, rebuild to regenerate models:

```bash
dotnet build
```

## Run Tests

```bash
# All tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings
```

## Run Quality Gate

```bash
dotnet quality-gate test --config quality-gate.yml
```

## Add Support for a New Traefik Property

No code changes needed. The property will appear automatically after updating the schema:

1. Run the Design project to download the latest schema
2. Rebuild -- the source generator picks up the new property
3. Add tests for the new property in `BuilderTests.cs` or `SerializerTests.cs`

## Extend the Source Generator

### Add a new emitter

1. Create a new emitter class in `Traefik.Bundle.SourceGenerator` (follow `TraefikModelClassEmitter` pattern)
2. Call it from `TraefikBundleGenerator.Generate()` after the merge step
3. Add the generated source via `ctx.AddSource()`

### Handle a new schema pattern

1. Add the new `PropertyType` variant to `SchemaModels.cs`
2. Update `TraefikSchemaReader.ParseProperty()` to detect the pattern
3. Update `TraefikNamingHelper.MapCSharpType()` for the new type
4. Update `TraefikModelClassEmitter` to emit the correct property declaration
5. Update `TraefikBuilderHelper` to create the correct builder property model

### Add a new schema file

1. Place the JSON schema in `src/FrenchExDev.Net.Traefik.Bundle/schemas/`
2. Name it `traefik-v{version}-{kind}.json` (the generator matches `traefik-v3-*.json`)
3. Update `TraefikSchemaReader.DetectKind()` if the kind is not `static` or `file-provider`
4. The `.csproj` already includes `schemas/traefik-v3-*.json` as AdditionalFiles

## Inspect Generated Code

The generated sources live in the `obj/` tree. To inspect them:

```bash
# Find all generated files
find . -path "*/Generated/*.g.cs" -name "Traefik*"

# Or check the DebugInfo for statistics
find . -path "*/Generated/DebugInfo.g.cs"
```
