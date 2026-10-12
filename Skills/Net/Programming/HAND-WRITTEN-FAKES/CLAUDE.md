# HAND-WRITTEN-FAKES — Claude Context

Replace mocking frameworks with small, debuggable fake classes. Three patterns: Stub (canned data), Spy (captures arguments), Programmable (per-input behavior). Fakes are `internal sealed` in a `Fakes/` folder.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [SOLID](../SOLID/) — narrow interfaces make fakes trivial

## Related packages
- All packages with tests

## Notes for Claude
- Never inherit one fake from another — duplicate the class
- Never put fakes inside test classes as nested types
- Never use `Mock<T>` "just for this one test" — scope creep is real
- Test helpers (Roslyn builders, file-system fixtures) go in `Helpers/`, not `Fakes/`
- Default constructors take canned data; no reflection-based setup
- A configurable fake with 12 boolean knobs is a code smell — split into per-scenario factory methods
