# Outbox — Claude Context

Transactional outbox pattern for reliable domain event publishing — stores events as `OutboxMessage` records atomically with the business transaction, processes them asynchronously. EF Core integration via `SaveChangesInterceptor` automatically captures domain events from `IHasDomainEvents` entities.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [OUTBOX-PATTERN](../../../Skills/Net/Programming/OUTBOX-PATTERN/PHILOSOPHY.md)
- [DDD](../../../Skills/Net/Programming/DDD/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Outbox.slnx`

## Notes for Claude
- Core targets `netstandard2.0 + net10.0` — works with .NET Framework. EF integration is `net10.0` only.
- The library does NOT provide an `IOutboxProcessor` implementation — that's consumer-specific (broker, serializer, host). Don't add one to the core library.
- `OutboxInterceptor` runs in `SavingChanges`/`SavingChangesAsync` BEFORE the actual save, so messages share the transaction. Never move it to a post-save hook.
- Type field stores `event.GetType().AssemblyQualifiedName` — never `FullName` or short names. Deserialization depends on this.
- `InMemoryOutbox` uses `ConcurrentBag<OutboxMessage>` (NOT `List<T>`) for parallel test safety — never replace.
- Retry state (`Attempts`, `LastError`) lives on the `OutboxMessage` row, not in processor memory — the processor is stateless.
- Consumers MUST be idempotent — at-least-once delivery is the cost of correctness.
- 14 tests covering `OutboxMessage` defaults and `InMemoryOutbox` thread safety.
