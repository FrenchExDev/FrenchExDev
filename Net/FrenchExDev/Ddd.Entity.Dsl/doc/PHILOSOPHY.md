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
- **Stage 1 (this project)**: DDD attributes → Entity.Dsl attributes. Pure text output, no EF Core dependency.
- **Stage 2 (Entity.Dsl)**: Entity.Dsl attributes → EF Core code. Doesn't know about DDD.

Each stage is independently testable. The Lib's emitter tests verify the bridge output without Roslyn or EF Core. The Entity.Dsl tests verify EF Core generation without DDD. Changes to one stage don't break the other.

---

## Partial classes because two SGs contribute to one type

The developer writes:
```csharp
[AggregateRoot]
public partial class Order { ... }
```

The bridge SG emits:
```csharp
[MappedEntity]
public partial class Order { }
```

The Entity.Dsl SG emits:
```csharp
public partial class Order { /* EF Core-specific members */ }
```

All three are the same class. C# partial classes merge them at compile time. The developer sees one `Order` class with DDD attributes. The compiler sees one `Order` class with DDD attributes, Entity.Dsl attributes, and generated EF Core code.

Without partial classes, the bridge would need to rewrite the entire class -- including the developer's code -- which is fragile and would conflict with other SGs.

---

## Lib + SG split because Roslyn SGs are hard to test

A Roslyn source generator runs inside the compiler pipeline. Testing it requires setting up `CSharpCompilation`, `SyntaxTree`, `MetadataReference` -- heavy infrastructure for what is essentially a string transformation.

The Lib project isolates the string transformation: `DddBridgeModel → string`. It's a pure function with no Roslyn dependency. Tests create a `DddBridgeModel`, call `Emit()`, and assert the output contains the expected attribute and namespace. Three lines of setup, zero Roslyn boilerplate.

The SG project does the Roslyn integration: discover attributed classes, extract models, call the emitter. This thin layer is tested via integration tests or manual verification, not unit tests.

This is the same Lib/SG split used by Builder (`Builder.SourceGenerator.Lib` + `Builder.SourceGenerator`) and Entity.Dsl in the FrenchExDev ecosystem.

---

## Fully qualified attributes to avoid namespace conflicts

The emitter outputs `[global::FrenchExDev.Net.Entity.Dsl.Attributes.MappedEntity]` with the `global::` prefix. This ensures the attribute resolves correctly regardless of the user's `using` directives or namespace structure.

Without `global::`, a user namespace like `MyApp.Entity.Dsl.Attributes` could shadow the real attribute. Fully qualified names with `global::` are immune to shadowing. The generated code is slightly verbose, but it never breaks.

---

## Relationship semantics are modeled but not yet emitted

The `DddBridgeModel` already has fields for `EntityId`, `Composition`, `Aggregation`, and `Association` property names. These represent the planned DDD → Entity.Dsl property-level mappings:

| DDD | Entity.Dsl | EF Core behavior |
|-----|-----------|-----------------|
| `[EntityId]` | `[PrimaryKey]` | Primary key column |
| `[Composition]` | `OnDelete = Cascade` | Parent deleted → children deleted |
| `[Aggregation]` | `OnDelete = Restrict` | Can't delete parent while children exist |
| `[Association]` | `OnDelete = NoAction` | No cascade, FK nullable |

The model captures the design intent now. The emitter will be extended to emit these property-level attributes when Entity.Dsl supports them. Building the model first ensures the SG's public contract doesn't change when the implementation catches up.
