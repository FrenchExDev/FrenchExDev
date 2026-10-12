# SOLID — Claude Context

SOLID principles as applied in the codebase: 1-method interfaces at infrastructure seams,
DI via optional constructor params with defaults, hand-written fakes only, extension points
over modification.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [QUALITY-GATES](../QUALITY-GATES/) — ISolutionLoader pattern
- [VOS-ORCHESTRATION](../VOS-ORCHESTRATION/) — IVosBackend pattern
- [HAND-WRITTEN-FAKES](../HAND-WRITTEN-FAKES/) — test doubles

## Related packages
- All packages under [`Net/FrenchExDev/`](../../../../Net/FrenchExDev/)

## Notes for Claude
- Narrow interfaces make Fakes trivial (3-5 lines)
- No DI container required; sensible defaults enable production use with zero config
- Unsupported operations return typed errors, never throw or silently succeed
- Open/closed enforced via extension points (BuilderEmitModel, DesignPipeline)
- Liskov via capability discovery (e.g. `IVosBackend.SupportedActions`)
