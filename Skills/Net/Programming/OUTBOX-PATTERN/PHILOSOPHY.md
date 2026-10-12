# OUTBOX-PATTERN — Philosophy

The transactional outbox pattern guarantees that domain events are published **if and only if** the database transaction commits. It eliminates the dual-write problem at the cost of trading at-most-once for at-least-once delivery.

## Distributed Systems Lie

When a service updates its database AND publishes an event to a message broker in the same operation, two things can go wrong:

1. The database commits but the broker publish fails — **the event is lost**.
2. The broker publish succeeds but the database transaction rolls back — **a phantom event was published**.

Both are real failures in production. Network partitions, broker outages, process crashes between the two operations — these are not edge cases, they are Tuesday.

The outbox pattern eliminates the dual-write entirely:

1. The domain event is written as an `OutboxMessage` row in the **same transaction** as the business data.
2. A separate processor reads pending messages and publishes them to the broker.
3. If the transaction commits, the message exists. If it rolls back, the message doesn't.
4. The processor retries on failure until the message is acknowledged.

This trades at-most-once for at-least-once delivery. Consumers must be **idempotent**, but that's a solvable problem. Lost events are not.

## Interceptor Over Explicit Calls

Domain entities raise events during business operations. The outbox interceptor hooks into the ORM's `SavingChanges` pipeline, collects pending events from tracked entities, serializes them, and adds `OutboxMessage` records to the same transaction. **Developers don't call `outbox.StoreAsync()` manually.**

This matters because:

- Manual storage is easy to forget — a new `SaveChangesAsync` call site without an outbox store is a silent bug.
- The interceptor guarantees coverage — every `SaveChanges` automatically captures domain events.
- The entity stays clean — it raises events; it doesn't know about serialization or outbox tables.

The trade-off is implicit behavior: you must know the interceptor exists. But the alternative — sprinkling `outbox.StoreAsync()` after every `SaveChanges` call and hoping nobody misses one — is worse.

`IOutbox.StoreAsync` still exists for non-ORM scenarios (direct SQL, external APIs). The interceptor is the primary path; manual storage is the escape hatch.

## Type Stored As `AssemblyQualifiedName`

Each `OutboxMessage` carries the event's full `AssemblyQualifiedName` as its type identifier. This is the most precise type identifier in .NET — namespace, class name, assembly, version, culture.

Alternatives considered:

- **`FullName`** — doesn't include the assembly; deserialization can't resolve the type without a registry.
- **Short name / custom string** — requires a registry mapping names to types, another thing to maintain.
- **Generic type parameter** — makes `OutboxMessage` generic, complicating the database schema.

`AssemblyQualifiedName` is verbose but self-describing. The processor calls `Type.GetType(message.Type)` and gets the correct CLR type without any mapping. If assemblies are renamed or types moved, the processor fails loudly rather than silently deserializing to the wrong type.

## Retry Tracking Is On The Message, Not The Processor

`OutboxMessage` carries `Attempts` and `LastError`. The processor increments `Attempts` on failure and records the error. This means:

- Retry state survives processor restarts (it's in the database, not memory).
- Different processor instances see the same retry count (no double-retrying).
- Failed messages can be inspected via SQL.
- `MaxRetryAttempts` is a query filter, not in-memory state.

**The processor is stateless. All state lives in the `OutboxMessages` table.**

## The Processor Is Not Provided — On Purpose

The library defines `IOutboxProcessor` but does **not** provide an implementation. Why:

- The processor depends on the message broker (RabbitMQ, Kafka, Azure Service Bus...).
- The processor depends on the deserialization strategy (System.Text.Json, Newtonsoft, Protobuf).
- The processor depends on the hosting model (BackgroundService, Hangfire, Azure Functions).

Providing a "default" processor would mean picking a broker, a serializer, and a host — none universal. The library's job is the **write side** (store atomically). The **read side** (process and dispatch) is inherently application-specific.

## Idempotent Consumers Are A Hard Requirement

At-least-once delivery means your event consumers WILL eventually receive the same event twice. They must be designed to handle this — usually by storing the message ID in a deduplication table or making the operation naturally idempotent.

This is not optional. It's the cost of using an outbox.

## Anchor Package

[`Net/FrenchExDev/Outbox/`](../../../Net/FrenchExDev/Outbox/) — `IOutbox`, `IOutboxProcessor`, `OutboxMessage`, EF Core integration with `OutboxInterceptor`, and an `InMemoryOutbox` test double.
