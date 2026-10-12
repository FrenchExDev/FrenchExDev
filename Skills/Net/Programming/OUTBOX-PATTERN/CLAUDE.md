# OUTBOX-PATTERN — Claude Context

Transactional outbox for domain events: events written as `OutboxMessage` rows in same DB transaction as business data. EF Core interceptor hooks `SavingChanges` pipeline for automatic capture. At-least-once delivery — consumers must be idempotent.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [DDD](../DDD/) — aggregates with `IHasDomainEvents`
- [ENTITY-DSL](../ENTITY-DSL/) — EF Core integration

## Related packages
- Outbox (no anchor — pattern specification)

## Notes for Claude
- Never dual-write: never call `broker.PublishAsync` and `dbContext.SaveChangesAsync` together outside a transaction
- Interceptor runs BEFORE save so messages share the transaction
- Events serialized with runtime type, not declared type (important for polymorphism)
- `AssemblyQualifiedName` required for type storage — not `FullName`
- At-least-once = consumers WILL receive same event twice (idempotency is hard requirement)
- Do NOT delete messages immediately after publish — keep until `RetentionPeriod`
- No processor provided — write your own `BackgroundService` polling the table
- `OutboxMessage.Id` can serve as deduplication key on consumer side
