# INJECTABLE-DI — Claude Context

Attribute-driven DI registration via `[Injectable]`. Lifetime declared on the class, not in composition root. Separate source generators per container (Microsoft, DryIoc). Four Roslyn analyzers for compile-time safety.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [MAPPER-PATTERN](../MAPPER-PATTERN/) — SG architecture
- [BUILDER-PATTERN](../BUILDER-PATTERN/) — shared emitter lib

## Related packages
- [`FrenchExDev.Net.Injectable`](../../../../Net/FrenchExDev/Injectable/)

## Notes for Claude
- Do NOT decorate abstract classes — INJECT002 warns
- Captive dependency (Singleton depending on Scoped) = INJECT001 = real bug
- Open generics auto-detected: `Repo<T> : IRepo<T>` -> registered as `typeof(IRepo<>), typeof(Repo<>)`
- `[InjectableDefaults]` assembly-level sets default scope for undecorated classes
- Interface can declare scope contract; implementations with mismatched scope = INJECT004 error
- Do NOT put `[Injectable]` on DTOs, entities, or value objects — only services
- DryIoc decorators use native `Setup.Decorator`; Microsoft DI uses service replacement
