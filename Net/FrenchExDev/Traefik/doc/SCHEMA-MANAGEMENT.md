# Schema Management

## Schema Sources

The Traefik Bundle uses two JSON schemas from SchemaStore:

| Schema | File | Content |
|---|---|---|
| Static | `traefik-v3-static.json` | Entry points, providers, API, logging, metrics, tracing, certificate resolvers |
| Dynamic (File Provider) | `traefik-v3-file-provider.json` | HTTP/TCP/UDP routers, services, middlewares, TLS stores and options |

Source URLs:
- `https://raw.githubusercontent.com/SchemaStore/schemastore/master/src/schemas/json/traefik-v3.json`
- `https://raw.githubusercontent.com/SchemaStore/schemastore/master/src/schemas/json/traefik-v3-file-provider.json`

## Storage

Schemas are stored in `src/FrenchExDev.Net.Traefik.Bundle/schemas/` and serve dual purpose:

1. **Compile time**: listed as `<AdditionalFiles>` in the `.csproj`, consumed by the source generator
2. **Runtime**: embedded as assembly resources, accessible for validation or introspection

## Update Workflow

```bash
# 1. Download latest schemas
dotnet run --project src/FrenchExDev.Net.Traefik.Bundle.Design

# 2. Rebuild to regenerate models
dotnet build

# 3. Run tests to verify nothing broke
dotnet test

# 4. Inspect changes
git diff src/FrenchExDev.Net.Traefik.Bundle/schemas/
```

The Design project uses the `FrenchExDev.Net.Wrapper.Versioning` pipeline:
- `StaticItemCollector` provides the known schema URLs
- `UseHttpDownload()` fetches the JSON
- `UseSave()` writes to the schemas directory

## Filename Convention

```
traefik-v{version}-{kind}.json
```

- **version**: major Traefik version (currently `3`)
- **kind**: `static` for startup config, `file-provider` for dynamic config

The source generator matches files via the pattern `traefik-v3-*.json` in AdditionalFiles. `TraefikSchemaReader` extracts the version and kind from the filename:

- `DetectKind("traefik-v3-file-provider.json")` returns `SchemaKind.Dynamic`
- `DetectKind("traefik-v3-static.json")` returns `SchemaKind.Static`
- `ExtractVersion("traefik-v3-static.json")` returns `"3"`

## Version Tracking

The generator emits `TraefikSchemaVersions` with:

```csharp
public static class TraefikSchemaVersions
{
    public static IReadOnlyList<string> Available => ...;
    public static string Latest => "3";
    public static string Oldest => "3";
}
```

Properties that exist in only a subset of schema versions get annotated:

```csharp
[SinceVersion("3")]
public string? SomeNewProperty { get; set; }
```

Currently only v3 schemas exist, so all properties span the full range. The infrastructure is ready for when v4 schemas appear.

## Schema Structure

### Static Schema

Top-level properties map directly to `TraefikStaticConfig` fields:

```
entryPoints, providers, api, log, accessLog, metrics, tracing,
certificatesResolvers, serversTransport, experimental, ...
```

Definitions include provider configurations (Docker, Kubernetes, File, Consul, etc.), transport settings, and middleware defaults.

### Dynamic Schema (File Provider)

Top-level is sectioned by protocol:

```
http:
  routers: { "name": { rule, service, entryPoints, middlewares } }
  services: { "name": { loadBalancer | weighted | mirroring } }
  middlewares: { "name": { addPrefix | basicAuth | chain | ... } }
tcp: { routers, services, middlewares }
udp: { routers, services }
tls: { stores, options, certificates }
```

Each section uses `additionalProperties` with `$ref` to create `Dictionary<string, T>` types. Middleware and service types use discriminated `oneOf`.

## Adding a New Schema Version

1. Add the new schema files following the naming convention
2. Update the `<AdditionalFiles>` glob in the `.csproj` if the version prefix changed
3. The generator will automatically merge definitions across versions
4. Properties unique to specific versions get `SinceVersion`/`UntilVersion` annotations
5. Update tests to cover version-specific behavior
