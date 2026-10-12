# SAGA-PATTERN — Philosophy

A saga is a sequence of steps that **must** either all complete or be **compensated** in reverse order. It is the primary mechanism for managing distributed transactions across services that don't share a database — and the secondary mechanism for any multi-step business process where partial completion would corrupt state.

## Why Sagas

Distributed transactions (two-phase commit) don't scale across service boundaries — they require all participants to hold locks until every participant agrees, and they fail catastrophically when one participant becomes unavailable. Most modern architectures rule them out.

The saga is the alternative. Instead of "all or nothing in one atomic operation", it says: "all steps run sequentially. If any step fails, the steps that already succeeded are **compensated** in reverse order to undo their effects."

```
Step 1: Reserve seat        → success
Step 2: Charge payment      → success
Step 3: Send confirmation   → FAILURE
                              ↓
Compensate 2: Refund payment → success
Compensate 1: Release seat   → success
```

The system ends in a consistent state — either fully complete or fully rolled back to the original state.

## Compensation Is Not A Rollback

A database transaction rollback erases the change. A saga compensation **applies a new operation** that semantically reverses the previous one:

- "Charge payment" is compensated by "issue refund", not by un-charging.
- "Reserve seat" is compensated by "release seat".
- "Send email" is compensated by "send retraction email" — or accepted as un-compensable.

Compensation steps are first-class operations. They have their own failure modes. They produce their own audit trail. The saga orchestrator must handle compensation failures explicitly — usually by raising an alert because manual intervention is required.

## Two Saga Styles: Orchestration And Choreography

- **Orchestration** — a central orchestrator calls each step in order and tracks state. The flow is explicit and centralized.
- **Choreography** — each service publishes events that the next service consumes. There is no central coordinator.

This skill describes the **orchestration** style. Orchestration is easier to reason about, easier to test, and produces a single place to look when debugging. Choreography is more decoupled but harder to debug because the saga's flow is implicit in the event subscriptions.

## Steps Have Two Operations: Execute And Compensate

```csharp
public interface ISagaStep<in TContext> where TContext : SagaContext
{
    Task<Result> ExecuteAsync(TContext context, CancellationToken ct = default);
    Task<Result> CompensateAsync(TContext context, CancellationToken ct = default);
}
```

Every step is an object with two methods. Both return `Result` so the orchestrator can compose failures without exception handling. The interface is **contravariant** on `TContext` so concrete steps can accept a derived context type.

## State Lives On The Context

`SagaContext` is the bag of state passed between steps:

- `SagaId` — unique identifier
- `State` — current `SagaState` enum value (`Pending`, `Running`, `Completed`, `Compensating`, `Compensated`, `Failed`)
- `CurrentStepIndex` — which step is currently executing
- `LastError` — message from the last failed step
- `StartedAt` / `CompletedAt`

Concrete sagas extend `SagaContext` to carry domain-specific state (order ID, payment ID, reservation ID, ...). The context is what you persist to resume a saga across process restarts.

## Sagas Are Persistable, Steps Are Not

The orchestrator runs in memory. The **state** of a running saga lives on the context, which can be serialized to JSON and stored via an `ISagaStore`. This enables:

- Resuming a saga after a process restart
- Inspecting in-flight sagas via SQL
- Distributing saga execution across multiple processes

Steps themselves are pure functions of the context. They have no internal state. This is what makes serializing and resuming a saga possible.

## State Machine

```
Pending → Running → Completed
                 → Compensating → Compensated
                                → Failed (compensation also failed)
```

`Failed` is the dangerous terminal state — it means a compensation failed and manual intervention is required. Production systems should alert on this state.

## Anchor Package

[`Net/FrenchExDev/Saga/`](../../../Net/FrenchExDev/Saga/) — `SagaOrchestrator<TContext>`, `ISagaStep<TContext>`, `SagaContext` base, `SagaState` enum, `ISagaStore` persistence interface, `InMemorySagaStore` test double.
