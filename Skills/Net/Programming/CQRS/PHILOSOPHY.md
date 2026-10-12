# CQRS — Philosophy

## Lightweight CQRS

This codebase implements CQRS at the **interface level** — a clean separation of read and write paths. There is no event sourcing, no message bus, no eventual consistency infrastructure. Commands mutate state; queries read state. The separation is enforced by distinct handler interfaces.

## Two Handler Interfaces

```csharp
// Ddd/src/FrenchExDev.Net.Ddd/ICommandHandler.cs
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}

// Ddd/src/FrenchExDev.Net.Ddd/IQueryHandler.cs
public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
```

Each interface has exactly **one method** (`HandleAsync`). This is ISP applied to CQRS.

## Commands Linked to Aggregates

Commands declare their target aggregate via `[Command(AggregateRoot = "...")]`. This makes the relationship explicit and validatable:

```csharp
[Command("PlaceOrder")]
public class PlaceOrderCommand
{
    public UserId UserId { get; init; }
    public IReadOnlyList<OrderLineInput> Lines { get; init; }
}
```

Orphan commands (no aggregate) are a design smell — every mutation should target a specific aggregate.

## Events Raised, Not Stored

`IDomainEvent` carries `OccurredAt` for temporal ordering:

```csharp
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
```

Events are raised by command handlers **after persistence** — not stored in an event store. This is intentionally lightweight: events drive side effects (notifications, projections), not state reconstruction.

## VosOrchestrator as Command Pattern Exemplar

`VosOrchestrator.ExecuteAsync()` demonstrates the command pattern in practice:

```csharp
// Vos/src/FrenchExDev.Net.Vos/VosOrchestrator.cs
public async Task<List<(string Name, VosActionResult Result)>> ExecuteAsync(
    string? instanceName, bool all,
    Func<IVosBackend, ResolvedInstance, CancellationToken, Task<VosActionResult>> action,
    CancellationToken ct = default)
```

The `action` delegate is effectively a command — it accepts a backend and instance, performs a mutation, returns a result. Status queries use the same mechanism but the action is read-only.

`IVosBackend` itself separates mutations (`UpAsync`, `HaltAsync`, `DestroyAsync`, `ProvisionAsync`) from queries (`StatusAsync`, `SshAsync`).

See: `Vos/src/FrenchExDev.Net.Vos/VosOrchestrator.cs`, `Vos/src/FrenchExDev.Net.Vos/IVosBackend.cs`

## No Mediator

Handlers are injected directly — no MediatR, no message bus, no dispatcher. The dependency graph stays explicit and traceable. You can Ctrl+Click on a handler to see its implementation. No runtime registration, no convention-based discovery.

## Builders in the Command Flow

Command handlers use generated builders to construct aggregates:

```
Receive command
  --> Build aggregate via OrderBuilder (generated With*() methods)
  --> EnsureInvariants() (generated, calls all [Invariant] methods)
  --> If failed: return Result.Failure
  --> Persist via repository
  --> Raise OrderPlacedEvent : IDomainEvent
```

Builders validate input. Invariants validate domain rules. The command handler orchestrates the flow.
