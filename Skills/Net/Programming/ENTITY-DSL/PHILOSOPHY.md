# ENTITY-DSL — Philosophy

The Entity DSL pattern generates EF Core persistence code from attribute-decorated POCOs. The goal is a single, declarative source of truth: the developer says **what** the model is, and a source generator produces **how** EF Core configures it, how repositories look, and how the unit of work is wired.

## What the Pattern Solves

Hand-written EF Core configuration suffers from three chronic problems:

1. **Drift.** The POCO, the `IEntityTypeConfiguration<T>`, the `DbContext` registration, and the repository interface evolve in three or four files. They drift out of sync the day after they're written.
2. **Boilerplate.** A new aggregate root means a new POCO, a new fluent-API config, a new repo interface, a new repo class, a new `DbSet<T>` line in the context, a new injection registration. None of it is interesting.
3. **Domain pollution.** The cleanest pattern says "the domain layer should not know about EF Core." But the moment you put fluent-API calls in the domain assembly, EF Core has leaked in.

The Entity DSL fixes all three by inverting the relationship: the POCO is decorated with **persistence-agnostic attributes** (`[Entity]`, `[AggregateRoot]`, `[Property]`, `[Composition]`), and a source generator emits everything EF Core needs in a separate generated file.

## The Domain Layer Has No EF Core Reference

This is the load-bearing constraint. The package containing your `Order`, `Customer`, `Invoice` POCOs references the Entity DSL **attributes** package (which has zero dependencies) and nothing else. No `Microsoft.EntityFrameworkCore`. No fluent API. No `DbContext`.

```
Domain (POCOs + attributes)        ← references: Entity.Dsl.Attributes only
   ↓
Generated EF Core config           ← lives in a separate generated file
   ↓
Infrastructure (DbContext, DI)     ← references: EF Core, the generated config
```

The benefit is concrete: the domain assembly compiles without EF Core in scope. You can reference it from a unit test project, a console tool, or another domain that doesn't need persistence, without dragging EF Core along.

## Generation Gap, Always

Every generated artefact follows the **Generation Gap pattern**: a base class (always regenerated, full of `virtual` methods) plus a partial stub (developer-owned, free to override). Regenerating the base never destroys developer customisations.

```csharp
// Always regenerated — never edit:
public abstract partial class OrderConfigurationBase : IEntityTypeConfiguration<Order>
{
    public virtual void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);
        // ... fluent API
    }
}

// Developer-owned — edit freely:
public partial class OrderConfiguration : OrderConfigurationBase { }
```

The two-file split is the only safe way to mix generated and hand-written code over a long timeline. Editing generated files is forbidden; editing the partial is encouraged.

## DDD Attributes Drive Persistence Defaults

The pattern reuses DDD vocabulary as the input language. Every relationship attribute carries an implied EF Core delete behavior:

| DDD attribute | EF Core delete behavior | Why |
|---|---|---|
| `[Composition]` | `Cascade` | Children are owned by the parent. Deleting the parent deletes the children. |
| `[Aggregation]` | `Restrict` | Children are referenced but not owned. The relationship guards against accidental deletion. |
| `[Association]` | `NoAction` | Independent lifecycles. Cleanup is the application's job. |

The defaults are correct for ~90% of relationships. Override the attribute when you need something different. The DDD vocabulary stays meaningful even when the developer never thinks about EF Core.

## Behaviors Are Attributes

Cross-cutting persistence concerns — timestamps, soft delete, audit trails, optimistic concurrency — are declared as attributes that the source generator weaves into:

- The POCO partial class (adding the relevant properties)
- The generated `IEntityTypeConfiguration<T>` (adding the column mapping)
- The `DbContext` `SaveChanges` hook (setting the value automatically)

```csharp
[AggregateRoot("Order")]
[Timestampable]                          // adds CreatedAt, UpdatedAt + SaveChanges hook
[SoftDeletable]                          // adds DeletedAt + global query filter
public partial class Order { ... }
```

The developer never writes the timestamp logic. The behavior attribute is the entire opt-in.

This is open/closed: adding a new behavior means adding a new attribute and one branch in the emitter, not editing every entity in the codebase.

## What the Generator Emits

For one decorated entity, the generator emits at minimum:

1. **`{Entity}ConfigurationBase`** — `IEntityTypeConfiguration<T>` with full fluent API
2. **`{Entity}Configuration`** — partial stub for developer overrides
3. **`I{Entity}Repository`** — repository interface inheriting from `IRepository<T>`
4. **`{Entity}RepositoryBase`** — concrete base implementation
5. **`{Entity}Repository`** — partial stub for developer overrides, marked `[Injectable]`
6. **Optional `{Entity}` partial** — adds behavior properties and any generated members

For one decorated `DbContext`, the generator emits:

1. **`{Context}Base`** — partial class adding `DbSet<T>` for every `[Entity]` in scope
2. **`AddXxxDbContextExtensions`** — DI registration helper
3. **`I{Context}UnitOfWork`** — UoW interface
4. **`{Context}UnitOfWorkBase`** + partial stub — UoW implementation marked `[Injectable]`

The developer writes ~20 lines of attributes and gets ~500 lines of correct, regenerable persistence code.

## Bridge Generators for Other DSLs

The Entity DSL takes its own attributes (`[MappedEntity]`, `[PrimaryKey]`, `[NavigationProperty]`) as input. Other DSLs — most importantly the DDD DSL — bridge into it via a **bridge source generator**.

The DDD bridge SG reads `[AggregateRoot]`, `[Entity]`, `[Composition]`, `[Aggregation]` and emits an additional partial class decorated with the Entity DSL's own attributes:

```csharp
// Developer writes:
[AggregateRoot("Order")]
public partial class Order { ... }

// DDD bridge SG emits Order.DddBridge.g.cs:
[MappedEntity]
[PrimaryKey("Id")]
[NavigationProperty("Lines", OnDelete = DeleteBehavior.Cascade)]
public partial class Order { }

// Entity DSL SG sees the bridge attributes and emits the EF Core config.
```

This is two-stage generation. Bridge generators keep the input languages separate (DDD doesn't know about EF Core; Entity DSL doesn't know about aggregates), while preserving the seamless developer experience.

## Trade-offs Accepted

| Trade-off | Decision |
|---|---|
| C# partials cannot add attributes to existing properties | Bridge SG emits **class-level** attributes (`[PrimaryKey("Id")]`, `[NavigationProperty("Lines", ...)]`). The Entity DSL SG resolves the names against the property list. |
| Two-stage generation has longer compile times | Justified by clean separation of DDD and persistence concerns. |
| Generation Gap costs one extra file per entity | Justified by safe regeneration. |
| `[Composition]` defaults to Cascade — can be wrong | Wrong cases are minority; explicit override is one attribute parameter. |
| Source-generated DI requires `[Injectable]` infrastructure | Reuses existing `[Injectable]` pattern; no new framework. |

## What This Pattern Is Not

- **Not an ORM.** EF Core remains the ORM. The pattern only generates the configuration.
- **Not a code-first migration tool.** Migrations use the EF Core CLI as usual.
- **Not a query DSL.** Repositories expose `IQueryable<T>` and standard methods. Custom queries go in the partial stub.
- **Not a CRUD scaffolder.** It generates persistence plumbing, not endpoints, screens, or DTOs.

Use this pattern when the domain has more than ~3 aggregates and you want EF Core configuration to stop drifting. Skip it for one-off scripts or single-table tools.
