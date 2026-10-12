# OUTBOX-PATTERN — Requirements

## Core Contracts

- [ ] `IOutbox` with `StoreAsync(OutboxMessage, CancellationToken)`.
- [ ] `IOutboxProcessor` with `ProcessPendingAsync(CancellationToken)`.
- [ ] `OutboxMessage` entity with `Id` (Guid), `Type` (string), `Payload` (string), `CreatedAt` (DateTimeOffset), `ProcessedAt` (DateTimeOffset?), `Attempts` (int), `LastError` (string?).
- [ ] `OutboxProcessorOptions` with `PollingInterval`, `BatchSize`, `MaxRetryAttempts`, `RetentionPeriod`.

## Project Layout

- [ ] Core (`netstandard2.0 + net10.0`) with **zero NuGet dependencies**.
- [ ] ORM-specific integration (e.g. `Outbox.EntityFramework`) in a separate project.
- [ ] Test double (`InMemoryOutbox`) in a separate `.Testing` project.

## Interceptor Requirements

- [ ] Hooks both `SavingChanges` AND `SavingChangesAsync`.
- [ ] Runs **before** the actual save so messages are part of the same transaction.
- [ ] Inspects the `ChangeTracker` for entities implementing `IHasDomainEvents`.
- [ ] Filters to entities with `DomainEvents.Count > 0`.
- [ ] Serializes each event to JSON with its **runtime type** (`event.GetType()`), not the declared type.
- [ ] Stores `event.GetType().AssemblyQualifiedName` as the message Type.
- [ ] Calls `entity.ClearDomainEvents()` after serialization.
- [ ] Adds messages via `context.Set<OutboxMessage>().AddRange(messages)` so they share the transaction.

## `IHasDomainEvents` Contract

- [ ] `IReadOnlyList<object> DomainEvents { get; }`.
- [ ] `void ClearDomainEvents()`.
- [ ] Implementing it on an aggregate base class is the recommended pattern.

## Type Identification

- [ ] Always store `AssemblyQualifiedName`, never `FullName` or short names.
- [ ] Processor reconstructs types via `Type.GetType(message.Type)`.

## Retry Tracking

- [ ] State lives on the `OutboxMessage`, not in processor memory.
- [ ] `Attempts` increments on failure.
- [ ] `LastError` records the most recent exception message.
- [ ] `MaxRetryAttempts` is a query filter (`m.Attempts < options.MaxRetryAttempts`), not in-memory state.

## Schema

- [ ] Table name: `OutboxMessages`.
- [ ] `Type` column max length 512.
- [ ] `LastError` column max length 4000 (or unlimited if the DB supports it).
- [ ] `Payload` column unbounded (`nvarchar(max)` or equivalent).

## In-Memory Test Double

- [ ] Backed by `ConcurrentBag<OutboxMessage>` (NOT `List<T>`) — required for parallel test safety.
- [ ] Exposes a `Messages` property that returns a snapshot via `ToArray()`.

## What MUST NOT Be Done

- [ ] No "dual write" code paths — never call `broker.PublishAsync` and `dbContext.SaveChangesAsync` in the same method.
- [ ] No exception swallowing in the interceptor — exceptions must propagate so the transaction rolls back.
- [ ] No bundled processor implementation — that's consumer-specific (broker, serializer, host).
- [ ] No assumptions about consumer idempotency — document it as a hard requirement.
- [ ] No "delete after publish" — keep messages until `RetentionPeriod` so failures can be inspected.

## Multi-Targeting

- [ ] Core targets `netstandard2.0 + net10.0`.
- [ ] EF integration targets `net10.0` (depends on modern EF Core).
- [ ] Testing targets `netstandard2.0 + net10.0`.

## Anchor Package

[`Net/FrenchExDev/Outbox/`](../../../Net/FrenchExDev/Outbox/) — implements every requirement.
