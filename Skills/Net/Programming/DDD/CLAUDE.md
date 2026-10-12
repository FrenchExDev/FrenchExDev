# DDD — Claude Context

Attribute-based DDD (aggregates, entities, value objects, invariants, relationships) via
source generation. M3 MetaConcept companions for every attribute. Invariants return
`Result`, never throw.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [DSL-FOUNDATIONS](../DSL-FOUNDATIONS/) — M3 metamodel
- [BUILDER-PATTERN](../BUILDER-PATTERN/) — construction
- [RESULT-PATTERN](../RESULT-PATTERN/) — invariant results
- [CQRS](../CQRS/) — command/query separation

## Related packages
- [`FrenchExDev.Net.Ddd`](../../../../Net/FrenchExDev/Ddd/)
- [`FrenchExDev.Net.Ddd.Attributes`](../../../../Net/FrenchExDev/Ddd/)
- [`FrenchExDev.Net.Ddd.SourceGenerator`](../../../../Net/FrenchExDev/Ddd/)

## Notes for Claude
- Every `[AggregateRoot]` must have exactly one `[EntityId]` property — SG fails without it
- Every DDD attribute must have a `[MetaConcept]` companion — no naked attributes
- `[Composition]` = inside aggregate (cascade); never use cross-aggregate
- `[Association]` = cross-aggregate (nullable); `[Aggregation]` = weak reference
- Invariants never throw — return `Result.Success()` or `Result.Failure("...")`
- Multiple invariant failures aggregate via `Result.Combine()` — no short-circuit
