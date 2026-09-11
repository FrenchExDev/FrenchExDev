# Traefik — Claude Context

Strongly-typed .NET configuration library for Traefik reverse proxy. Models
and builders are generated from Traefik's official JSON schemas (SchemaStore),
with a two-tier split (`TraefikStaticConfig` + `TraefikDynamicConfig`),
camelCase YAML round-tripping via YamlDotNet, and version tracking via
`TraefikSchemaVersions`.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Schema Management](doc/SCHEMA-MANAGEMENT.md)

## Relevant skills
- [COMPOSE-BUNDLE](../../../Skills/Net/Programming/COMPOSE-BUNDLE/PHILOSOPHY.md)
- [SG](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [BUILDER-PATTERN](../../../Skills/Net/Programming/BUILDER-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Traefik.Bundle.slnx`

## Notes for Claude
- The package name in the solution is `Traefik.Bundle` (not just `Traefik`) — it follows the compose-bundle pattern.
- Two distinct root types: `TraefikStaticConfig` (entrypoints, providers, log) and `TraefikDynamicConfig` (routers, middlewares, services). Don't merge them.
- Traefik middleware/service discriminated unions are flattened into single classes with nullable branches — pick exactly one branch per instance.
- Schemas are sourced from SchemaStore, not GitHub releases. The Design project downloads `traefik-v3-static.json` and `traefik-v3-file-provider.json`.
- YAML uses camelCase, not snake_case (unlike compose-spec). The serializer is configured accordingly.
- Builders are emitted via shared `Builder.SourceGenerator.Lib` — same as every other bundle in the monorepo.
