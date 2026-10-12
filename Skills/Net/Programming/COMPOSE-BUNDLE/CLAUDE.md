# COMPOSE-BUNDLE — Claude Context

Strongly-typed C# object model for external config formats (Docker Compose, Traefik, GitLab CI) generated from upstream JSON schemas. Version-aware with `[SinceVersion]`/`[UntilVersion]` per property. Uses shared `BuilderEmitter` from Builder.SourceGenerator.Lib. Contributor pattern for composition.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [BUILDER-PATTERN](../BUILDER-PATTERN/) — BuilderEmitter reuse
- [DSL-FOUNDATIONS](../DSL-FOUNDATIONS/) — schema-driven modeling
- [DESIGN-PHASED-PROJECT](../DESIGN-PHASED-PROJECT/) — schema download phase

## Related packages
- [`FrenchExDev.Net.Builder.SourceGenerator.Lib`](../../../../Net/FrenchExDev/Builder/)

## Notes for Claude
- Never edit `*.g.cs` files — overwritten on every build
- Schema downloader runs manually, never on build — `dotnet run --project Bundle.Design`
- At least 5 schemas of distinct versions required — single-version wrappers hide versioning bugs
- `oneOf` resolution order matters: `[string, object{props}]` then StringOrObject, then `[string, integer]` then StringOrInteger
- Never bypass `Builder.SourceGenerator.Lib` — forking produces inconsistency across monorepo
- YAML round-trips but the C# model is the source of truth
