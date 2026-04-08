# Ddd.Entity.Dsl -- Philosophy

## Bridge SG because DDD and persistence are separate concerns

The Ddd project defines domain modeling concepts: aggregate roots, entities, value objects, compositions, aggregations. It knows nothing about databases.

The Entity.Dsl project defines persistence concepts: mapped entities, primary keys, navigation properties, delete behaviors. It knows nothing about DDD.

Merging them would couple domain modeling to persistence -- exactly the kind of coupling DDD exists to prevent. But duplicating attributes manually (`[AggregateRoot]` AND `[MappedEntity]` on the same class) is tedious and error-prone.

The bridge SG resolves this: write `[AggregateRoot]`, get `[MappedEntity]` for free. Domain classes stay pure DDD. Persistence code is generated. The bridge is the adapter between two clean abstractions.

---

## Two-stage generation over one monolithic SG

A single SG could read `[AggregateRoot]` and emit EF Core code directly. But that SG would need to understand both DDD semantics AND EF Core internals. It would be coupled to both domains and harder to test, maintain, and evolve independently.

Two stages keep each SG focused:
- **Stage 1 (this project)**: DDD attributes -> Entity.Dsl attributes. Pure text output, no EF Core dependency.
- **Stage 2 (Entity.Dsl)**: Entity.Dsl attributes -> EF Core code. Doesn't know about DDD.

Each stage is independently testable. The Lib's emitter tests verify the bridge output without Roslyn or EF Core. The Entity.Dsl tests verify EF Core generation without DDD. Changes to one stage don't break the other.

---

## Class-level attributes because partial classes cannot decorate existing properties

C# partial classes can add new members and class-level attributes, but they cannot add attributes to properties declared in another partial. When the developer writes:

```csharp
[AggregateRoot("Order")]
public partial class Order
{
    [EntityId]
    public Guid Id { get; set; }
}
```

The bridge SG emits a separate partial class. It cannot add `[PrimaryKey]` to the existing `Id` property -- that would require redeclaring the property, causing a duplicate member error.

The solution: express property-level metadata at class level using property name strings. This follows the same pattern as EF Core 7+'s `[PrimaryKey(nameof(Id))]`:

```csharp
[MappedEntity]
[PrimaryKey("Id")]
public partial class Order { }
```

The Entity.Dsl SG reads both class-level `[PrimaryKey("Id")]` and property-level `[PrimaryKey]` (for non-bridge users). Class-level takes precedence when both are present.

The same approach applies to relationships:

```csharp
[NavigationProperty("Lines", OnDelete = DeleteBehavior.Cascade)]
```

This is slightly more verbose than a property-level attribute, but it compiles correctly and conveys the same information to the Entity.Dsl SG.

---

## Partial classes because two SGs contribute to one type

The developer writes:
```csharp
[AggregateRoot("Order")]
public partial class Order { ... }
```

The bridge SG emits:
```csharp
[MappedEntity]
[PrimaryKey("Id")]
[NavigationProperty("Lines", OnDelete = DeleteBehavior.Cascade)]
public partial class Order { }
```

The Entity.Dsl SG emits:
```csharp
// OrderConfigurationBase with HasKey, OnDelete fluent API
// OrderConfiguration partial stub
// DbContext with DbSet<Order>
```

All partials merge at compile time. The developer sees one `Order` class with DDD attributes. The compiler sees one `Order` class with DDD attributes, Entity.Dsl attributes, and generated EF Core code.

Without partial classes, the bridge would need to rewrite the entire class -- including the developer's code -- which is fragile and would conflict with other SGs.

---

## Lib + SG split because Roslyn SGs are hard to test

A Roslyn source generator runs inside the compiler pipeline. Testing it requires setting up `CSharpCompilation`, `SyntaxTree`, `MetadataReference` -- heavy infrastructure for what is essentially a string transformation.

The Lib project isolates the string transformation: `DddBridgeModel -> string`. It's a pure function with no Roslyn dependency. Tests create a `DddBridgeModel`, call `Emit()`, and assert the output contains the expected attributes. Three lines of setup, zero Roslyn boilerplate.

The SG project does the Roslyn integration: discover attributed classes, extract models, call the emitter. This thin layer is tested via integration tests or manual verification, not unit tests.

This is the same Lib/SG split used by Builder (`Builder.SourceGenerator.Lib` + `Builder.SourceGenerator`) and Entity.Dsl in the FrenchExDev ecosystem.

---

## Fully qualified attributes to avoid namespace conflicts

The emitter outputs `[global::FrenchExDev.Net.Entity.Dsl.Attributes.MappedEntity]` with the `global::` prefix. This ensures the attribute resolves correctly regardless of the user's `using` directives or namespace structure.

Without `global::`, a user namespace like `MyApp.Entity.Dsl.Attributes` could shadow the real attribute. Fully qualified names with `global::` are immune to shadowing. The generated code is slightly verbose, but it never breaks.

---

## DDD relationship semantics mapped to EF Core delete behaviors

Each DDD relationship type has a precise semantic that maps to an EF Core delete behavior:

| DDD | Semantic | Delete Behavior | Rationale |
|-----|----------|----------------|-----------|
| `[Composition]` | The child is part of the parent (whole-part). It has no independent lifecycle. | `Cascade` | If the parent is deleted, the child must be deleted too -- it cannot exist alone. |
| `[Aggregation]` | The child is referenced by the parent but has its own lifecycle. It may be shared across aggregates. | `Restrict` | The parent cannot be deleted while children reference it -- prevents orphaned references. |
| `[Association]` | Independent entities linked by a loose reference. Neither owns the other. | `NoAction` | No cascading -- the entities are independent and the FK may be nullable. |

These mappings are conventions. If a developer needs different behavior, they can override the generated `ConfigureRelationships` method in their partial `Configuration` class (Entity.Dsl's generation gap pattern).
