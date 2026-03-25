# DDD — Philosophy

## Attribute-Based DDD, Not Framework DDD

This codebase does not force domain entities to extend base classes or implement marker interfaces. DDD is expressed via **attributes** that trigger source generation and validation:

```csharp
[AggregateRoot("Product")]
public partial class Product
{
    [EntityId] public partial ProductId Id { get; }
    [Property("Name", Required = true)] public partial string Name { get; }
    [Composition] public partial IReadOnlyList<ProductVariant> Variants { get; }

    [Invariant("Price must be positive")]
    private Result PriceIsPositive() => Price > 0
        ? Result.Success()
        : Result.Failure("Price must be positive");
}
```

No `AggregateRoot<T>` base class. No `IEntity` interface. Just attributes on plain C# classes.

## Metamodel Foundation

DDD is a DSL built on the `Dsl` framework's M3 metamodel. Every DDD attribute has a `[MetaConcept]` companion that carries validation rules and containment constraints:

```
M3 (Dsl)       [MetaConcept(typeof(AggregateRootConcept))]
                defines what "AggregateRoot" means as a modeling concept
M2 (Ddd)       public sealed class AggregateRootAttribute : Attribute
                the DDD DSL attribute applied by developers
M1 (App)       [AggregateRoot("Order")] public partial class Order
                the developer's domain model
M0 (Runtime)   order.Lines.Count = 3, order.Total = 1500
                live data at runtime
```

See: `Dsl/src/.../MetaConcept.cs`, `Ddd/src/.../Attributes/Concepts/`

## Aggregate Boundaries Via Relationship Attributes

Three relationship types enforce aggregate boundaries at compile time:

- **`[Composition]`** — inside the aggregate boundary. Cascade delete. Owned lifecycle. For entities/VOs within the same aggregate.
- **`[Association]`** — cross-aggregate reference. Nullable FK. Eventual consistency. Never cascade delete.
- **`[Aggregation]`** — weak reference. Nullable FK. Shared lifecycle.

The SG validates these relationships: `[Composition]` targets must be `[Entity]` or `[ValueObject]` within the same aggregate. `[Association]` targets must be `[AggregateRoot]` in a different aggregate.

## Invariants as Methods, Not Configuration

Invariants are `[Invariant]`-decorated methods that return `Result`:

```csharp
[Invariant("Price must be positive")]
private Result PriceIsPositive() => Price > 0
    ? Result.Success()
    : Result.Failure("Price must be positive");
```

The SG generates `EnsureInvariants()` that calls all invariant methods and aggregates failures via `Result.Combine()`. Invariants never throw — they return failure values.

## Builders for Construction

Domain objects are constructed through generated builders, not constructors:

```csharp
var product = await new ProductBuilder()
    .WithName("Widget")
    .WithPrice(9.99m)
    .WithVariants(v => v.WithColor("Red"))
    .BuildAsync();
```

Validation lives in the builder (`ValidateAsync`), not in entity constructors. Entities are valid by construction — if `BuildAsync()` succeeds, all invariants hold.

See: `Builder/src/FrenchExDev.Net.Builder/Code.cs` (AbstractBuilder<T>)

## Diem CMF Is the Primary Consumer

The DDD DSL is designed for the Diem Content Management Framework. Diem uses DDD alongside other DSLs: Content (Parts, Blocks, StreamFields), Admin (Forms, Lists, Actions), Pages (Widgets), Workflow (Gates, StateMachine). Each is its own FrenchExDev.Net project.

See: `Diem/doc/ARCHITECTURE.md`
