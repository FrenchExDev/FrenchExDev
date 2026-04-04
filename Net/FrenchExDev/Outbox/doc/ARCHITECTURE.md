# Outbox -- Architecture

## 1. Overview

Outbox implements the transactional outbox pattern: domain events are stored as `OutboxMessage` records in the same database transaction as the business operation, then processed asynchronously by an `IOutboxProcessor`. This guarantees that events are published if and only if the transaction commits. The EF Core integration uses a `SaveChangesInterceptor` to capture domain events automatically from tracked entities.

---

## 2. Project Structure

```
Outbox/
  FrenchExDev.Net.Outbox.slnx
  quality-gate.yml
  src/
    FrenchExDev.Net.Outbox/                          (Core — netstandard2.0 + net10.0)
      IOutbox.cs                                     Interface: StoreAsync
      IOutboxProcessor.cs                            Interface: ProcessPendingAsync
      OutboxMessage.cs                               Entity: Id, Type, Payload, CreatedAt, ProcessedAt, Attempts, LastError
      OutboxProcessorOptions.cs                      Config: PollingInterval, BatchSize, MaxRetryAttempts, RetentionPeriod
    FrenchExDev.Net.Outbox.EntityFramework/          (EF Core integration — net10.0)
      EfCoreOutbox.cs                                IOutbox impl: stores via DbContext
      OutboxInterceptor.cs                           SaveChangesInterceptor: captures domain events
      IHasDomainEvents.cs                            Interface for entities that raise events
      OutboxMessageConfiguration.cs                  IEntityTypeConfiguration: table mapping
    FrenchExDev.Net.Outbox.Testing/                  (Test helper — netstandard2.0 + net10.0)
      InMemoryOutbox.cs                              IOutbox impl: ConcurrentBag-backed
  test/
    FrenchExDev.Net.Outbox.Tests/                    (xUnit tests — net10.0)
      OutboxMessageTests.cs                          8 tests for OutboxMessage defaults
      InMemoryOutboxTests.cs                         6 tests for InMemoryOutbox
```

---

## 3. Dependency Graph

```
FrenchExDev.Net.Outbox                    (no dependencies, netstandard2.0 + net10.0)
  |
  +-- FrenchExDev.Net.Outbox.EntityFramework  (refs Outbox + EF Core + EF Core Relational)
  |
  +-- FrenchExDev.Net.Outbox.Testing          (refs Outbox, netstandard2.0 + net10.0)
  |
  +-- FrenchExDev.Net.Outbox.Tests            (refs Outbox + EntityFramework + Testing + xUnit)
```

The core library has zero NuGet dependencies. EF Core integration pulls in `Microsoft.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.Relational`.

---

## 4. Message Lifecycle

```
1. Entity raises event      order.Place() → _events.Add(new OrderPlaced(...))
2. SaveChanges called       dbContext.SaveChangesAsync()
3. Interceptor fires        OutboxInterceptor.SavingChangesAsync()
4. Events collected         ChangeTracker → IHasDomainEvents → DomainEvents
5. Serialized to JSON       JsonSerializer.Serialize(event, event.GetType())
6. OutboxMessage created    Id, Type (AssemblyQualifiedName), Payload, CreatedAt
7. Added to DbContext       context.Set<OutboxMessage>().AddRange(messages)
8. Events cleared           entity.ClearDomainEvents()
9. Transaction commits      Business data + OutboxMessages in same transaction
10. Processor polls         IOutboxProcessor.ProcessPendingAsync()
11. Messages dispatched     Deserialize, publish to broker, mark ProcessedAt
12. Retention cleanup       Delete messages older than RetentionPeriod
```

Steps 1-9 happen synchronously within `SaveChanges`. Steps 10-12 happen asynchronously (e.g., via `BackgroundService`).

---

## 5. Type Architecture

### Core (netstandard2.0)

| Type | Kind | Purpose |
|------|------|---------|
| `IOutbox` | interface | `StoreAsync(OutboxMessage, ct)` |
| `IOutboxProcessor` | interface | `ProcessPendingAsync(ct)` |
| `OutboxMessage` | class | Entity with 7 properties (Id, Type, Payload, CreatedAt, ProcessedAt, Attempts, LastError) |
| `OutboxProcessorOptions` | class | PollingInterval (5s), BatchSize (100), MaxRetryAttempts (3), RetentionPeriod (7d) |

### EF Core Integration (net10.0)

| Type | Kind | Purpose |
|------|------|---------|
| `EfCoreOutbox` | sealed class | `IOutbox` impl: `AddAsync` + `SaveChangesAsync` on `DbContext.Set<OutboxMessage>()` |
| `OutboxInterceptor` | sealed class | `SaveChangesInterceptor`: collects `IHasDomainEvents`, serializes to `OutboxMessage` |
| `IHasDomainEvents` | interface | `DomainEvents` + `ClearDomainEvents()` on entities |
| `OutboxMessageConfiguration` | sealed class | `IEntityTypeConfiguration<OutboxMessage>`: table `OutboxMessages`, Type max 512, LastError max 4000 |

### Testing (netstandard2.0)

| Type | Kind | Purpose |
|------|------|---------|
| `InMemoryOutbox` | sealed class | `IOutbox` impl: `ConcurrentBag<OutboxMessage>`, exposes `Messages` for assertions |

---

## 6. OutboxInterceptor Pipeline

The interceptor hooks both sync and async `SavingChanges`:

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
  → base.SavingChanges (proceeds with normal SaveChanges)
```

Because the interceptor runs before the actual save, the `OutboxMessage` records are included in the same database transaction. If the transaction rolls back, both the business data and the outbox messages are rolled back.

---

## 7. Database Schema

Table: `OutboxMessages`

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | uniqueidentifier | PK |
| `Type` | nvarchar(512) | NOT NULL |
| `Payload` | nvarchar(max) | NOT NULL |
| `CreatedAt` | datetimeoffset | NOT NULL |
| `ProcessedAt` | datetimeoffset | nullable |
| `Attempts` | int | NOT NULL |
| `LastError` | nvarchar(4000) | nullable |

Configured via `OutboxMessageConfiguration`. Apply with `modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration())` in `OnModelCreating`.

---

## 8. OutboxProcessorOptions Defaults

| Option | Default | Purpose |
|--------|---------|---------|
| `PollingInterval` | 5 seconds | Time between processing cycles |
| `BatchSize` | 100 | Max messages per processing batch |
| `MaxRetryAttempts` | 3 | Retries before marking a message as failed |
| `RetentionPeriod` | 7 days | How long processed messages are kept |
