# CQRS — Architecture

## Handler Interfaces

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

Both follow ISP: exactly one method each. `TResult` is typically `Result<T>` for commands (may fail) and a plain type for queries.

## Command/Query Split: IVosBackend

`IVosBackend` demonstrates the split concretely:

```
MUTATIONS (commands):
  UpAsync(instance)
  HaltAsync(instance, force?)
  DestroyAsync(instance, force?)
  ReloadAsync(instance)
  ProvisionAsync(instance)
  SuspendAsync(instance)
  ResumeAsync(instance)
  SnapshotSaveAsync(instance, name)
  SnapshotRestoreAsync(instance, name)

QUERIES (reads):
  StatusAsync(instance)
  SshAsync(instance)
  SshCommandAsync(instance, command)
```

See: `Vos/src/FrenchExDev.Net.Vos/IVosBackend.cs`

## Command Lifecycle

```
Client
  |
  v
PlaceOrderCommand { UserId, Lines }
  |
  v
PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, Result<OrderId>>
  |
  |-- 1. Build aggregate via OrderBuilder
  |       .WithUserId(command.UserId)
  |       .WithLines(command.Lines.Select(...))
  |       .BuildAsync()
  |
  |-- 2. EnsureInvariants() (generated)
  |       Calls all [Invariant] methods
  |       Returns Result.Combine(results)
  |
  |-- 3. If failed --> return Result.Failure
  |
  |-- 4. Persist via IOrderRepository
  |
  +-- 5. Raise OrderPlacedEvent : IDomainEvent
          { OrderId, OccurredAt = DateTimeOffset.UtcNow }
```

## VosOrchestrator (Command Pattern)

```
VosOrchestrator.ExecuteAsync(instanceName, all, action, ct)
  |
  |-- ResolveTargets(instanceName, all)  --> List<ResolvedInstance>
  |
  |-- foreach instance:
  |     action(backend, instance, ct)  --> VosActionResult
  |     results.Add((name, result))
  |
  +-- return results
```

The `action` parameter is a command delegate: `Func<IVosBackend, ResolvedInstance, CancellationToken, Task<VosActionResult>>`.

## Event Model

```csharp
// Ddd/src/FrenchExDev.Net.Ddd/IDomainEvent.cs
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

// Attribute for declaring events
[DomainEvent("OrderPlaced")]
public class OrderPlacedEvent : IDomainEvent
{
    public OrderId OrderId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
```

`[DomainEvent]` attribute has optional `SourceAggregate` property linking the event to its originating aggregate.

## Builder Integration

Builders participate in the command flow via the generated `BuildAsync()` pipeline:

```
CommandHandler.HandleAsync(command)
  --> new OrderBuilder()
        .WithX(command.X)
        .WithY(command.Y)
  --> builder.BuildAsync(ct)
        --> ValidateAsync()      (generated: calls virtual ValidateX() per property)
        --> Instantiate()        (generated: builds child builders, resolves references)
        --> CreateInstance()     (generated: strategy-driven: init/ctor/factory/custom)
  --> Result<Order>
```

See: `Builder/src/FrenchExDev.Net.Builder/Code.cs` (AbstractBuilder<T>)

## Key Files

- `Ddd/src/FrenchExDev.Net.Ddd/ICommandHandler.cs`
- `Ddd/src/FrenchExDev.Net.Ddd/IQueryHandler.cs`
- `Ddd/src/FrenchExDev.Net.Ddd/IDomainEvent.cs`
- `Ddd/src/FrenchExDev.Net.Ddd.Attributes/CommandAttribute.cs`
- `Ddd/src/FrenchExDev.Net.Ddd.Attributes/DomainEventAttribute.cs`
- `Vos/src/FrenchExDev.Net.Vos/VosOrchestrator.cs`
- `Vos/src/FrenchExDev.Net.Vos/IVosBackend.cs`
