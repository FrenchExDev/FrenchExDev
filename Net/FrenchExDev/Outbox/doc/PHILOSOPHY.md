# Outbox -- Philosophy

## The outbox pattern exists because distributed systems lie

When a service updates its database and publishes an event to a message broker, two things can go wrong:

1. The database commits but the broker publish fails -- the event is lost
2. The broker publish succeeds but the database transaction rolls back -- a phantom event was published

Both are real failures in production. Network partitions, broker outages, process crashes between the two operations -- these are not edge cases, they are Tuesday.

The outbox pattern eliminates this by making event publication part of the database transaction. The `OutboxMessage` is written in the same transaction as the business data. If the transaction commits, the message exists. If it rolls back, the message doesn't. A separate processor reads pending messages and publishes them to the broker, retrying on failure.

This trades "at-most-once" for "at-least-once" delivery. Consumers must be idempotent, but that's a solvable problem. Lost events are not.

---

## Interceptor over explicit calls

The `OutboxInterceptor` hooks into EF Core's `SavingChanges` pipeline. Entities raise domain events during business operations. The interceptor collects and serializes them. Developers don't call `outbox.StoreAsync()` manually.

This matters because:

- Manual storage is easy to forget -- a new `SaveChangesAsync` call site without an outbox store is a silent bug
- The interceptor guarantees coverage -- every `SaveChanges` on the `DbContext` automatically captures domain events
- The entity stays clean -- it raises events, it doesn't know about serialization or outbox tables

The trade-off is implicit behavior: you must know the interceptor exists. But the alternative -- sprinkling `outbox.StoreAsync()` after every `SaveChanges` call and hoping nobody misses one -- is worse.

`IOutbox.StoreAsync` still exists for non-EF scenarios (direct SQL, external APIs). The interceptor is the primary path; manual storage is the escape hatch.

---

## Core is netstandard2.0 because outbox is infrastructure

`IOutbox`, `IOutboxProcessor`, `OutboxMessage`, and `InMemoryOutbox` target `netstandard2.0`. This means they work with:

- .NET Framework 4.6.1+ (legacy services that still need outbox patterns)
- .NET 6/8/10 (modern services)
- Any .NET Standard 2.0-compatible runtime

The EF Core integration targets `net10.0` because it depends on modern EF Core APIs. But the core contracts and the test double are portable.

This is the same split as the core Outbox library: infrastructure contracts are broad, provider implementations are specific.

---

## AssemblyQualifiedName for type fidelity

The interceptor stores `event.GetType().AssemblyQualifiedName` as the message type. This is the most precise type identifier in .NET -- it includes namespace, class name, assembly, version, and culture.

Alternatives considered:

- **`FullName`** -- doesn't include the assembly, so deserialization can't resolve the type without a type map
- **Short name / custom string** -- requires a registry mapping names to types, which is another thing to maintain
- **Generic type parameter** -- makes `OutboxMessage` generic, complicating the database schema

`AssemblyQualifiedName` is verbose but self-describing. The processor can call `Type.GetType(message.Type)` and get the correct CLR type without any mapping. If assemblies are renamed or types moved, the processor fails loudly rather than silently deserializing to the wrong type.

The fallback chain (`AssemblyQualifiedName ?? FullName ?? Name`) handles edge cases where the runtime can't produce the full name.

---

## Retry tracking is on the message, not on the processor

`OutboxMessage` has `Attempts` and `LastError` properties. The processor increments `Attempts` on failure and records the error. This means:

- Retry state survives processor restarts (it's in the database, not in memory)
- Different processor instances see the same retry count (no double-retrying)
- Failed messages can be inspected via SQL (`SELECT * FROM OutboxMessages WHERE Attempts >= 3 AND ProcessedAt IS NULL`)
- `MaxRetryAttempts` in `OutboxProcessorOptions` is a query filter, not in-memory state

The processor is stateless. All state lives in the `OutboxMessages` table.

---

## InMemoryOutbox uses ConcurrentBag, not List

Unit tests may run in parallel. A service under test may call `StoreAsync` from multiple threads (e.g., when testing concurrent order processing). `ConcurrentBag<T>` is lock-free for adds and safe for enumeration via `ToArray()`.

A `List<T>` with a lock would work, but `ConcurrentBag` is simpler and communicates intent: this collection is designed for concurrent producers.

---

## Processor is not provided -- on purpose

The library defines `IOutboxProcessor` but does not provide an implementation. This is deliberate:

- The processor depends on the message broker (RabbitMQ, Kafka, Azure Service Bus, etc.)
- The processor depends on the deserialization strategy (System.Text.Json, Newtonsoft, Protobuf)
- The processor depends on the hosting model (BackgroundService, Hangfire, Azure Functions)

Providing a "default" processor would mean picking a broker, a serializer, and a host -- none of which are universal. The HOW-TO shows the pattern; the consumer fills in the blanks.

The core library's job is the write side (store atomically). The read side (process and dispatch) is inherently application-specific.
