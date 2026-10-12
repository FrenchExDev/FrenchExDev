# DDD — Requirements

Non-negotiable rules for DDD implementation.

## Aggregate Roots

- **Every `[AggregateRoot]` must have exactly one `[EntityId]` property.** Enforced by `MustHaveIdConstraint`. The source generator will fail if missing.
- **Aggregate roots are transaction boundaries.** Everything reachable via `[Composition]` is loaded and saved atomically. No partial aggregate loads.

## Attributes and MetaConcepts

- **Every DDD attribute must have a `[MetaConcept]` companion.** No naked attributes. The concept class carries validation rules (`CanContain`, constraints).
- **Concepts must implement `CanContain`.** Each MetaConcept declares what child concepts it accepts. `AggregateRootConcept` accepts `Entity` and `ValueObject`. `ValueObjectConcept` accepts only `ValueObject`.

## Relationships

- **`[Composition]` = inside aggregate boundary.** Cascade delete, owned lifecycle. Use only for entities/VOs within the same aggregate.
- **`[Association]` = cross-aggregate reference.** Nullable FK, eventual consistency. Never cascade delete across aggregates.
- **`[Aggregation]` = weak reference.** Nullable FK. Use for shared lifecycle where neither side owns the other.
- **Never use `[Composition]` across aggregate boundaries.** If two aggregate roots need to reference each other, use `[Association]`.

## Invariants

- **Invariants must return `Result`, never throw.** `[Invariant]` methods return `Result.Success()` or `Result.Failure("message")`. No exceptions for domain validation.
- **Multiple invariant failures are aggregated.** Generated `EnsureInvariants()` calls all `[Invariant]` methods and combines results via `Result.Combine()`. One failure does not short-circuit others.

## Construction

- **Builders construct domain objects.** Validation lives in the builder (`ValidateAsync`), not in entity constructors. Entities should be valid by construction.
- **Never construct aggregates directly** (via `new`). Always go through a generated builder or factory.
- **`Reference<T>` and `VisitedObjects` are hidden from developers.** These are internal to `AbstractBuilder<T>` for cycle detection in object graphs. User code never touches them.

## Result Type

- **`ValidationResult.MemberNames` is NEVER null.** The SDK returns `Enumerable.Empty<string>()`. Never null-check it.
- **Use `Result.Combine()` for aggregating multiple results.** Not manual if/else chains.

## Source Generation

- **`InvariantGenerator` follows the 2-step SG pattern.** Roslyn extraction in `.SourceGenerator`, emission in `.SourceGenerator.Lib` via `InvariantEmitter`. No Roslyn types in the emitter.
