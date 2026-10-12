# BUILDER-PATTERN — Claude Context

Async builder pattern: validate all inputs before allocation, handle circular graphs via `Reference<T>` + `VisitedObjects`, single-flight for concurrent callers. SG generates `With*()` fluent methods, validation hooks, and instantiation bridge.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [RESULT-PATTERN](../RESULT-PATTERN/) — failure values
- [GUARD-CLAUSES](../GUARD-CLAUSES/) — preconditions
- [DDD](../DDD/) — construction

## Related packages
- [`FrenchExDev.Net.Builder`](../../../../Net/FrenchExDev/Builder/)
- [`FrenchExDev.Net.Builder.Attributes`](../../../../Net/FrenchExDev/Builder/src/FrenchExDev.Net.Builder.Attributes/)
- [`FrenchExDev.Net.Builder.SourceGenerator`](../../../../Net/FrenchExDev/Builder/src/FrenchExDev.Net.Builder.SourceGenerator/)
- [`FrenchExDev.Net.Builder.SourceGenerator.Lib`](../../../../Net/FrenchExDev/Builder/src/FrenchExDev.Net.Builder.SourceGenerator.Lib/)

## Notes for Claude
- Single-flight is not optional — concurrent callers must receive same instance for graphs
- `reference.Resolve(instance)` must be called BEFORE building children (graph order)
- Never throw inside `Validate*()` hooks — `yield return` exception values instead
- Never cache build results outside the builder — single-flight is built in
- Four instantiation strategies: `init` (default) | `ctor` | `factory:Name` | `custom`
- Mode 1 returns `Task<Result<Reference<T>>>`; Mode 2 returns `Task<Result<T, TException>>`
- `Reference<T>` and `VisitedObjects` are completely hidden from developers in SG mode
