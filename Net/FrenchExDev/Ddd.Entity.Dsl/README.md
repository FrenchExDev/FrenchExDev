# Ddd.Entity.Dsl

Bridge source generator that maps DDD attributes to Entity.Dsl attributes. When a class is decorated with `[AggregateRoot]` or `[Entity]`, this SG emits a partial class with `[MappedEntity]`, `[PrimaryKey]`, and `[NavigationProperty]` attributes, which the Entity.Dsl source generator then picks up to generate EF Core persistence code. The bridge eliminates manual duplication -- DDD users get Entity.Dsl code generation for free.

## Quick Start

```csharp
// 1. Developer writes DDD domain model
[AggregateRoot("Order")]
public partial class Order
{
    [EntityId]
    public Guid Id { get; set; }

    public string CustomerName { get; set; } = "";

    [Composition]
    public List<OrderLine> Lines { get; set; } = [];

    [Aggregation]
    public Customer Customer { get; set; } = null!;
}

// 2. Ddd.Entity.Dsl bridge SG generates Order.DddBridge.g.cs:
//
//    [MappedEntity]
//    [PrimaryKey("Id")]
//    [NavigationProperty("Lines", OnDelete = DeleteBehavior.Cascade)]
//    [NavigationProperty("Customer", OnDelete = DeleteBehavior.Restrict)]
//    public partial class Order { }

// 3. Entity.Dsl SG reads these attributes and generates:
//    - OrderConfigurationBase (HasKey, OnDelete fluent API)
//    - OrderConfiguration (partial stub for overrides)
//    - OrderConfigurationRegistration (IEntityTypeConfiguration<T>)
//    - DbContext with DbSet<Order>
//    - IOrderRepository + OrderRepository
//    - IUnitOfWork
```

## DDD to Entity.Dsl Mapping

| DDD Attribute | Entity.Dsl Attribute | EF Core Behavior |
|---------------|---------------------|------------------|
| `[AggregateRoot]` / `[Entity]` | `[MappedEntity]` | Entity mapped to table |
| `[EntityId]` | `[PrimaryKey("...")]` (class-level) | Primary key column(s) |
| `[Composition]` | `[NavigationProperty(OnDelete = Cascade)]` | Parent deleted -> children deleted |
| `[Aggregation]` | `[NavigationProperty(OnDelete = Restrict)]` | Cannot delete parent while children exist |
| `[Association]` | `[NavigationProperty(OnDelete = NoAction)]` | No cascade, independent lifecycle |

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Ddd.Entity.Dsl.SourceGenerator` | netstandard2.0 | Incremental SG: reads DDD attributes, emits Entity.Dsl attributes |
| `Ddd.Entity.Dsl.SourceGenerator.Lib` | netstandard2.0 | `DddEntityDslBridgeEmitter` + `DddBridgeModel` (no Roslyn dependency, testable) |
| `Ddd.Entity.Dsl.Tests` | net10.0 | 10 xUnit tests for the emitter |

## Key Design Decisions

- **Bridge SG, not a monolith** -- Ddd and Entity.Dsl are separate concerns; this SG is the adapter between them
- **Class-level attributes** -- C# partial classes cannot add attributes to existing properties, so the bridge emits `[PrimaryKey("Id")]` and `[NavigationProperty("Lines", ...)]` at class level
- **Lib + SG split** -- the emitter logic lives in a testable Lib (no Roslyn dependency); the SG links it as source files
- **Two-stage generation** -- Ddd.Entity.Dsl emits Entity.Dsl attributes -> Entity.Dsl SG reads them and emits EF Core code

## Entity.Dsl Attributes Used by the Bridge

The bridge emits these Entity.Dsl attributes (all in `FrenchExDev.Net.Entity.Dsl.Attributes`):

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[MappedEntity]` | Class | Entry point for Entity.Dsl SG |
| `[PrimaryKey(params string[])]` | Class | Declares primary key property names (supports composite) |
| `[NavigationProperty(name, OnDelete)]` | Class | Declares a relationship with delete behavior |
| `DeleteBehavior` enum | -- | `Cascade`, `Restrict`, `NoAction`, `SetNull`, `ClientCascade` |

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- two-stage SG pipeline, project structure, bridge model, emitter design
- [HOW-TO.md](doc/HOW-TO.md) -- using the bridge, adding the SG, understanding generated output
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why a bridge SG, why class-level attributes, why partial classes

## Building

```bash
dotnet build Ddd.Entity.Dsl/FrenchExDev.Net.Ddd.Entity.Dsl.slnx
dotnet test Ddd.Entity.Dsl/FrenchExDev.Net.Ddd.Entity.Dsl.slnx
```
