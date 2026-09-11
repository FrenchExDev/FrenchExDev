# SAGA-PATTERN — How-To

## 1. Define a Saga Context

Extend `SagaContext` with the domain state your steps need to share:

```csharp
public sealed class BookFlightContext : SagaContext
{
    public Guid CustomerId { get; set; }
    public string FlightNumber { get; set; } = "";

    // Populated by steps as they run
    public Guid? ReservationId { get; set; }
    public Guid? PaymentId { get; set; }
    public string? ConfirmationNumber { get; set; }
}
```

The optional properties (`ReservationId`, `PaymentId`) are how steps record what they did so their compensation step can undo it.

## 2. Define Each Step

```csharp
public sealed class ReserveSeatStep(ISeatService seats) : ISagaStep<BookFlightContext>
{
    public async Task<Result> ExecuteAsync(BookFlightContext ctx, CancellationToken ct)
    {
        var resId = await seats.ReserveAsync(ctx.FlightNumber, ctx.CustomerId, ct);
        if (resId is null) return Result.Failure();
        ctx.ReservationId = resId;
        return Result.Success();
    }

    public async Task<Result> CompensateAsync(BookFlightContext ctx, CancellationToken ct)
    {
        if (ctx.ReservationId.HasValue)
            await seats.ReleaseAsync(ctx.ReservationId.Value, ct);
        return Result.Success();
    }
}

public sealed class ChargePaymentStep(IPaymentGateway payments) : ISagaStep<BookFlightContext>
{
    public async Task<Result> ExecuteAsync(BookFlightContext ctx, CancellationToken ct)
    {
        var pid = await payments.ChargeAsync(ctx.CustomerId, 199.00m, ct);
        if (pid is null) return Result.Failure();
        ctx.PaymentId = pid;
        return Result.Success();
    }

    public async Task<Result> CompensateAsync(BookFlightContext ctx, CancellationToken ct)
    {
        if (ctx.PaymentId.HasValue)
            await payments.RefundAsync(ctx.PaymentId.Value, ct);
        return Result.Success();
    }
}

public sealed class SendConfirmationStep(IEmailService email) : ISagaStep<BookFlightContext>
{
    public async Task<Result> ExecuteAsync(BookFlightContext ctx, CancellationToken ct)
    {
        var conf = await email.SendBookingConfirmationAsync(ctx.CustomerId, ctx.FlightNumber, ct);
        ctx.ConfirmationNumber = conf;
        return Result.Success();
    }

    public Task<Result> CompensateAsync(BookFlightContext ctx, CancellationToken ct)
        => Task.FromResult(Result.Success());   // accepted as un-compensable
}
```

## 3. Wire The Orchestrator

```csharp
var orchestrator = new SagaOrchestrator<BookFlightContext>(new ISagaStep<BookFlightContext>[]
{
    new ReserveSeatStep(seats),
    new ChargePaymentStep(payments),
    new SendConfirmationStep(email)
});
```

The order in the array is the execution order. Compensation runs in reverse from the last successful step.

## 4. Execute The Saga

```csharp
var context = new BookFlightContext
{
    CustomerId = customerId,
    FlightNumber = "AF1234"
};

var result = await orchestrator.ExecuteAsync(context, ct);

if (result.IsSuccess)
{
    // context.State == Completed
    Console.WriteLine($"Booked: {context.ConfirmationNumber}");
}
else if (context.State == SagaState.Compensated)
{
    // Forward step failed, compensation succeeded — system is back to original state
    Console.WriteLine($"Booking failed and rolled back: {context.LastError}");
}
else if (context.State == SagaState.Failed)
{
    // Compensation also failed — manual intervention required
    logger.LogCritical("Saga {Id} requires manual intervention: {Error}", context.SagaId, context.LastError);
    await alertService.NotifyOpsAsync(context);
}
```

## 5. Persist The Saga

```csharp
var instance = new SagaInstance
{
    Id = context.SagaId,
    SagaType = nameof(BookFlightContext),
    ContextJson = JsonSerializer.Serialize(context),
    State = context.State,
    CurrentStepIndex = context.CurrentStepIndex,
    CreatedAt = context.StartedAt,
    CompletedAt = context.CompletedAt,
    LastError = context.LastError
};

await sagaStore.SaveAsync(instance, ct);
```

Persist after the orchestrator returns, or after each step if you need to resume across process restarts.

## 6. Resume A Saga

```csharp
var instance = await sagaStore.FindAsync(sagaId, ct);
if (instance is not null && instance.State == SagaState.Pending)
{
    var context = JsonSerializer.Deserialize<BookFlightContext>(instance.ContextJson)!;
    var result = await orchestrator.ExecuteAsync(context, ct);
    // ...persist again...
}
```

Resuming requires the orchestrator to know how to skip already-completed steps. Either store `CurrentStepIndex` and start there, or design steps to be idempotent so re-running them is safe.

## 7. Test With In-Memory Store

```csharp
[Fact]
public async Task BookFlight_PaymentFails_CompensatesReservation()
{
    var seats = new FakeSeatService();
    var payments = new FailingPaymentGateway();   // always returns null
    var email = new FakeEmailService();

    var orchestrator = new SagaOrchestrator<BookFlightContext>(new ISagaStep<BookFlightContext>[]
    {
        new ReserveSeatStep(seats),
        new ChargePaymentStep(payments),
        new SendConfirmationStep(email)
    });

    var ctx = new BookFlightContext { CustomerId = Guid.NewGuid(), FlightNumber = "AF1234" };
    var result = await orchestrator.ExecuteAsync(ctx);

    Assert.True(result.IsFailure);
    Assert.Equal(SagaState.Compensated, ctx.State);
    Assert.True(seats.WasReleased(ctx.ReservationId!.Value));   // compensation ran
    Assert.False(email.WasSent());                              // step 3 never ran
}
```

## What NOT To Do

- **Don't throw exceptions from steps.** Return `Result.Failure()`.
- **Don't put business validation in steps.** Validate before starting the saga; steps execute pre-validated commands.
- **Don't compensate the failed step itself** — only compensate steps that successfully completed.
- **Don't ignore the `Failed` state** — it requires manual intervention. Always alert on it.
- **Don't make compensation impossible.** If a step truly cannot be compensated (e.g. sending an email), document it explicitly and accept the limitation.
- **Don't share mutable state outside the context** — the context IS the state. Anything else breaks resumability.

## Anchor Package

[`Net/FrenchExDev/Saga/`](../../../Net/FrenchExDev/Saga/) — implementation reference.
