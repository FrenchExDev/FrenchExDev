# OUTBOX-PATTERN — Architecture

## Project Layout

```
Outbox                       Core (netstandard2.0 + net10.0, no NuGet deps)
  IOutbox
  IOutboxProcessor
  OutboxMessage
  OutboxProcessorOptions

Outbox.EntityFramework       EF Core integration (net10.0)
  EfCoreOutbox
  OutboxInterceptor
  IHasDomainEvents
  OutboxMessageConfiguration

Outbox.Testing               Test double (netstandard2.0 + net10.0)
  InMemoryOutbox

Outbox.Tests                 xUnit (net10.0)
```

The core library is **portable** to .NET Framework. The ORM-specific integration is its own assembly so non-EF consumers don't pull in EF Core dependencies.

## Core Types

```csharp
public interface IOutbox
{
    Task StoreAsync(OutboxMessage message, CancellationToken ct = default);
}

public interface IOutboxProcessor
{
    Task ProcessPendingAsync(CancellationToken ct = default);
}

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;        // AssemblyQualifiedName
    public string Payload { get; set; } = string.Empty;     // JSON
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

public class OutboxProcessorOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int BatchSize { get; set; } = 100;
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}
```

## Message Lifecycle

```
1. Entity raises event           order.Place() → _events.Add(new OrderPlaced(...))
2. SaveChanges called            dbContext.SaveChangesAsync()
3. Interceptor fires             OutboxInterceptor.SavingChangesAsync()
4. Events collected              ChangeTracker → IHasDomainEvents → DomainEvents
5. Serialized to JSON            JsonSerializer.Serialize(event, event.GetType())
6. OutboxMessage created         Id, Type (AssemblyQualifiedName), Payload, CreatedAt
7. Added to DbContext            context.Set<OutboxMessage>().AddRange(messages)
8. Events cleared                entity.ClearDomainEvents()
9. Transaction commits           Business data + OutboxMessages in one transaction
10. Processor polls              IOutboxProcessor.ProcessPendingAsync()
11. Messages dispatched          Deserialize, publish to broker, mark ProcessedAt
12. Retention cleanup            Delete messages older than RetentionPeriod
```

Steps 1–9 happen synchronously within `SaveChanges` and share its transaction. Steps 10–12 happen asynchronously in a background processor.

## `IHasDomainEvents` Contract

```csharp
public interface IHasDomainEvents
{
    IReadOnlyList<object> DomainEvents { get; }
    void ClearDomainEvents();
}
```

Aggregates implement this interface. The interceptor inspects the EF Core `ChangeTracker` for entries whose entity implements `IHasDomainEvents` and has a non-empty event list.

## Interceptor Pipeline

```
SavingChanges / SavingChangesAsync
  → ConvertDomainEventsToOutboxMessages(context)
      1. Query ChangeTracker for entities implementing IHasDomainEvents
      2. Filter to entities with DomainEvents.Count > 0
      3. For each entity, for each domain event:
         a. Create OutboxMessage with:
            - Type = event.GetType().AssemblyQualifiedName
            - Payload = JsonSerializer.Serialize(event, event.GetType())
         b. Add to messages list
      4. entity.ClearDomainEvents()
      5. context.Set<OutboxMessage>().AddRange(messages)
  → base.SavingChanges (proceeds with the actual save)
```

Because the interceptor runs **before** the actual save, the `OutboxMessage` records are part of the same transaction. If the transaction rolls back, both the business data and the messages roll back together.

## Database Schema

| Column | Type | Constraints |
|---|---|---|
| `Id` | uniqueidentifier | PK |
| `Type` | nvarchar(512) | NOT NULL |
| `Payload` | nvarchar(max) | NOT NULL |
| `CreatedAt` | datetimeoffset | NOT NULL |
| `ProcessedAt` | datetimeoffset | nullable |
| `Attempts` | int | NOT NULL |
| `LastError` | nvarchar(4000) | nullable |

Configured via an `IEntityTypeConfiguration<OutboxMessage>` applied in `OnModelCreating`.

## In-Memory Test Double

`InMemoryOutbox` stores messages in a **`ConcurrentBag<OutboxMessage>`**, not a `List<T>`. Why: unit tests may run in parallel, and a service under test may call `StoreAsync` from multiple threads. `ConcurrentBag<T>` is lock-free for adds and safe for enumeration via `ToArray()`.

```csharp
public sealed class InMemoryOutbox : IOutbox
{
    private readonly ConcurrentBag<OutboxMessage> _messages = new();
    public IReadOnlyCollection<OutboxMessage> Messages => _messages.ToArray();

    public Task StoreAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }
}
```

## Anchor Package

[`Net/FrenchExDev/Outbox/`](../../../Net/FrenchExDev/Outbox/) — full implementation with EF Core integration.
