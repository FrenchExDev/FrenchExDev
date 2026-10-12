# CQRS — How-To

## Defining a New Command

```csharp
[Command("PlaceOrder", AggregateRoot = "Order")]
public class PlaceOrderCommand
{
    public UserId UserId { get; init; }
    public IReadOnlyList<OrderLineInput> Lines { get; init; } = [];
}
```

- Use `[Command]` with a name and target aggregate
- Command properties are the input data (use domain concept types where applicable)
- Commands are plain data classes — no logic

## Implementing a Command Handler

```csharp
public class PlaceOrderCommandHandler
    : ICommandHandler<PlaceOrderCommand, Result<OrderId>>
{
    private readonly IOrderRepository _repository;

    public PlaceOrderCommandHandler(IOrderRepository repository)
        => _repository = repository;

    public async Task<Result<OrderId>> HandleAsync(
        PlaceOrderCommand command, CancellationToken ct = default)
    {
        // 1. Build aggregate via generated builder
        var result = await new OrderBuilder()
            .WithUserId(command.UserId)
            .WithLines(command.Lines.Select(l =>
                new OrderLineBuilder()
                    .WithProductId(l.ProductId)
                    .WithQuantity(l.Quantity)))
            .BuildAsync(ct);

        if (result.IsFailure)
            return Result<OrderId>.Failure(result.ValidationResults);

        var order = result.Value;

        // 2. Persist
        await _repository.SaveAsync(order, ct);

        // 3. Raise event (after persistence)
        // order.RaiseEvent(new OrderPlacedEvent { ... });

        return Result<OrderId>.Success(order.Id);
    }
}
```

## Implementing a Query Handler

```csharp
public class GetOrderByIdQueryHandler
    : IQueryHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly IOrderReadRepository _readRepo;

    public GetOrderByIdQueryHandler(IOrderReadRepository readRepo)
        => _readRepo = readRepo;

    public async Task<OrderDto?> HandleAsync(
        GetOrderByIdQuery query, CancellationToken ct = default)
    {
        return await _readRepo.FindByIdAsync(query.OrderId, ct);
    }
}
```

Query handlers are read-only. They return data, not `Result<T>` (unless there's a genuine failure mode).

## Defining a Domain Event

```csharp
[DomainEvent("OrderPlaced", SourceAggregate = "Order")]
public class OrderPlacedEvent : IDomainEvent
{
    public OrderId OrderId { get; init; }
    public UserId UserId { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
```

- Implement `IDomainEvent` (requires `OccurredAt`)
- Use `[DomainEvent]` with name and source aggregate
- Raise after successful persistence, not before

## Following the VosOrchestrator Pattern

When you need a command orchestrator that delegates to a backend:

```csharp
public class MyOrchestrator
{
    private readonly IMyBackend _backend;

    public MyOrchestrator(IMyBackend backend) => _backend = backend;

    public async Task<List<(string Name, MyResult Result)>> ExecuteAsync(
        string? targetName,
        bool all,
        Func<IMyBackend, ResolvedTarget, CancellationToken, Task<MyResult>> action,
        CancellationToken ct = default)
    {
        var targets = ResolveTargets(targetName, all);
        var results = new List<(string, MyResult)>();

        foreach (var target in targets)
        {
            ct.ThrowIfCancellationRequested();
            var result = await action(_backend, target, ct);
            results.Add((target.Name, result));
        }

        return results;
    }
}
```

Key patterns:
- Accept the action as a delegate (command pattern)
- Resolve targets first (target resolution is separate from execution)
- Return results per target (not a single aggregate result)
- Support cancellation

Reference: `Vos/src/FrenchExDev.Net.Vos/VosOrchestrator.cs`
