# FrenchExDev.Net.Traefik.Bundle

Strongly-typed .NET configuration library for [Traefik](https://traefik.io/) v3. Models, builders, validator, and YAML/JSON round-trip serializer — all generated from Traefik's official JSON schemas.

- **Two-tier configuration** — separate `TraefikStaticConfig` and `TraefikDynamicConfig` root types, mirroring how Traefik actually loads its configuration
- **Schema-validated I/O** — `Try*` methods validate against the embedded JSON schema, including strict unknown-key detection
- **Atomic file writes** — `WriteStaticToFileAsync` writes via a `.tmp` sibling + `File.Replace`, safe for Traefik's file provider watch loop
- **Compile-time guardrails** — Roslyn analyzer (TFK001) flags discriminated-union misuse before runtime
- **Multi-version pipeline** — `[SinceVersion]`/`[UntilVersion]` attributes are stamped from the schema delta across loaded versions

## Quick start

### Build, validate, and write

```csharp
using FrenchExDev.Net.Traefik.Bundle;

var dynamicConfig = (await new TraefikDynamicConfigBuilder()
    .WithHttp(http => http
        .WithRouter("api", r => r
            .WithRule("Host(`api.example.com`)")
            .WithService("api-backend")
            .WithEntryPoint("websecure"))
        .WithService("api-backend", s => s
            .WithLoadBalancer(lb => lb /* ... */)))
    .BuildAsync())
    .ValueOrThrow().Resolved();

// Atomic, schema-validated write — safe to call repeatedly while Traefik
// is watching the file. The schema is checked BEFORE any bytes hit disk.
var write = await TraefikSerializer.WriteDynamicToFileAsync(
    "/etc/traefik/dynamic.yml", dynamicConfig);

if (write.IsFailure)
    throw new InvalidOperationException("Refused to write invalid config");
```

### Read and validate

```csharp
// Schema-validating read. Catches unknown keys (typos) and type errors —
// not just structural problems.
var result = await TraefikSerializer.ReadStaticFromFileAsync("traefik.yml");

if (result.IsSuccess)
{
    var config = result.Value!;
    Console.WriteLine($"Loaded {config.EntryPoints?.Count ?? 0} entry points");
}
else
{
    Console.Error.WriteLine(result.ValidationResult?.ErrorMessage);
}
```

### Discriminated union: exactly-one-branch enforcement

```csharp
var middleware = await new TraefikHttpMiddlewareBuilder()
    .WithStripPrefix(new TraefikStripPrefixMiddleware { Prefixes = new() { "/api" } })
    .WithBasicAuth(new TraefikBasicAuthMiddleware())  // ← second branch!
    .BuildAsync();

// middleware.IsFailure == true
// "TraefikHttpMiddleware requires exactly one branch to be set; found 2."
```

The same misuse is also flagged at compile time by analyzer rule **TFK001** when both branches are visible in a single object initializer.

## Packages

| Package | Target | Role |
|---|---|---|
| `FrenchExDev.Net.Traefik.Bundle` | net10.0 | Models, builders, serializer (consumer-facing) |
| `FrenchExDev.Net.Traefik.Bundle.Attributes` | netstandard2.0; net10.0 | `[TraefikBundle]`, `[TraefikDiscriminatedUnion]` markers |
| `FrenchExDev.Net.Traefik.Bundle.SourceGenerator` | netstandard2.0 | Roslyn incremental generator + analyzer (TFK001, TFK004) |

The runtime package depends on `FrenchExDev.Net.Result` (for `Result<T>` returns), `FrenchExDev.Net.Builder` (for `AbstractBuilder<T>`), `YamlDotNet`, and `JsonSchema.Net`.

## Diagnostics

| ID | Severity | What it catches |
|---|---|---|
| **TFK001** | Warning | Two or more branches set on a discriminated union (`TraefikHttpMiddleware`, `TraefikHttpService`, etc.) in the same object initializer |
| **TFK004** | Warning | `[TraefikBundle]` consumer with no `traefik-v*.json` files wired as `<AdditionalFiles>` |

`[Obsolete]` is stamped on properties marked `deprecated` in the schema, so the standard `CS0618` warning replaces a custom rule.

## Building & testing

```bash
cd Net/FrenchExDev/Traefik
dotnet build FrenchExDev.Net.Traefik.slnx
dotnet test  FrenchExDev.Net.Traefik.slnx
dotnet run --project ../QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test --config quality-gate.yml
```

## Updating schemas

```bash
dotnet run --project src/FrenchExDev.Net.Traefik.Bundle.Design
```

Downloads `traefik-v3-static.json` and `traefik-v3-file-provider.json` from SchemaStore into `src/FrenchExDev.Net.Traefik.Bundle/schemas/`. Rebuild to regenerate models. See [SCHEMA-MANAGEMENT](doc/SCHEMA-MANAGEMENT.md) for details.

## Documentation

| Document | Content |
|---|---|
| [ARCHITECTURE](doc/ARCHITECTURE.md) | Project decomposition, source generator pipeline, IR types, analyzer wiring |
| [HOW-TO](doc/HOW-TO.md) | Common tasks: building, reading, validating, atomic writes, extending the SG |
| [PHILOSOPHY](doc/PHILOSOPHY.md) | Design decisions and trade-offs |
| [SCHEMA-MANAGEMENT](doc/SCHEMA-MANAGEMENT.md) | Schema sourcing, multi-version merge, version tracking |
