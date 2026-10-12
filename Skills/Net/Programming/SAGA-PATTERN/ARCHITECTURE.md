# SAGA-PATTERN — Architecture

## Core Types

```csharp
public abstract class SagaContext
{
    public Guid SagaId { get; set; } = Guid.NewGuid();
    public SagaState State { get; set; } = SagaState.Pending;
    public int CurrentStepIndex { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public enum SagaState
{
    Pending,        // created, not started
    Running,        // executing forward steps
    Completed,      // all steps succeeded
    Compensating,   // a step failed; rolling back
    Compensated,    // compensation succeeded after a failure
    Failed          // compensation also failed (manual intervention needed)
}

public interface ISagaStep<in TContext> where TContext : SagaContext
{
    Task<Result> ExecuteAsync(TContext context, CancellationToken ct = default);
    Task<Result> CompensateAsync(TContext context, CancellationToken ct = default);
}

public sealed class SagaOrchestrator<TContext> where TContext : SagaContext
{
    public SagaOrchestrator(IReadOnlyList<ISagaStep<TContext>> steps);
    public Task<Result> ExecuteAsync(TContext context, CancellationToken ct = default);
}
```

## Orchestrator Algorithm

```
ExecuteAsync(context):
    context.State = Running
    context.StartedAt = UtcNow

    if no steps:
        context.State = Completed
        return Success

    for i in 0..steps.Count - 1:
        context.CurrentStepIndex = i
        result = await steps[i].ExecuteAsync(context, ct)

        if result.IsFailure:
            context.LastError = "Step {i} failed"
            context.State = Compensating
            compResult = await CompensateAsync(context, fromIndex: i - 1, ct)

            if compResult.IsFailure:
                context.State = Failed     // alert! manual intervention
            else:
                context.State = Compensated
            context.CompletedAt = UtcNow
            return Failure

    context.State = Completed
    context.CompletedAt = UtcNow
    return Success

CompensateAsync(context, fromIndex, ct):
    for i in fromIndex..0 (reverse):
        context.CurrentStepIndex = i
        result = await steps[i].CompensateAsync(context, ct)
        if result.IsFailure:
            context.LastError = "Compensation of step {i} failed"
            return Failure
    return Success
```

Key invariants:

- Forward steps run **in order** (0, 1, 2, ..., N).
- Compensation runs in **reverse order** (i-1, i-2, ..., 0) starting from the **last successful** step.
- The failed step itself is NOT compensated (it didn't successfully execute).
- If compensation of step `j` fails, compensation of steps `0..j-1` is also abandoned and the saga ends in `Failed`.

## Persistence

```csharp
public interface ISagaStore
{
    Task SaveAsync(SagaInstance instance, CancellationToken ct = default);
    Task<SagaInstance?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SagaInstance>> FindPendingAsync(CancellationToken ct = default);
    Task UpdateAsync(SagaInstance instance, CancellationToken ct = default);
}

public sealed class SagaInstance
{
    public Guid Id { get; set; }
    public string SagaType { get; set; } = string.Empty;   // routing key
    public string ContextJson { get; set; } = string.Empty;
    public SagaState State { get; set; }
    public int CurrentStepIndex { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastError { get; set; }
}
```

The orchestrator does **not** call the store. Persistence is the consumer's responsibility — either after each step or after the saga completes. This keeps the orchestrator pure and testable.

## Project Layout

```
Saga                    netstandard2.0 + net10.0 — refs Result
  SagaContext, SagaState, ISagaStep, SagaOrchestrator<TContext>,
  SagaInstance, ISagaStore

Saga.Testing            netstandard2.0 + net10.0 — refs Saga
  InMemorySagaStore

Saga.Tests              net10.0 — refs Saga + Testing + xUnit
```

## Domain Saga Example

```csharp
public sealed class BookFlightContext : SagaContext
{
    public Guid CustomerId { get; set; }
    public string FlightNumber { get; set; } = "";
    public Guid? ReservationId { get; set; }   // set by step 1
    public Guid? PaymentId { get; set; }        // set by step 2
}

public sealed class ReserveSeatStep : ISagaStep<BookFlightContext>
{
    public async Task<Result> ExecuteAsync(BookFlightContext ctx, CancellationToken ct)
    {
        var resId = await _seatService.ReserveAsync(ctx.FlightNumber, ctx.CustomerId, ct);
        if (resId is null) return Result.Failure();
        ctx.ReservationId = resId;
        return Result.Success();
    }

    public async Task<Result> CompensateAsync(BookFlightContext ctx, CancellationToken ct)
    {
        if (ctx.ReservationId.HasValue)
            await _seatService.ReleaseAsync(ctx.ReservationId.Value, ct);
        return Result.Success();
    }
}
```

Steps record their side-effect identifiers (`ReservationId`, `PaymentId`) on the context so the corresponding compensation step knows what to undo.

## Result Coupling

Steps return `Result`, not `bool` or `void`. This means:

- The orchestrator can distinguish "succeeded" from "did nothing" cleanly.
- Failures carry their own error metadata.
- The orchestrator composes step failures without exception handling.

## Anchor Package

[`Net/FrenchExDev/Saga/`](../../../Net/FrenchExDev/Saga/) — full implementation.
