# Philosophy

## Design Decisions

### Schema as Single Source of Truth

All models and builders are generated from Traefik's official JSON schemas hosted on SchemaStore. No hand-written model classes exist. This guarantees that the C# API surface matches the real Traefik configuration spec exactly, and updating to a new schema version regenerates everything automatically.

### Two Separate Root Types

Traefik fundamentally separates static (startup) from dynamic (runtime) configuration. Rather than merging them into one object, the library preserves this distinction with `TraefikStaticConfig` and `TraefikDynamicConfig`. This mirrors how Traefik actually loads configuration and prevents mixing concerns.

### Discriminated Unions as Flat Classes

Traefik's middleware and service types are discriminated unions (e.g., a middleware is exactly one of: addPrefix, basicAuth, chain, ...). The generator emits these as flat classes with nullable properties rather than using inheritance or wrapper types. This is a pragmatic choice:

- **Serialization**: YAML maps naturally to nullable properties. No custom converters needed.
- **Discovery**: all available types are visible via IntelliSense on a single class.
- **Simplicity**: no abstract base, no visitor pattern, no casting.

The trade-off is that nothing prevents setting two properties simultaneously at compile time. Builders mitigate this by offering `With{Branch}()` methods that guide correct usage.

### Reuse of Builder.SourceGenerator.Lib

The builder generation logic is not duplicated. `TraefikBuilderHelper` translates schema models into `BuilderEmitModel` and delegates to the shared `BuilderEmitter`. This is the same emission pipeline used by DockerCompose.Bundle, ensuring consistency and reducing maintenance surface.

### YAML-Native Design

Traefik configuration files are YAML. The serializer uses `CamelCaseNamingConvention` so that C# PascalCase properties map directly to Traefik's camelCase keys without annotations. Null omission keeps serialized output clean. Unmatched properties are ignored on deserialization for forward compatibility with newer Traefik versions.

### Embedded Schemas

The JSON schemas are embedded as resources in the Bundle assembly. This makes the package self-contained -- consumers don't need to distribute schema files separately. The source generator reads them as `AdditionalFiles` at compile time; the test project reads them as embedded resources at runtime.

### Version Tracking Infrastructure

`TraefikSchemaVersions` and the `SinceVersion`/`UntilVersion` attributes are generated even though only one schema version (v3) currently exists. This prepares the library for future multi-version support without requiring architectural changes.

## Trade-offs

| Decision | Benefit | Cost |
|---|---|---|
| Schema-driven generation | Always in sync with Traefik spec | Longer build, generator complexity |
| Flat discriminated unions | Simple serialization, good discoverability | No compile-time exclusivity enforcement |
| Shared BuilderEmitter | Consistent builders, single maintenance point | Coupling to Builder.SG.Lib API |
| Embedded schemas | Self-contained package | Bundle assembly size (~120KB for schemas) |
| camelCase naming convention | Zero-annotation YAML mapping | C# property names must be PascalCase equivalents |
