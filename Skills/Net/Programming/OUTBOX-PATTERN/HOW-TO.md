# OUTBOX-PATTERN — How-To

## 1. Implementing `IHasDomainEvents`

```csharp
public abstract class AggregateRoot : IHasDomainEvents
{
    private readonly List<object> _domainEvents = [];

    public IReadOnlyList<object> DomainEvents => _domainEvents;
    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void RaiseDomainEvent(object domainEvent) => _domainEvents.Add(domainEvent);
}
```

## 2. Raising Events From Entities

```csharp
public class Order : AggregateRoot
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }

    public void Place()
    {
        Status = OrderStatus.Placed;
        RaiseDomainEvent(new OrderPlaced(Id, DateTimeOffset.UtcNow));
    }

    public void Cancel(string reason)
    {
        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderCancelled(Id, reason, DateTimeOffset.UtcNow));
    }
}

public record OrderPlaced(Guid OrderId, DateTimeOffset PlacedAt);
public record OrderCancelled(Guid OrderId, string Reason, DateTimeOffset CancelledAt);
```

Events are plain records. They live in the same assembly as the entities.

## 3. Registering The Interceptor

```csharp
services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString)
           .AddInterceptors(new OutboxInterceptor());
});
```

The interceptor hooks both `SavingChanges` and `SavingChangesAsync`.

## 4. Mapping The OutboxMessage Entity

In `DbContext.OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    // ... other configurations
}
```

Then create the migration:

```bash
dotnet ef migrations add AddOutboxMessages
dotnet ef database update
```

## 5. The Application Code Stays Clean

```csharp
public sealed class OrderService(AppDbContext db)
{
    public async Task<Order> PlaceOrderAsync(Cart cart, CancellationToken ct)
    {
        var order = new Order();
        order.Place();   // raises OrderPlaced

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);   // interceptor captures the event automatically

        return order;
    }
}
```

No `outbox.StoreAsync()` call. The interceptor handles it.

## 6. Manual Storage (Non-ORM Scenarios)

For code paths that don't use EF Core, inject `IOutbox` directly:

```csharp
public sealed class PaymentService(IOutbox outbox)
{
    public async Task ProcessAsync(Payment payment, CancellationToken ct)
    {
        // ... business logic ...

        await outbox.StoreAsync(new OutboxMessage
        {
            Type = typeof(PaymentProcessed).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(new PaymentProcessed(payment.Id))
        }, ct);
    }
}
```

## 7. Implementing The Processor

The library does NOT provide a processor — write your own as a `BackgroundService`:

```csharp
public sealed class OutboxBackgroundProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxProcessorOptions> options,
    IMessageBroker broker,
    ILogger<OutboxBackgroundProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = await db.Set<OutboxMessage>()
                .Where(m => m.ProcessedAt == null && m.Attempts < options.Value.MaxRetryAttempts)
                .OrderBy(m => m.CreatedAt)
                .Take(options.Value.BatchSize)
                .ToListAsync(ct);

            foreach (var message in pending)
            {
                try
                {
                    var type = Type.GetType(message.Type)!;
                    var payload = JsonSerializer.Deserialize(message.Payload, type)!;
                    await broker.PublishAsync(payload, ct);
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    message.Attempts++;
                    message.LastError = ex.Message;
                    logger.LogWarning(ex, "Outbox publish failed for {Id}", message.Id);
                }
            }

            await db.SaveChangesAsync(ct);
            await Task.Delay(options.Value.PollingInterval, ct);
        }
    }
}
```

## 8. Testing With `InMemoryOutbox`

```csharp
[Fact]
public async Task PlaceOrder_StoresOutboxMessage()
{
    var outbox = new InMemoryOutbox();
    var service = new OrderService(outbox);

    await service.PlaceOrderAsync(cart);

    Assert.Single(outbox.Messages);
    Assert.Contains("OrderPlaced", outbox.Messages.First().Type);
}
```

`InMemoryOutbox.Messages` is thread-safe — parallel tests are fine.

## 9. Integration Test With In-Memory EF Core

```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("test-db")
    .AddInterceptors(new OutboxInterceptor())
    .Options;

using var context = new AppDbContext(options);
var order = new Order();
order.Place();

context.Orders.Add(order);
await context.SaveChangesAsync();

var outboxMessages = await context.Set<OutboxMessage>().ToListAsync();
Assert.Single(outboxMessages);
Assert.Contains("OrderPlaced", outboxMessages[0].Type);
```

This verifies the interceptor actually captured the event into the same transaction.

## 10. Make Consumers Idempotent

At-least-once delivery means consumers WILL receive the same event twice eventually. Always:

- Use the `OutboxMessage.Id` as a deduplication key on the consumer side, OR
- Make the operation naturally idempotent (e.g. "set status to Placed" is idempotent, "increment counter" is not).

## What NOT To Do

- **Don't dual-write** — never call `broker.PublishAsync` and `dbContext.SaveChangesAsync` in the same code path.
- **Don't skip the interceptor** — it's the cheapest correctness guarantee you have.
- **Don't catch exceptions in the interceptor** — let them propagate so the transaction rolls back.
- **Don't store events with `FullName` instead of `AssemblyQualifiedName`** — deserialization will silently fail.
- **Don't make consumers non-idempotent** — at-least-once delivery is the cost of correctness.

## Anchor Package

[`Net/FrenchExDev/Outbox/`](../../../Net/FrenchExDev/Outbox/) — implementation reference.
