# SAGA-PATTERN — Requirements

## Core Types

- [ ] `SagaContext` abstract base with `SagaId`, `State`, `CurrentStepIndex`, `LastError`, `StartedAt`, `CompletedAt`.
- [ ] `SagaState` enum with `Pending`, `Running`, `Completed`, `Compensating`, `Compensated`, `Failed`.
- [ ] `ISagaStep<in TContext> where TContext : SagaContext` with `ExecuteAsync` and `CompensateAsync`, both returning `Task<Result>`.
- [ ] `SagaOrchestrator<TContext>` taking `IReadOnlyList<ISagaStep<TContext>>` in the constructor.
- [ ] `ISagaStore` with `SaveAsync`, `FindAsync`, `FindPendingAsync`, `UpdateAsync`.
- [ ] `SagaInstance` persistence entity with `Id`, `SagaType`, `ContextJson`, `State`, `CurrentStepIndex`, `CreatedAt`, `CompletedAt`, `LastError`.

## Orchestrator Behavior

- [ ] Forward steps run **in order** (0, 1, 2, ..., N-1).
- [ ] On failure, compensation runs in **reverse order** starting from `i - 1` (NOT `i`).
- [ ] Failed step is NOT compensated.
- [ ] If compensation succeeds, end state is `Compensated` and orchestrator returns `Failure`.
- [ ] If compensation fails, end state is `Failed` and orchestrator returns `Failure`. No further compensation runs.
- [ ] Empty step list → immediately `Completed` with `Success`.
- [ ] `context.State`, `CurrentStepIndex`, `StartedAt`, `CompletedAt`, `LastError` are all updated by the orchestrator.

## Step Contract

- [ ] Steps return `Task<Result>` (not `bool`, not `void`, not `Task`).
- [ ] Steps MUST NOT throw — failures are returned as `Result.Failure()`.
- [ ] `ISagaStep<in TContext>` is contravariant on the context type.
- [ ] Compensation MUST be safe to call when forward step never executed (defensive nullable checks on side-effect identifiers).

## State Machine

- [ ] `Pending → Running` on first call.
- [ ] `Running → Completed` when all steps succeed.
- [ ] `Running → Compensating` on first failure.
- [ ] `Compensating → Compensated` if all compensations succeed.
- [ ] `Compensating → Failed` if any compensation fails.

## Persistence Separation

- [ ] The orchestrator does NOT call `ISagaStore` itself. Persistence is the consumer's responsibility.
- [ ] `SagaContext` is JSON-serializable so it can be stored as `ContextJson` on `SagaInstance`.

## Test Double

- [ ] `InMemorySagaStore` in a separate `.Testing` project.
- [ ] Backed by a thread-safe collection (e.g. `ConcurrentDictionary<Guid, SagaInstance>`).

## Result Coupling

- [ ] The library depends on a `Result` type so steps and the orchestrator can compose failures without exceptions.

## What MUST NOT Be Done

- [ ] Steps MUST NOT throw exceptions for expected failures.
- [ ] Orchestrator MUST NOT swallow exceptions from `ExecuteAsync` / `CompensateAsync` — let them propagate (they indicate bugs, not domain failures).
- [ ] Compensation MUST NOT compensate the failed step itself.
- [ ] State outside `SagaContext` MUST NOT exist — context is the only mutable state.
- [ ] `Failed` state MUST trigger an alert in production — never silently logged.

## Multi-Targeting

- [ ] Core targets `netstandard2.0 + net10.0`.
- [ ] Test double targets `netstandard2.0 + net10.0`.

## Anchor Package

[`Net/FrenchExDev/Saga/`](../../../Net/FrenchExDev/Saga/) — implements every requirement.
