# Schema Management

## Schema sources

Two JSON schemas from SchemaStore drive the entire generated API:

| Schema | File | Content |
|---|---|---|
| Static | `traefik-v3-static.json` | Entry points, providers, API, log, accessLog, metrics, tracing, certificate resolvers, transport |
| Dynamic (file provider) | `traefik-v3-file-provider.json` | HTTP/TCP/UDP routers, services, middlewares, TLS stores and options |

Source URLs (used by the Design downloader):

- `https://raw.githubusercontent.com/SchemaStore/schemastore/master/src/schemas/json/traefik-v3.json`
- `https://raw.githubusercontent.com/SchemaStore/schemastore/master/src/schemas/json/traefik-v3-file-provider.json`

## Storage

Schemas live in [src/FrenchExDev.Net.Traefik.Bundle/schemas/](../src/FrenchExDev.Net.Traefik.Bundle/schemas/) and serve **three** purposes:

1. **Compile time** — listed as `<AdditionalFiles>` in `Bundle.csproj`, consumed by `TraefikBundleGenerator` to emit models and builders.
2. **Runtime validation** — embedded as assembly resources via `<EmbeddedResource>`, loaded lazily by `TraefikSerializer.LoadEmbeddedSchema` for `JsonSchema.Net` validation in the `Try*` API.
3. **Self-contained packaging** — consumers don't need to distribute the schemas separately; they ship inside the `FrenchExDev.Net.Traefik.Bundle` nupkg.

## Update workflow

```bash
# 1. Download latest schemas
dotnet run --project src/FrenchExDev.Net.Traefik.Bundle.Design

# 2. Rebuild — picks up the new schemas, regenerates everything
dotnet build FrenchExDev.Net.Traefik.slnx

# 3. Run tests to confirm nothing broke
dotnet test FrenchExDev.Net.Traefik.slnx

# 4. Inspect the schema diff
git diff src/FrenchExDev.Net.Traefik.Bundle/schemas/
```

The Design project uses `FrenchExDev.Net.Wrapper.Versioning`:

- `StaticItemCollector` provides the SchemaStore URLs
- `UseHttpDownload()` fetches the JSON
- `UseSave()` writes to the schemas directory

The Design CLI hits external services and is run **manually**. Don't wire it into CI without rate-limit handling.

## Filename convention

```
traefik-v{version}-{kind}.json
```

| Field | Examples | Notes |
|---|---|---|
| `version` | `3`, `3.1`, `4` | Major or major.minor; sorted by `StringComparer.Ordinal` during merge |
| `kind` | `static`, `file-provider` | `file-provider` ⇒ Dynamic; everything else ⇒ Static |

The source generator matches files via the pattern `traefik-v*.json` in `<AdditionalFiles>`. `TraefikSchemaReader` extracts version and kind from the filename:

- `DetectKind("traefik-v3-file-provider.json")` ⇒ `SchemaKind.Dynamic`
- `DetectKind("traefik-v3-static.json")` ⇒ `SchemaKind.Static`
- `ExtractVersion("traefik-v3.1-file-provider.json")` ⇒ `"3.1"`

## Multi-version pipeline

The generator can load multiple schema versions side-by-side. The merge stage:

1. Sorts schemas by version (`StringComparer.Ordinal`)
2. Walks them in order, performing a **union merge** of definitions and properties
3. Records the first version each `(definition, property)` pair appears in
4. Stamps `[SinceVersion("{version}")]` on properties whose first-seen version is *not* the earliest loaded schema

```csharp
// In the synthetic v3.1 schema, httpRouter gains an `observability` property
// that doesn't exist in v3. The generator stamps it accordingly:

public partial class TraefikHttpRouter
{
    public string? Rule { get; set; }                  // unchanged across versions

    [SinceVersion("3.1")]
    public string? Observability { get; set; }         // new in v3.1
}
```

The asserting test is `BuilderTests.HttpRouter_Observability_HasSinceVersionAttribute`. The synthetic schema lives at [src/FrenchExDev.Net.Traefik.Bundle/schemas/traefik-v3.1-file-provider.json](../src/FrenchExDev.Net.Traefik.Bundle/schemas/traefik-v3.1-file-provider.json) — it's intentionally minimal (one property) and is meant to be **replaced** with the real v3.1 delta when SchemaStore publishes one.

### `TraefikSchemaVersions` runtime API

```csharp
public static class TraefikSchemaVersions
{
    public static IReadOnlyList<string> Available { get; }   // e.g. ["3", "3.1"]
    public static string Latest { get; }                     // "3.1"
    public static string Oldest { get; }                     // "3"
}
```

The `BuilderTests.TraefikSchemaVersions_ContainsBothLoadedVersions` test pins this contract.

### Adding a new schema version

1. Drop the file in `schemas/` following the naming convention.
2. The csproj globs `traefik-v*.json` for both `<AdditionalFiles>` and `<EmbeddedResource>`, so no project edit is needed.
3. Rebuild. New properties get `[SinceVersion("{newVersion}")]` automatically.
4. Add tests if the new version introduces a property worth pinning.

## Schema structure (v3 reference)

### Static schema

Top-level properties map directly to `TraefikStaticConfig` fields:

```
entryPoints, providers, api, log, accessLog, metrics, tracing,
certificatesResolvers, serversTransport, experimental, …
```

Definitions cover provider configurations (Docker, Kubernetes, File, Consul, …), transport settings, and middleware defaults.

### Dynamic schema (file provider)

Top-level is sectioned by protocol:

```
http:
  routers     : { "<name>": { rule, service, entryPoints, middlewares, … } }
  services    : { "<name>": { loadBalancer | weighted | mirroring } }
  middlewares : { "<name>": { addPrefix | basicAuth | chain | … } }
tcp:
  routers     : { "<name>": { … } }
  services    : { "<name>": { loadBalancer | weighted } }
  middlewares : { "<name>": { … } }
udp:
  routers, services
tls:
  stores, options, certificates
```

Each `routers`/`services`/`middlewares` map uses `additionalProperties` with a `$ref`, which the generator emits as `Dictionary<string, T>`. Middleware and service types are flat unions emitted from `oneOf` blocks.

## Schema validation at runtime

The `TraefikSerializer.Try*` methods load the embedded schemas lazily via `Lazy<JsonSchema?>` (cached per process) and validate user-supplied YAML against them through the [YamlToJson](../src/FrenchExDev.Net.Traefik.Bundle/YamlToJson.cs) conversion path. See [PHILOSOPHY](PHILOSOPHY.md#strict-yaml-validation-via-yamltojson-not-the-typed-deserializer) for why the conversion is hand-rolled instead of using the typed YamlDotNet pipeline.
