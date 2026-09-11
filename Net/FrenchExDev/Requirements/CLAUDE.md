# Requirements — Claude Context

Requirements DSL built on the M3 framework: model epics, features, and acceptance criteria as decorated C# classes; trace production code and tests back to requirements via `[ForRequirement]` and `[Verifies]`. Source generator emits the discovery and traceability surface.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [REQUIREMENTS](../../../Skills/Net/Programming/REQUIREMENTS/PHILOSOPHY.md)
- [DSL-FOUNDATIONS](../../../Skills/Net/Programming/DSL-FOUNDATIONS/PHILOSOPHY.md)
- [SG](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Requirements.slnx`

## Notes for Claude
- Acceptance criteria are abstract methods on a `Feature<T>` subclass — they are signatures, not implementations. The traceability attributes (`[ForRequirement]`, `[Verifies]`) reference them via `nameof(...)`.
- Built on the M3 framework; the same conventions apply (companion classes, constraint methods, zero runtime dependencies in attribute projects).
- Don't conflate this with the centralized `Skills/Net/Programming/REQUIREMENTS/` skill — that one captures repo-wide requirements practices; this package is the runtime + SG that implements the DSL.
