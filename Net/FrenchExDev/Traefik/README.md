# FrenchExDev.Net.Traefik.Bundle

Strongly-typed .NET configuration library for Traefik reverse proxy, generated from official JSON schemas. Provides compile-time-safe models, fluent builders, and round-trip YAML serialization for both static and dynamic Traefik configuration.

## Quick Start

```csharp
// Build a static configuration
var staticConfig = new TraefikStaticConfigBuilder()
    .WithEntryPoint("web", ep => ep.WithAddress(":80"))
    .WithEntryPoint("websecure", ep => ep.WithAddress(":443"))
    .BuildAsync()
    .Result.ValueOrThrow().Value;

// Serialize to YAML
string yaml = TraefikSerializer.Serialize(staticConfig);

// Build a dynamic configuration
var dynamicConfig = new TraefikDynamicConfigBuilder()
    .WithHttp(http => http
        .WithRouter("my-app", r => r
            .WithRule("Host(`app.example.com`)")
            .WithService("my-service")
            .WithEntryPoint("websecure"))
        .WithService("my-service", s => s
            .WithLoadBalancer(lb => lb /* ... */)))
    .BuildAsync()
    .Result.ValueOrThrow().Value;

// Deserialize from YAML
var config = TraefikSerializer.DeserializeStatic(File.ReadAllText("traefik.yml"));
```

## Solution Structure

| Project | Target | Purpose |
|---|---|---|
| `Traefik.Bundle` | net10.0 | Models, builders, serializer (consumer-facing) |
| `Traefik.Bundle.Attributes` | netstandard2.0; net10.0 | `[TraefikBundle]` marker attribute |
| `Traefik.Bundle.SourceGenerator` | netstandard2.0 | Incremental source generator (Roslyn analyzer) |
| `Traefik.Bundle.Design` | net10.0 | Design-time schema download utility |
| `Traefik.Bundle.Tests` | net10.0 | xUnit tests (models, builders, schemas, serialization) |

## Key Features

- **Schema-driven** -- all models generated from Traefik's official JSON schemas (SchemaStore)
- **Two-tier configuration** -- separate `TraefikStaticConfig` and `TraefikDynamicConfig` root types
- **Discriminated unions** -- middleware and service types modeled as flat classes with nullable branches
- **Fluent builders** -- generated via shared `BuilderEmitter` from `Builder.SourceGenerator.Lib`
- **YAML round-trip** -- camelCase serialization/deserialization with `YamlDotNet`
- **Version tracking** -- `TraefikSchemaVersions` class with `SinceVersion`/`UntilVersion` attributes

## Dependencies

| Package / Project | Role |
|---|---|
| `FrenchExDev.Net.Builder` | `AbstractBuilder<T>` base class |
| `FrenchExDev.Net.Builder.SourceGenerator.Lib` | Shared builder emission logic |
| `FrenchExDev.Net.Result` | `Result<T>` return types from builders |
| `YamlDotNet` | YAML serialization |

## Running Tests

```bash
cd Net/FrenchExDev/Traefik
dotnet test
```

## Updating Schemas

```bash
dotnet run --project src/FrenchExDev.Net.Traefik.Bundle.Design
```

This downloads the latest `traefik-v3-static.json` and `traefik-v3-file-provider.json` from SchemaStore into `src/FrenchExDev.Net.Traefik.Bundle/schemas/`.

## Documentation

| Document | Content |
|---|---|
| [ARCHITECTURE](doc/ARCHITECTURE.md) | Project decomposition, source generator pipeline, generated code layout |
| [PHILOSOPHY](doc/PHILOSOPHY.md) | Design decisions and trade-offs |
| [HOW-TO](doc/HOW-TO.md) | Common tasks: adding config, extending the generator, testing |
| [SCHEMA-MANAGEMENT](doc/SCHEMA-MANAGEMENT.md) | Schema sourcing, update workflow, version tracking |
