# Outbox

Transactional outbox pattern implementation for reliable domain event publishing. Stores domain events as `OutboxMessage` records atomically with the business transaction, then processes them asynchronously. Includes an EF Core integration with a `SaveChangesInterceptor` that automatically captures domain events from tracked entities, and an in-memory implementation for testing.

## Quick Start

```csharp
// 1. Entities raise domain events
public class Order : IHasDomainEvents
{
    private readonly List<object> _events = [];
    public IReadOnlyList<object> DomainEvents => _events;
    public void ClearDomainEvents() => _events.Clear();

    public void Place()
    {
        Status = OrderStatus.Placed;
        _events.Add(new OrderPlaced(Id, DateTimeOffset.UtcNow));
    }
}

// 2. Register the interceptor — events are captured on SaveChanges
services.AddDbContext<AppDbContext>((sp, options) =>
    options.AddInterceptors(new OutboxInterceptor()));

// 3. Events flow: entity → interceptor → OutboxMessages table → processor → message broker
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Outbox` | netstandard2.0; net10.0 | `IOutbox`, `IOutboxProcessor`, `OutboxMessage`, `OutboxProcessorOptions` |
| `Outbox.EntityFramework` | net10.0 | `EfCoreOutbox`, `OutboxInterceptor`, `IHasDomainEvents`, EF entity config |
| `Outbox.Testing` | netstandard2.0; net10.0 | `InMemoryOutbox` with thread-safe `ConcurrentBag` storage |
| `Outbox.Tests` | net10.0 | 14 xUnit tests |

## Key Design Decisions

- **Interceptor-based capture** -- `OutboxInterceptor` hooks into `SavingChanges` to collect domain events from entities implementing `IHasDomainEvents`, serializes them to JSON, and adds `OutboxMessage` records to the same transaction
- **Core is netstandard2.0** -- `IOutbox`, `OutboxMessage`, and `InMemoryOutbox` work with .NET Framework, .NET 6+, and .NET 10
- **Type stored as `AssemblyQualifiedName`** -- enables deserialization back to the correct CLR type at processing time
- **Retry tracking built in** -- `Attempts` and `LastError` on `OutboxMessage` support retry logic in the processor
- **`InMemoryOutbox` is thread-safe** -- backed by `ConcurrentBag<T>` for parallel test scenarios

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project structure, message lifecycle, interceptor pipeline, entity configuration
- [HOW-TO.md](doc/HOW-TO.md) -- EF Core setup, domain event capture, processing, testing patterns
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why outbox pattern, why interceptor, why netstandard2.0

## Building

```bash
dotnet build Outbox/FrenchExDev.Net.Outbox.slnx
dotnet test Outbox/FrenchExDev.Net.Outbox.slnx
```
