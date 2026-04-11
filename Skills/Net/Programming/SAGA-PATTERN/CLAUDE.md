# SAGA-PATTERN — Claude Context

Multi-step distributed transaction with forward execution and reverse compensation. State machine: Pending -> Running -> (Completed | Compensating -> Compensated | Failed). Steps return `Task<Result>`. Consumer owns persistence via `ISagaStore`.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [RESULT-PATTERN](../RESULT-PATTERN/) — step return values
- [DDD](../DDD/) — aggregate context

## Related packages
- [`FrenchExDev.Net.Saga`](../../../../Net/FrenchExDev/Saga/)

## Notes for Claude
- Failed step itself is NOT compensated — only previously successful steps are
- If compensation fails, saga ends in `Failed` state (not `Compensating`)
- Compensation must be idempotent or defensive (nullable checks on IDs)
- State outside `SagaContext` breaks resumability — context is serializable
- `Failed` state MUST trigger production alerts — never silently logged
- Orchestrator does NOT persist — consumer responsible via `ISagaStore`
- Steps record side-effect IDs on context so compensation can undo them
- `InMemorySagaStore` provided for testing only
