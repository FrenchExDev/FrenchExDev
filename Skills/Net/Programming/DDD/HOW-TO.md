# DDD — How-To

## Defining a New Aggregate Root

```csharp
[AggregateRoot("Order")]
public partial class Order
{
    [EntityId]
    public partial OrderId Id { get; }

    [Property("Status", Required = true)]
    public partial OrderStatus Status { get; }

    [Composition]
    public partial IReadOnlyList<OrderLine> Lines { get; }

    [Association]
    public partial CustomerId? CustomerId { get; }

    [Invariant("Order must have at least one line")]
    private Result HasLines() => Lines.Count > 0
        ? Result.Success()
        : Result.Failure("Order must have at least one line");

    [Invariant("Total must be positive")]
    private Result TotalIsPositive() => Lines.Sum(l => l.Amount) > 0
        ? Result.Success()
        : Result.Failure("Total must be positive");
}
```

Checklist:
- [ ] Has `[AggregateRoot("Name")]` attribute
- [ ] Has exactly one `[EntityId]` property
- [ ] Uses `[Composition]` for owned children inside the aggregate
- [ ] Uses `[Association]` for cross-aggregate references (nullable)
- [ ] Has `[Invariant]` methods returning `Result`

## Choosing Relationship Attributes

| Scenario | Attribute | Why |
|----------|-----------|-----|
| Order owns OrderLines | `[Composition]` | Lines live and die with the Order. Same aggregate. |
| Order references Customer | `[Association]` | Customer is a different aggregate. Nullable FK. |
| Tag shared across Products | `[Aggregation]` | Neither side owns the other. Shared lifecycle. |

Decision rule:
1. Are they in the **same aggregate** (loaded/saved together)? -> `[Composition]`
2. Are they in **different aggregates**? -> `[Association]`
3. Is it a **shared/weak reference** with no ownership? -> `[Aggregation]`

## Defining Entities and Value Objects

```csharp
// Entity: has identity, can contain other entities/VOs
[Entity("OrderLine")]
public partial class OrderLine
{
    [EntityId] public partial OrderLineId Id { get; }
    [Property("Quantity", Required = true)] public partial int Quantity { get; }
    [Composition] public partial Money Amount { get; }
}

// Value Object: no identity, immutable, compared by value
[ValueObject("Money")]
public partial class Money
{
    [ValueComponent("Amount")] public partial decimal Amount { get; }
    [ValueComponent("Currency")] public partial string Currency { get; }
}
```

## Adding a New DDD Concept

When extending the DDD DSL with a new concept:

1. **Create the attribute** in `Ddd/src/.../Attributes/`:
   ```csharp
   [MetaConcept(typeof(MyConcept))]
   [AttributeUsage(AttributeTargets.Class)]
   public sealed class MyConceptAttribute : Attribute
   {
       [MetaProperty("Name", "string", Required = true)]
       public string Name { get; }
       public MyConceptAttribute(string name) => Name = name;
   }
   ```

2. **Create the MetaConcept** in `Ddd/src/.../Attributes/Concepts/`:
   ```csharp
   public sealed class MyConcept : MetaConcept
   {
       public override string Name => "MyConcept";
       public override Type AttributeType => typeof(MyConceptAttribute);
       public override bool CanContain(MetaConcept child)
           => child is EntityConcept || child is ValueObjectConcept;
   }
   ```

3. **Add constraints** if needed:
   ```csharp
   [MetaConstraint("MustHaveX", nameof(MustHaveXConstraint),
       Message = "MyConcept must have X")]
   public static ConstraintResult MustHaveXConstraint(ConceptValidationContext ctx)
   {
       // Validate and return Satisfied() or Failed()
   }
   ```

4. **Write tests** in `Ddd/test/.../DddAttributeTests.cs` — verify `[MetaConcept]` is present, concept hierarchy is correct, constraints pass/fail as expected.

## Adding a Constraint

```csharp
// On the attribute class
[MetaConstraint("MustBePositive", nameof(MustBePositiveConstraint),
    Message = "Price property must be positive")]
public static ConstraintResult MustBePositiveConstraint(ConceptValidationContext ctx)
{
    // ctx.Properties gives you the properties of the annotated class
    // ctx.TypeName, ctx.ConceptName give you context
    return ConstraintResult.Satisfied();
}
```

Constraints are validated by the Dsl framework at compile time (via SG diagnostic) or at design time.
