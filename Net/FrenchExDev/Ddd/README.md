# FrenchExDev.Net.Ddd

A Domain-Driven Design DSL built on [FrenchExDev.Net.Dsl](../Dsl/). Define aggregates, entities, value objects, invariants, and CQRS commands using C# attributes. A Roslyn source generator produces entity implementations, builders, EF Core configurations, repositories, and command handlers.

## The Idea

You write ~120 lines of attributed partial classes. The compiler generates ~1,000 lines of infrastructure.

```csharp
// What you write:
[AggregateRoot("Order", BoundedContext = "Ordering")]
public partial class Order
{
    [EntityId]
    public partial OrderId Id { get; }

    [Property("OrderDate", Required = true)]
    public partial DateTime OrderDate { get; }

    [Composition]
    public partial IReadOnlyList<OrderLine> Lines { get; }

    [Composition]
    public partial ShippingAddress ShippingAddress { get; }

    [Association]
    public partial CustomerId CustomerId { get; }

    [Invariant("Order must have at least one line")]
    private Result HasLines()
        => Lines.Count > 0
            ? Result.Success()
            : Result.Failure("Order must have at least one line");

    [Invariant("Order total must be positive")]
    private Result TotalIsPositive()
    {
        var total = Lines.Sum(l => l.LineTotal.Amount);
        return total > 0 ? Result.Success() : Result.Failure($"Total ({total}) is not positive");
    }
}
```

```csharp
// What the compiler generates:
// Order.g.cs              — backing fields, property implementations, domain event collection
// Order.Invariants.g.cs   — EnsureInvariants() calling HasLines() + TotalIsPositive()
// OrderBuilder.g.cs       — fluent builder with validation
// OrderConfiguration.g.cs — EF Core IEntityTypeConfiguration<Order>
// IOrderRepository.g.cs   — repository interface
// OrderRepository.g.cs    — repository implementation
```

## Concepts

### 13 DSL Attributes

All attributes are `[MetaConcept]`-decorated, making them part of the M3 metamodel.

#### Class-level (what kind of thing is this?)

| Attribute | Usage | Key properties |
|-----------|-------|---------------|
| `[AggregateRoot("Order")]` | Marks a class as an aggregate root | `Name`, `BoundedContext` |
| `[Entity("OrderLine")]` | Marks a class as an entity within an aggregate | `Name` |
| `[ValueObject("Money")]` | Marks a class as an immutable value object | `Name` |
| `[Command("PlaceOrder")]` | Marks a class as a CQRS command | `Name`, `AggregateRoot` |
| `[DomainEvent("OrderPlaced")]` | Marks a class as a domain event | `Name`, `SourceAggregate` |
| `[BoundedContext("Ordering")]` | Declares a bounded context (assembly-level) | `Name`, `Description` |

#### Property-level (what does this property mean?)

| Attribute | Usage | Key properties |
|-----------|-------|---------------|
| `[EntityId]` | Marks the identity property | — |
| `[Property("Name")]` | Marks a domain property | `Name`, `Required`, `MaxLength` |
| `[ValueComponent("Amount", "decimal")]` | Marks a value object component | `Name`, `Type`, `Required` |
| `[Composition]` | Ownership relationship (cascade delete) | — |
| `[Association]` | Cross-aggregate reference (set null) | — |
| `[Aggregation]` | Weak reference | — |

#### Method-level (what does this method enforce?)

| Attribute | Usage | Key properties |
|-----------|-------|---------------|
| `[Invariant("description")]` | Marks a method as an aggregate invariant | `Description` |

### Relationship Semantics

| Attribute | Semantics | EF Core mapping | Delete behavior |
|-----------|-----------|-----------------|-----------------|
| `[Composition]` on entity | Parent owns child | `HasOne`/`HasMany` | Cascade |
| `[Composition]` on value object | Embedded columns | `OwnsOne`/`OwnsMany` | Cascade |
| `[Association]` | Cross-aggregate reference | FK with no cascade | SetNull |
| `[Aggregation]` | Weak reference | FK, nullable | SetNull |

### Invariant Enforcement

Methods marked `[Invariant]` must be `private` and return `Result` (from `FrenchExDev.Net.Result`). The source generator discovers them and emits `EnsureInvariants()`:

```csharp
// Generated: Order.Invariants.g.cs
public partial class Order
{
    public Result EnsureInvariants()
    {
        var _results = new List<Result>();
        _results.Add(HasLines());
        _results.Add(TotalIsPositive());
        foreach (var _r in _results)
        {
            if (_r.IsFailure) return _r;
        }
        return Result.Success();
    }
}
```

`EnsureInvariants()` is called by generated builders after construction and by command handlers after mutation. Invalid aggregates never reach the database.

### CQRS Interfaces

```csharp
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
```

### Metamodel Features

`AggregateRootAttribute` demonstrates the full M3 feature set:

- `[MetaConcept(typeof(AggregateRootConcept))]` — links to behavioral companion
- `[MetaInherits(typeof(EntityConcept))]` — AggregateRoot IS-A Entity at metamodel level
- `[MetaConstraint("MustHaveId", nameof(MustHaveIdConstraint))]` — real C# validation method
- `AggregateRootConcept.CanContain()` — only Entity and ValueObject can live inside an aggregate

### Compile-Time Diagnostics

| ID | Severity | Rule |
|----|----------|------|
| DDD001 | Error | Aggregate root missing `[EntityId]` property |
| DDD002 | Error | Entity not reachable via `[Composition]` from any aggregate root |
| DDD003 | Error | `[Composition]` across aggregate boundaries |
| DDD004 | Error | Value object not owned via `[Composition]` |
| DDD100 | Warning | Aggregate root has zero `[Invariant]` methods |
| DDD101 | Warning | `[Invariant]` method does not return `Result` |
| DDD102 | Error | `[Invariant]` method is not `private` |

## Project Structure

```
Ddd/
  FrenchExDev.Net.Ddd.slnx
  src/
    FrenchExDev.Net.Ddd/                          Runtime interfaces
      IDomainEvent.cs                               domain event contract
      ICommandHandler.cs                             CQRS command handler
      IQueryHandler.cs                               CQRS query handler
    FrenchExDev.Net.Ddd.Attributes/               13 DSL attributes + 13 companions
      AggregateRootAttribute.cs                      with MustHaveIdConstraint
      EntityAttribute.cs
      ValueObjectAttribute.cs
      EntityIdAttribute.cs
      PropertyAttribute.cs
      ValueComponentAttribute.cs
      CompositionAttribute.cs
      AssociationAttribute.cs
      AggregationAttribute.cs
      InvariantAttribute.cs
      CommandAttribute.cs
      DomainEventAttribute.cs
      BoundedContextAttribute.cs
      Concepts/                                      behavioral companions
        AggregateRootConcept.cs                        SuperTypes, CanContain
        EntityConcept.cs
        ValueObjectConcept.cs
        ... (13 total)
    FrenchExDev.Net.Ddd.SourceGenerator/          Roslyn incremental SG
      InvariantGenerator.cs                          discovers [Invariant], emits EnsureInvariants()
    FrenchExDev.Net.Ddd.SourceGenerator.Lib/      Emission logic (no Roslyn dep)
      InvariantEmitter.cs                            string-based EnsureInvariants() emitter
      DddBuilderHelper.cs                            entity-to-builder model conversion (placeholder)
    FrenchExDev.Net.Ddd.Testing/                  Test helpers
      DddTestHelpers.cs                              IsAggregateRoot() reflection helper
  test/
    FrenchExDev.Net.Ddd.Tests/                    16 tests
      DddAttributeTests.cs                           MetaConcept, MetaInherits, constraints, containment
```

## Dependencies

| Project | Depends on | Why |
|---------|-----------|-----|
| `Ddd` | `Result`, `Builder` | Invariants return `Result`; builders use `AbstractBuilder<T>` |
| `Ddd.Attributes` | `Dsl` | M3 attributes: `[MetaConcept]`, `[MetaProperty]`, etc. |
| `Ddd.SourceGenerator` | `Ddd.SourceGenerator.Lib`, `Builder.SourceGenerator.Lib` | Emission helpers |

## Target Frameworks

| Project | TFM |
|---------|-----|
| `Ddd`, `Ddd.Attributes`, `Ddd.Testing` | `netstandard2.0;net10.0` |
| `Ddd.SourceGenerator`, `Ddd.SourceGenerator.Lib` | `netstandard2.0` |
| `Ddd.Tests` | `net10.0` |

## License

MIT
