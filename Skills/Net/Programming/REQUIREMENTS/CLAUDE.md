# REQUIREMENTS — Claude Context

Type-safe requirement traceability: requirements as abstract classes, acceptance criteria
as abstract methods, 3 traceability attributes (`[ForRequirement]`, `[Verifies]`,
`[TestsFor]`). SG emits `RequirementRegistry` and analyzer detects gaps.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [SG](../SG/) — source generation pattern
- [DDD](../DDD/) — Result for AC results

## Related packages
- [`FrenchExDev.Net.Requirements`](../../../../Net/FrenchExDev/Requirements/)

## Notes for Claude
- `nameof()` on AC methods is stable across renames (automatic IDE propagation)
- REQ*02 diagnostics (broken references) are Errors; REQ*00/01 (missing coverage) are Warnings
- Domain concept types (UserId, Email) must be `readonly struct` with zero external deps
- 7-project decomposition: core, attributes, SG, .Lib, analyzers, testing, tests
- `.Lib` has zero Roslyn dependency — extraction in SG, emission in Lib
