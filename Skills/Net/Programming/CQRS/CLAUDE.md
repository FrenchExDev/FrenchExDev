# CQRS — Claude Context

Lightweight command-query separation: distinct `ICommandHandler<TCommand, TResult>` and
`IQueryHandler<TQuery, TResult>` interfaces. Events raised after persistence, not before.
No mediator or message bus.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [DDD](../DDD/) — aggregates and invariants
- [BUILDER-PATTERN](../BUILDER-PATTERN/) — command flow construction
- [RESULT-PATTERN](../RESULT-PATTERN/) — failure values

## Related packages
- [`FrenchExDev.Net.Vos`](../../../../Net/FrenchExDev/Vos/) (reference implementation with IVosBackend)

## Notes for Claude
- Commands return `Result<T>` — commands may fail (validation, invariant, persistence)
- Queries return plain types — no `Result<T>` wrapper unless genuine failure mode exists
- Never mix command and query in one handler class
- Handlers injected directly — no MediatR, no message bus
- Backend interfaces separate mutations from queries (e.g. Up/Halt vs Status/Ssh)
- Unsupported operations return typed errors, not exceptions or silent no-ops
