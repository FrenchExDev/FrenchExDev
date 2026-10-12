# QUALITY-GATES — Claude Context

Automated code quality enforcement via `dotnet quality-gate` CLI. Runs tests, collects
coverage, evaluates thresholds (cyclomatic complexity, LCOM, coupling, test quality score).
100% coverage as design pressure.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [SOLID](../SOLID/) — 4 SOLID infrastructure interfaces
- [SG](../SG/) — Roslyn-based analysis

## Related packages
- [`FrenchExDev.Net.QualityGate`](../../../../Net/FrenchExDev/QualityGate/)

## Notes for Claude
- `[ExcludeFromCodeCoverage]` permitted only with written justification
- Test quality score >= 0.95 measures assertion density, not just coverage
- `dotnet quality-gate test` for dev loop; `dotnet quality-gate check` for CI gate
- Unit tests run without MSBuild using in-memory Roslyn compilations
- No mocking frameworks — hand-written Fakes in `Fakes/` folder
- Full docs at `QualityGate/QUALITY-GATE.md`
