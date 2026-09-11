# Philosophy — FrenchExDev.Net.Ddd

## The Domain Is the Architecture

In most enterprise systems, the domain model is an afterthought. The team picks a framework, configures the database, sets up the API layer, and then asks: "what are we actually building?" The domain lives in a thin service layer between the controller and the ORM, shaped by technical constraints rather than business reality.

DDD reverses this. The domain model comes first. It uses the language of the business — aggregates, entities, value objects, invariants, commands, events. The infrastructure (persistence, API, UI) is generated from the domain model, not the other way around.

This DSL makes that inversion concrete. You write `[AggregateRoot("Order")]` and the compiler generates the infrastructure. The domain model IS the architecture.

## Why Attributes Instead of Base Classes

Traditional DDD frameworks use base classes: `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`. Your domain classes inherit from them, gaining identity, equality, and persistence behavior.

The problem: your domain model becomes coupled to the framework. `Order : AggregateRoot<OrderId>` means Order IS-A framework class. You can't use a different persistence strategy without changing the domain model. You can't test the domain without referencing the framework.

With attributes, the coupling is declarative, not structural:

```csharp
[AggregateRoot("Order")]           // ← declaration (removable)
public partial class Order          // ← your class (no base class)
{
    [EntityId]
    public partial OrderId Id { get; }

    [Invariant("Must have lines")]
    private Result HasLines() => ...  // ← your logic (pure C#)
}
```

The attributes say what Order IS in the domain (an aggregate root). They don't force Order to inherit from anything. The source generator reads the attributes and produces a partial class with the infrastructure code. Your half of the partial class is pure domain logic with zero framework dependencies.

Remove the attributes, and Order is still a valid C# class. It just doesn't get generated infrastructure.

## Why Invariants Return Result, Not Throw

The most common approach to aggregate invariants is guard clauses that throw:

```csharp
public void AddLine(OrderLine line)
{
    if (Lines.Count >= MaxLines)
        throw new DomainException("Too many lines");
}
```

This has three problems:

1. **Binary outcome.** It either throws or it doesn't. You can't collect multiple validation failures and report them together.

2. **Expensive.** Exceptions allocate, capture stack traces, and unwind the call stack. Invariant checks happen on every mutation — they should be cheap.

3. **Untestable without try/catch.** To verify an invariant fails, you need `Assert.Throws<>()`. To verify it succeeds, you need the absence of an exception — which proves nothing.

`Result` solves all three:

```csharp
[Invariant("Order must have at least one line")]
private Result HasLines()
    => Lines.Count > 0
        ? Result.Success()
        : Result.Failure("Order must have at least one line");
```

1. **Composable.** `EnsureInvariants()` calls all invariant methods and aggregates their Results. You get every failure, not just the first one.

2. **Cheap.** `Result` is a value type. No allocation on success. No stack trace. No unwinding.

3. **Testable.** `Assert.True(order.EnsureInvariants().IsSuccess)` — direct, clear, no try/catch.

The generated `EnsureInvariants()` method calls every `[Invariant]` method and returns the first failure (or success if all pass). It is injected into every generated builder and command handler. Invalid aggregates never reach the database.

## Why Composition, Association, and Aggregation Are Separate

These three look similar — they're all references from one entity to another. But they have fundamentally different semantics that drive fundamentally different infrastructure:

**Composition** means ownership. The parent controls the child's lifecycle. Delete the Order, delete its OrderLines. In DDD terms, composition defines the aggregate boundary — everything reachable via composition from the root belongs to the aggregate and is loaded/saved as a unit.

```csharp
[Composition]  // OrderLine lives and dies with Order
public partial IReadOnlyList<OrderLine> Lines { get; }
```

EF Core: `HasMany().OnDelete(Cascade)`. The child is always loaded with the parent (eager or explicit include). No orphans possible.

**Association** means reference across aggregate boundaries. The Order references a Customer, but doesn't own it. Deleting the Order doesn't delete the Customer. They live in different aggregates, different transaction boundaries, potentially different microservices.

```csharp
[Association]  // Order knows about Customer but doesn't own it
public partial CustomerId CustomerId { get; }
```

EF Core: FK with `OnDelete(SetNull)`. No cascade. Eventual consistency.

**Aggregation** means weak reference. The entity has a nullable FK to something it doesn't own and doesn't strongly depend on. If the referenced entity is deleted, the FK becomes null.

Making these explicit at the attribute level prevents the most common DDD mistake: putting a Composition where an Association should be (creating cross-aggregate transaction boundaries) or putting an Association where a Composition should be (breaking aggregate consistency).

The wrong relationship type produces incorrect database behavior. By making the developer choose explicitly, we make the choice visible and reviewable.

## Why Partial Classes, Not Inheritance

The original Diem PHP used a 6-layer inheritance chain for customization. The developer's class sat at the top, inheriting from generated classes that inherited from framework classes. This worked but had costs:

- **Deep call stacks.** `$this->getListQuery()` walked 4 levels of `parent::` calls.
- **Fragile base class problem.** Adding a method to a base class could break subclasses.
- **Single inheritance.** PHP (and C#) only allows one base class. Using it for framework plumbing means you can't use it for domain modeling.

C# partial classes avoid all three:

```csharp
// Generated (regenerated every build)
public partial class Order
{
    private OrderId _id;
    public partial OrderId Id => _id;
    public Result EnsureInvariants() => Result.Aggregate(HasLines(), TotalIsPositive());
}

// Developer's code (never overwritten)
public partial class Order
{
    [Invariant("Must have lines")]
    private Result HasLines() => Lines.Count > 0 ? Result.Success() : Result.Failure("...");
}
```

Both halves are the same class. No inheritance. No `base.` calls. No method resolution order. The compiler merges them. The developer and the generator each write their half without knowing about the other's internals.

The developer's partial class file is created once (by scaffolding or by hand) and never touched by the generator. The generator's file is overwritten on every build. They coexist peacefully.

## Why the Generator Produces Invariant Enforcement, Not the Developer

A common DDD pattern is manual invariant checking:

```csharp
public void AddLine(OrderLine line)
{
    _lines.Add(line);
    EnsureInvariants(); // developer must remember to call this
}
```

The problem: the developer must remember to call `EnsureInvariants()` after every mutation. Forget once, and an invalid aggregate reaches the database. Code review catches some misses. Production catches the rest.

In this DSL, the generated builder calls `EnsureInvariants()` automatically. The generated command handler calls it automatically. The developer never calls it manually — the generator ensures it is always called.

This is the same principle as garbage collection: a thing that must always happen should not depend on humans remembering to do it.

## Why MetaConcept on DDD Attributes

Every DDD attribute (`[AggregateRoot]`, `[Entity]`, `[Composition]`, `[Invariant]`, ...) is decorated with `[MetaConcept]` from the Dsl framework. This means:

1. **The MetamodelRegistry knows about DDD.** A project referencing `Ddd.Attributes` will have DDD concepts in its generated registry alongside any other DSL concepts it uses.

2. **Constraints are discoverable.** `AggregateRootAttribute.MustHaveIdConstraint` is a real method referenced by `[MetaConstraint]`. Design tools can find and invoke it.

3. **Containment rules are behavioral.** `AggregateRootConcept.CanContain(child)` returns true only for Entity and ValueObject. This is metamodel-level enforcement of DDD rules.

4. **Inheritance is explicit.** `[MetaInherits(typeof(EntityConcept))]` on AggregateRoot means the metamodel knows that AggregateRoot IS-A Entity — not just at the C# level, but at the modeling level.

This integration means DDD is not a special case. It is a DSL built on the same M3 foundation as every other DSL. The same source generator pipeline, the same validation infrastructure, the same design-time tooling. DDD gets no special treatment — and needs none.

## The Aggregate as the Unit of Truth

An aggregate is not just a cluster of related entities. It is the **unit of consistency** — the smallest thing that is always correct.

When a developer writes:

```csharp
[AggregateRoot("Order")]
public partial class Order
{
    [Composition] public partial IReadOnlyList<OrderLine> Lines { get; }
    [Invariant("Must have lines")] private Result HasLines() => ...
    [Invariant("Total must be positive")] private Result TotalIsPositive() => ...
}
```

They are making a statement: "An Order, together with its Lines, is always valid. The invariants always hold. If you have an Order instance, you can trust it."

The generated infrastructure enforces this trust:
- The builder checks invariants before returning an Order
- The command handler checks invariants after mutation
- The repository loads the full aggregate (with Lines) — no lazy loading surprises
- The EF Core configuration cascades deletes — no orphan OrderLines

The developer declares the aggregate's truth. The compiler enforces it everywhere.
