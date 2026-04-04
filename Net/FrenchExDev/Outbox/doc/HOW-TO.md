# Outbox -- Developer Guide (HOW-TO)

## Table of Contents

1. [Implementing IHasDomainEvents](#1-implementing-ihasdomainevents)
2. [Registering the Interceptor](#2-registering-the-interceptor)
3. [Configuring the Entity](#3-configuring-the-entity)
4. [Raising Domain Events](#4-raising-domain-events)
5. [Manual Outbox Storage](#5-manual-outbox-storage)
6. [Processor Options](#6-processor-options)
7. [Implementing a Processor](#7-implementing-a-processor)
8. [Testing with InMemoryOutbox](#8-testing-with-inmemoryoutbox)
9. [Testing Interceptor Behavior](#9-testing-interceptor-behavior)
10. [Running Tests](#10-running-tests)

---

## 1. Implementing IHasDomainEvents

Entities that raise domain events implement `IHasDomainEvents`:

```csharp
public abstract class AggregateRoot : IHasDomainEvents
{
    private readonly List<object> _domainEvents = [];

    public IReadOnlyList<object> DomainEvents => _domainEvents;
    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void RaiseDomainEvent(object domainEvent) => _domainEvents.Add(domainEvent);
}
```

---

## 2. Registering the Interceptor

```csharp
services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString)
           .AddInterceptors(new OutboxInterceptor());
});
```

The interceptor hooks both `SavingChanges` and `SavingChangesAsync`. Domain events are captured and serialized to `OutboxMessage` records before the actual save -- they participate in the same transaction.

---

## 3. Configuring the Entity

In your `DbContext.OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    // ... other configurations
}
```

This maps `OutboxMessage` to the `OutboxMessages` table with appropriate column types and lengths.

### Migration

```bash
dotnet ef migrations add AddOutboxMessages
dotnet ef database update
```

---

## 4. Raising Domain Events

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

// Domain events are plain records
public record OrderPlaced(Guid OrderId, DateTimeOffset PlacedAt);
public record OrderCancelled(Guid OrderId, string Reason, DateTimeOffset CancelledAt);
```

When `SaveChangesAsync` is called, the interceptor automatically:
1. Finds all tracked entities with pending domain events
2. Serializes each event to JSON with its `AssemblyQualifiedName`
3. Creates `OutboxMessage` records
4. Clears the domain events from the entities

---

## 5. Manual Outbox Storage

For scenarios where the interceptor isn't used (e.g., non-EF code), inject `IOutbox` directly:

```csharp
public class PaymentService(IOutbox outbox)
{
    public async Task ProcessAsync(Payment payment, CancellationToken ct)
    {
        // Business logic...

        await outbox.StoreAsync(new OutboxMessage
        {
            Type = typeof(PaymentProcessed).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(new PaymentProcessed(payment.Id))
        }, ct);
    }
}
```

---

## 6. Processor Options

Configure via `OutboxProcessorOptions`:

```csharp
services.Configure<OutboxProcessorOptions>(options =>
{
    options.PollingInterval = TimeSpan.FromSeconds(10);
    options.BatchSize = 50;
    options.MaxRetryAttempts = 5;
    options.RetentionPeriod = TimeSpan.FromDays(14);
});
```

| Option | Default | Purpose |
|--------|---------|---------|
| `PollingInterval` | 5s | How often to check for pending messages |
| `BatchSize` | 100 | Max messages per processing cycle |
| `MaxRetryAttempts` | 3 | Retries before giving up on a message |
| `RetentionPeriod` | 7d | Cleanup window for processed messages |

---

## 7. Implementing a Processor

`IOutboxProcessor` defines the contract. A typical implementation as a `BackgroundService`:

```csharp
public class OutboxBackgroundProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxProcessorOptions> options,
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
                    // Publish to broker...
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    message.Attempts++;
                    message.LastError = ex.Message;
                }
            }

            await db.SaveChangesAsync(ct);
            await Task.Delay(options.Value.PollingInterval, ct);
        }
    }
}
```

---

## 8. Testing with InMemoryOutbox

`InMemoryOutbox` stores messages in a `ConcurrentBag` and exposes them via `Messages`:

```csharp
[Fact]
public async Task PlaceOrder_stores_outbox_message()
{
    var outbox = new InMemoryOutbox();
    var service = new OrderService(outbox);

    await service.PlaceOrderAsync(cart);

    Assert.Single(outbox.Messages);
    Assert.Equal("OrderPlaced", outbox.Messages.First().Type);
}
```

### Thread-safety

`InMemoryOutbox` is safe for parallel tests:

```csharp
var tasks = Enumerable.Range(0, 100)
    .Select(i => outbox.StoreAsync(new OutboxMessage { Type = $"Event{i}" }));

await Task.WhenAll(tasks);
Assert.Equal(100, outbox.Messages.Count);
```

---

## 9. Testing Interceptor Behavior

For integration tests verifying the interceptor, use an in-memory EF Core database:

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

---

## 10. Running Tests

```bash
dotnet test Outbox/FrenchExDev.Net.Outbox.slnx

# With quality gate
dotnet quality-gate test --config Outbox/quality-gate.yml
```

Test suite: 14 xUnit tests covering `OutboxMessage` defaults (8 tests) and `InMemoryOutbox` behavior (6 tests).
