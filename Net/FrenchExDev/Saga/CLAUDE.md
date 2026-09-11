# Saga — Claude Context

Orchestration-style saga pattern for managing multi-step processes with compensation. `SagaOrchestrator<TContext>` runs `ISagaStep` instances in order and, on failure, compensates the already-completed steps in reverse.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [SAGA-PATTERN](../../../Skills/Net/Programming/SAGA-PATTERN/PHILOSOPHY.md)
- [MEDIATOR-PATTERN](../../../Skills/Net/Programming/MEDIATOR-PATTERN/PHILOSOPHY.md)
- [RESULT-PATTERN](../../../Skills/Net/Programming/RESULT-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Saga.slnx`

## Notes for Claude
- README and `doc/*.md` files are currently empty stubs — derive intent from `src/FrenchExDev.Net.Saga/*.cs`.
- The orchestrator compensates **only the steps that already succeeded**. The failed step itself is NOT compensated. Compensation runs in REVERSE order from `i - 1`.
- Steps return `Task<Result>` (the non-generic Result), not `bool` and not `Task`. Never throw from a step — return `Result.Failure()`.
- `SagaState.Failed` (compensation also failed) requires manual intervention — production systems must alert on it.
- Persistence is the consumer's responsibility — the orchestrator does NOT call `ISagaStore` itself. `ISagaStore` and `SagaInstance` exist as a contract for consumers to implement.
- `SagaContext` IS the state. Never store mutable state outside the context — that breaks resumability.
- Concrete steps record their side-effect identifiers (e.g. `ReservationId`) on the context so their `CompensateAsync` knows what to undo.
- `ISagaStep<in TContext>` is contravariant so steps can accept derived context types.
