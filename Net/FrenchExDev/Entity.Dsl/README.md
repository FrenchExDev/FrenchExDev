# Entity.Dsl

Attribute-based DSL that generates production-ready Entity Framework Core code from decorated POCO classes.

Developers describe **what** their domain model is; the source generator produces **how** EF Core configures it.

## Quick Start

```csharp
// 1. Decorate your POCO with DDD + Entity.Dsl attributes
[AggregateRoot("Order")]
[Table("Orders", Schema = "sales")]
[Timestampable]
[SoftDeletable]
public partial class Order
{
    [EntityId]
    [PrimaryKey(ValueGenerated = ValueGeneration.OnAdd)]
    public Guid Id { get; set; }

    [Property("OrderNumber", Required = true, MaxLength = 50)]
    [Column(Name = "order_number")]
    public string OrderNumber { get; set; } = "";

    [Property("Total", Required = true)]
    public decimal Total { get; set; }

    [Composition]
    [HasMany(WithOne = "Order", ForeignKey = "OrderId")]
    public List<OrderItem> Items { get; set; } = new();
}

// 2. Declare your DbContext
[DbContext]
public partial class SalesDbContext : DbContext { }

// 3. Register in DI
services.AddSalesDbContext(o => o.UseSqlServer("..."));
services.AddMyAppInjectables(); // Injectable SG handles repos + UoW
```

The source generator produces:
- `IEntityTypeConfiguration<T>` with full Fluent API (Generation Gap pattern)
- Repository interface + base + stub per entity (with `[Injectable]`)
- UnitOfWork interface + base + stub per DbContext (with `[Injectable]`)
- DbContext base with lifecycle hooks
- Behavior properties via partial classes (`[Timestampable]`, `[SoftDeletable]`, etc.)

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Entity.Dsl.Attributes` | netstandard2.0;net10.0 | DSL attributes + MetaConcept companions |
| `Entity.Dsl.Abstractions` | net10.0 | Runtime interfaces (`IRepository<T>`, `IUnitOfWork`, etc.) |
| `Entity.Dsl.SourceGenerator` | netstandard2.0 | Incremental Roslyn source generator |
| `Entity.Dsl.SourceGenerator.Lib` | netstandard2.0 | Roslyn-free emit models + emitters (public API for DSL-to-DSL) |

## Key Design Decisions

- **Complement DDD, don't duplicate** -- Entity.Dsl reads `[Entity]`, `[AggregateRoot]`, `[Composition]`, `[Aggregation]` from `Ddd.Attributes` and adds persistence-specific semantics
- **Domain layer stays EF-Core-free** -- POCOs reference Entity.Dsl.Attributes (no EF Core dependency)
- **Generation Gap pattern** -- Base.g.cs (always regenerated, virtual methods) + partial stub (developer extends)
- **`[Injectable]` reuse** -- Repositories and UnitOfWork use `[Injectable]` from the Injectable project; only `AddDbContext` gets a dedicated extension
- **Lifecycle convention** -- `[Composition]` = Cascade, `[Aggregation]` = Restrict, `[Association]` = NoAction
- **Behaviors via attributes** -- `[Timestampable]`, `[SoftDeletable]`, etc. generate properties + config + SaveChanges hooks (zero boilerplate)

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project structure, Generation Gap pattern, SOLID extensibility
- [HOW-TO.md](doc/HOW-TO.md) -- developer guide with examples for every feature
- [PLAN.md](doc/PLAN.md) -- full implementation plan with all phases

## Building

```bash
dotnet build Entity.Dsl/FrenchExDev.Net.Entity.Dsl.slnx
dotnet test Entity.Dsl/FrenchExDev.Net.Entity.Dsl.slnx
```
