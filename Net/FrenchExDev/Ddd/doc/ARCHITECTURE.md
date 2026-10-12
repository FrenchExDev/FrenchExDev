# Architecture — FrenchExDev.Net.Ddd

## Overview

The DDD DSL translates Domain-Driven Design patterns into C# attributes. Developers annotate partial classes with `[AggregateRoot]`, `[Entity]`, `[ValueObject]`, `[Composition]`, `[Invariant]`, etc. A Roslyn source generator validates the model at compile time and produces entity implementations, builders, EF Core configurations, repositories, and CQRS handlers.

## How It Fits in the M3 Hierarchy

```
M3 (Dsl)       [MetaConcept(typeof(AggregateRootConcept))]
                defines what "AggregateRoot" means as a modeling concept
                    │
M2 (Ddd)       public sealed class AggregateRootAttribute : Attribute
                the DDD DSL attribute — applied by developers
                    │
M1 (App)       [AggregateRoot("Order")] public partial class Order
                the developer's domain model
                    │
M0 (Runtime)   order.Lines.Count = 3, order.Total = 1500
                live data at runtime
```

The DDD DSL is an M2 layer. Its attributes are decorated with M3 primitives from `FrenchExDev.Net.Dsl`, making them self-describing and discoverable by the MetamodelRegistry.

## Attribute Categories

The 13 attributes fall into three categories based on what they annotate:

```
Class-level                  Property-level               Method-level
───────────                  ──────────────               ────────────
[AggregateRoot]              [EntityId]                   [Invariant]
[Entity]                     [Property]
[ValueObject]                [ValueComponent]
[Command]                    [Composition]
[DomainEvent]                [Association]
[BoundedContext]             [Aggregation]
```

## Aggregate Boundary Model

The central DDD concept: everything reachable via `[Composition]` from an `[AggregateRoot]` belongs to that aggregate. Cross-aggregate references use `[Association]`.

```
┌───────────────────────────────────────────────────┐
│             Order Aggregate (transaction boundary)│
│                                                   │
│  ┌───────────────┐                                │
│  │ Order          │ [AggregateRoot]               │
│  │  Id: OrderId   │ [EntityId]                    │
│  │  Status        │ [Property]                    │
│  └───┬──────┬─────┘                               │
│      │      │                                     │
│  [Composition]  [Composition]                     │
│      │      │                                     │
│  ┌───▼──────┐  ┌──────────────────┐               │
│  │OrderLine │  │ShippingAddress   │               │
│  │[Entity]  │  │[ValueObject]     │               │
│  │          │  │Street, City, Zip │               │
│  │ ┌──────┐ │  └──────────────────┘               │
│  │ │Money │ │                                     │
│  │ │[VO]  │ │                                     │
│  │ └──────┘ │                                     │
│  └──────────┘                                     │
└───────────────────────────────────────────────────┘
         │
    [Association]  (cross-aggregate, no cascade)
         │
    ┌────▼─────┐
    │ Customer │ [AggregateRoot] (different aggregate)
    └──────────┘
```

## Companion Classes

Each attribute has a companion `MetaConcept` subclass in the `Concepts/` folder. Most are minimal (just `Name` and `AttributeType`). `AggregateRootConcept` is the richest:

```csharp
public sealed class AggregateRootConcept : MetaConcept
{
    public override string Name => "AggregateRoot";
    public override Type AttributeType => typeof(AggregateRootAttribute);

    // Metamodel inheritance: AggregateRoot IS-A Entity
    public override IReadOnlyList<Type> SuperTypes => new[] { typeof(EntityConcept) };

    // Containment: only entities and value objects inside an aggregate
    public override bool CanContain(MetaConcept child)
        => child is EntityConcept || child is ValueObjectConcept;
}
```

This enforces DDD rules at the metamodel level:
- An aggregate can contain entities and value objects (composition)
- An aggregate cannot contain commands or domain events (those are separate concerns)
- AggregateRoot inherits all Entity semantics (it IS an entity)

## Constraint Validation

`AggregateRootAttribute` declares a compile-time constraint via `[MetaConstraint]`:

```
[MetaConstraint("MustHaveId", nameof(MustHaveIdConstraint),
    Message = "Aggregate root must have an [EntityId] property")]
```

The constraint is a static method receiving a `ConceptValidationContext`:

```csharp
public static ConstraintResult MustHaveIdConstraint(ConceptValidationContext ctx)
{
    // Iterates all properties, checks if any has [EntityId]
    // Returns Satisfied() or Failed("message")
}
```

The source generator (or design-time tooling) builds the context from the class being validated and invokes the constraint. If it fails, a compiler diagnostic is emitted.

## Source Generator: InvariantGenerator

The `InvariantGenerator` is an `IIncrementalGenerator` that:

1. Finds classes with `[AggregateRoot]` or `[Entity]` via `ForAttributeWithMetadataName`
2. Collects methods decorated with `[Invariant]` on each class
3. Delegates to `InvariantEmitter` (no Roslyn dependency) to produce source
4. Emits `{ClassName}.Invariants.g.cs` with an `EnsureInvariants()` method

```
  [AggregateRoot] class Order         InvariantGenerator           Order.Invariants.g.cs
  ├── [Invariant] HasLines()     ──>  ForAttributeWithMetadata ──> public Result EnsureInvariants()
  ├── [Invariant] TotalIsPositive()   ExtractInvariantModel        {
  └── ...                             InvariantEmitter.Emit()          _results.Add(HasLines());
                                                                       _results.Add(TotalIsPositive());
                                                                       ...
                                                                   }
```

### Emission Strategies

| Invariant count | Generated code |
|----------------|---------------|
| 0 | `return Result.Success();` |
| 1 | `return SingleMethod();` |
| 2+ | Collect all results, return first failure |

### Separation of Concerns

```
InvariantGenerator (Roslyn SG)          InvariantEmitter (SG.Lib, no Roslyn)
─ reads Roslyn symbols                  ─ receives plain strings
─ calls ForAttributeWithMetadataName    ─ builds C# source via StringBuilder
─ extracts InvariantModel               ─ fully testable without Roslyn
─ delegates to emitter                  ─ reusable by other generators
```

This follows the same pattern as `Builder.SourceGenerator` / `Builder.SourceGenerator.Lib`.

## Builder Generation (Future)

`DddBuilderHelper` will construct `BuilderEmitModel` from DDD entity metadata and call `BuilderEmitter.Emit()` from `FrenchExDev.Net.Builder.SourceGenerator.Lib`. This follows the exact pattern used by `DockerCompose.Bundle.SourceGenerator/BuilderHelper.cs`:

```
DDD Entity metadata  ──>  DddBuilderHelper.CreateBuilderModel()  ──>  BuilderEmitter.Emit()
   (properties,               (converts to BuilderEmitModel            (generates fluent
    compositions,               with Preamble for domain events,        With*() methods,
    value objects)              InstantiationExpression for              validation, build)
                                .AsReadOnly() on collections)
```

## EF Core Generation (Future)

For each `[AggregateRoot]`, the generator will emit `IEntityTypeConfiguration<T>`:

| DDD attribute | EF Core mapping |
|--------------|----------------|
| `[EntityId]` | `HasKey(x => x.Id)` + value conversion |
| `[Property(Required = true)]` | `.IsRequired()` |
| `[Property(MaxLength = 200)]` | `.HasMaxLength(200)` |
| `[Composition]` on entity | `HasOne`/`HasMany` + `OnDelete(Cascade)` |
| `[Composition]` on value object | `OwnsOne`/`OwnsMany` |
| `[Association]` | FK + `OnDelete(SetNull)` |

## CQRS Flow

```
Client ──> PlaceOrderCommand
               │
               ▼
       PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, Result<OrderId>>
               │
               ├── 1. Build aggregate via OrderBuilder
               │       (generated With*() methods)
               │
               ├── 2. EnsureInvariants()
               │       (generated, calls all [Invariant] methods)
               │
               ├── 3. If failed → return Result.Failure
               │
               ├── 4. Persist via IOrderRepository
               │       (generated interface + implementation)
               │
               └── 5. Raise OrderPlacedEvent : IDomainEvent
```

## Project Dependencies

```
                  ┌──────────┐     ┌──────────┐
                  │  Result  │     │ Builder  │
                  └────┬─────┘     └────┬─────┘
                       │                │
                       └─────────┬───────┘
                                 │
                         ┌───────▼────────┐
        ┌──────┐         │  Ddd (runtime) │
        │ Dsl  │         │  IDomainEvent  │
        └──┬───┘         │  ICommandHandler
           │             │  IQueryHandler │
    ┌──────▼────────┐    └────────────────┘
    │ Ddd.Attributes│
    │ 13 attributes │
    │ 13 companions │
    └──────┬────────┘
           │
    ┌──────▼───────────────────┐    ┌──────────────────────┐
    │ Ddd.SourceGenerator      │───>│ Ddd.SourceGenerator  │
    │ InvariantGenerator       │    │ .Lib                 │
    │ (Roslyn, netstandard2.0) │    │ InvariantEmitter     │
    └──────────────────────────┘    │ DddBuilderHelper     │
                                    │ (no Roslyn dep)      │
                                    └──────────┬───────────┘
                                               │
                                    ┌──────────▼─────────────┐
                                    │ Builder.SourceGenerator│
                                    │ .Lib                   │
                                    │ BuilderEmitter         │
                                    └────────────────────────┘
```

## Design Decisions

### Why `Result` instead of exceptions for invariants?

Invariants report **why** they failed, support multiple simultaneous failures via `Result.Aggregate()`, and are composable. Exceptions are binary (throw or not) and expensive. `Result` is a value — it can be returned, stored, logged, and tested without try/catch.

### Why `partial class` for generated code?

The developer writes one partial class with attributes and invariant methods. The generator emits another partial class with backing fields, property implementations, `EnsureInvariants()`, etc. Neither touches the other's code. The developer's file is never overwritten.

### Why separate Entity from AggregateRoot at the attribute level?

In DDD, an AggregateRoot IS-A Entity, but with additional responsibilities (transaction boundary, invariant enforcement, event publishing). The `[MetaInherits(typeof(EntityConcept))]` declaration captures this. The generator treats AggregateRoot as an Entity-plus: it gets everything an Entity gets, plus builder, repository, command handlers, and event collection.

### Why `[Composition]` vs `[Association]` vs `[Aggregation]`?

These map directly to DDD relationship semantics and drive EF Core generation:
- **Composition**: lifecycle ownership, cascade delete, inside the aggregate boundary
- **Association**: cross-aggregate reference, eventual consistency, no cascade
- **Aggregation**: shared reference, nullable FK, no cascade

The wrong choice produces incorrect database behavior, so making it explicit at the attribute level prevents mistakes.
