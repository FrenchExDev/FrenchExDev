# ENTITY-DSL — Architecture

The Entity DSL is implemented as four packages: attributes, runtime abstractions, source generator, and emit library. A separate bridge package maps DDD attributes onto Entity DSL attributes for two-stage generation.

## Package Layout

```
Entity.Dsl/
├── src/
│   ├── FrenchExDev.Net.Entity.Dsl.Attributes        attributes + companions (netstandard2.0;net10.0)
│   ├── FrenchExDev.Net.Entity.Dsl.Abstractions      runtime interfaces (net10.0)
│   ├── FrenchExDev.Net.Entity.Dsl.SourceGenerator   Roslyn IIncrementalGenerator (netstandard2.0)
│   └── FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib  emit models + emitters, no Roslyn dep (netstandard2.0)
└── test/
    └── FrenchExDev.Net.Entity.Dsl.Tests             xUnit tests

Ddd.Entity.Dsl/                                       BRIDGE — DDD attrs → Entity.Dsl attrs
├── src/
│   ├── FrenchExDev.Net.Ddd.Entity.Dsl.SourceGenerator     bridge IIncrementalGenerator (netstandard2.0)
│   └── FrenchExDev.Net.Ddd.Entity.Dsl.SourceGenerator.Lib emit lib (netstandard2.0)
└── test/
    └── FrenchExDev.Net.Ddd.Entity.Dsl.Tests
```

The four-project shape (`Attributes` / `Abstractions` / `SourceGenerator` / `SourceGenerator.Lib`) is the canonical split for any DSL that produces runtime types and is consumed at compile time.

## Attribute Vocabulary

### Class-level attributes (entity declaration)

| Attribute | Source | Purpose |
|---|---|---|
| `[Entity]`, `[AggregateRoot]` | DDD or Entity.Dsl | Marks the class as a persistable entity |
| `[MappedEntity]` | Entity.Dsl | Entry point recognised by the SG (emitted by the DDD bridge) |
| `[PrimaryKey(params string[])]` | Entity.Dsl | Names the PK property/properties (composite keys allowed) |
| `[NavigationProperty(name, OnDelete)]` | Entity.Dsl | Declares a relationship + delete behavior |
| `[Table("name", Schema = "...")]` | Entity.Dsl or interop | Maps to a table |
| `[DbContext]` | Entity.Dsl | Marks a `DbContext` partial for generation |
| `[Timestampable]` | Entity.Dsl | Adds `CreatedAt`/`UpdatedAt` + SaveChanges hook |
| `[SoftDeletable]` | Entity.Dsl | Adds `DeletedAt` + global query filter |

### Property-level attributes

| Attribute | Source | Purpose |
|---|---|---|
| `[Property("Name", Required = ..., MaxLength = ...)]` | DDD or Entity.Dsl | Domain-level slot |
| `[EntityId]` | DDD | Marks the identity property |
| `[PrimaryKey(ValueGenerated = ...)]` | Entity.Dsl | EF Core PK column attributes |
| `[Column(Name = "...")]` | Entity.Dsl or interop | Column name override |
| `[Composition]`, `[Aggregation]`, `[Association]` | DDD | Relationship semantics (drives delete behavior) |
| `[HasMany(WithOne = "...", ForeignKey = "...")]` | Entity.Dsl | Explicit nav-property shape |

## Generated Output (Per Entity)

For one decorated entity `Order`, the SG emits:

### `OrderConfigurationBase.g.cs`

```csharp
public abstract partial class OrderConfigurationBase : IEntityTypeConfiguration<Order>
{
    public virtual void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", "sales");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedOnAdd();
        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50).HasColumnName("order_number");
        builder.Property(o => o.Total).IsRequired();
        builder.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);

        // [Timestampable]
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt).IsRequired();

        // [SoftDeletable]
        builder.HasQueryFilter(o => o.DeletedAt == null);
    }
}
```

### `OrderConfiguration.g.cs` (developer-owned partial stub)

```csharp
public partial class OrderConfiguration : OrderConfigurationBase
{
    // override Configure() if you need to extend the base
}
```

### `IOrderRepository.g.cs` and `OrderRepository.g.cs`

```csharp
public partial interface IOrderRepository : IRepository<Order> { }

public partial class OrderRepositoryBase : RepositoryBase<Order>, IOrderRepository
{
    public OrderRepositoryBase(SalesDbContext ctx) : base(ctx) { }
}

[Injectable]
public partial class OrderRepository : OrderRepositoryBase
{
    public OrderRepository(SalesDbContext ctx) : base(ctx) { }
}
```

### `Order.Behaviors.g.cs` (partial of the POCO)

```csharp
public partial class Order
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
```

## Generated Output (Per DbContext)

For one `[DbContext] partial class SalesDbContext`, the SG emits:

### `SalesDbContextBase.g.cs`

```csharp
public partial class SalesDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // [Timestampable] hook
        foreach (var e in ChangeTracker.Entries<ITimestampable>())
        {
            if (e.State == EntityState.Added) e.Entity.CreatedAt = DateTime.UtcNow;
            if (e.State == EntityState.Added || e.State == EntityState.Modified) e.Entity.UpdatedAt = DateTime.UtcNow;
        }
        // [SoftDeletable] hook
        foreach (var e in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (e.State == EntityState.Deleted) { e.State = EntityState.Modified; e.Entity.DeletedAt = DateTime.UtcNow; }
        }
        return base.SaveChangesAsync(ct);
    }
}
```

### `SalesDbContextExtensions.g.cs`

```csharp
public static class SalesDbContextExtensions
{
    public static IServiceCollection AddSalesDbContext(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure)
        => services.AddDbContext<SalesDbContext>(configure);
}
```

### `ISalesDbContextUnitOfWork.g.cs` and base/stub

UoW interface and implementation, marked `[Injectable]` so the Injectable SG registers it automatically.

## Bridge Source Generator (DDD → Entity.Dsl)

The bridge takes DDD attributes as input and produces Entity DSL attributes as output. It is its own `IIncrementalGenerator` in `Ddd.Entity.Dsl.SourceGenerator`.

Pipeline:

1. Find every class with `[AggregateRoot]` or `[Entity]`
2. Extract the entity-id property (the one with `[EntityId]`)
3. For every `[Composition]` / `[Aggregation]` / `[Association]` property, capture the name and map to a `DeleteBehavior`
4. Build a `DddBridgeModel`
5. Call `DddEntityDslBridgeEmitter.Emit(model)`
6. Add a `{ClassName}.DddBridge.g.cs` partial

Output for `Order`:

```csharp
[MappedEntity]
[PrimaryKey("Id")]
[NavigationProperty("Lines", OnDelete = DeleteBehavior.Cascade)]
[NavigationProperty("Customer", OnDelete = DeleteBehavior.Restrict)]
public partial class Order { }
```

The Entity DSL SG then sees the bridge attributes (in the same compilation, on the same type) and emits the EF Core config. **Two-stage generation** in one compile.

### Why Class-Level Attributes

C# partials cannot add attributes to **existing** properties. The bridge can only add attributes to the class. So `[PrimaryKey]` and `[NavigationProperty]` are class-level and carry the property name as a string. The Entity DSL SG resolves the names against the type's property list at emission time.

## Source Generator Constraints

| Aspect | Decision |
|---|---|
| Generator type | `IIncrementalGenerator` |
| Discovery API | `ForAttributeWithMetadataName` |
| Emit lib | `SourceGenerator.Lib`, no Roslyn reference |
| Lib type | string-based emitters + POCO models |
| Generation pattern | Generation Gap (`*Base.g.cs` + `*.g.cs` partial stub) |
| DI registration | reuses `[Injectable]` from the Injectable package |
| Test strategy | unit tests against `*Emitter.Emit(model)` in the Lib |

## Lifecycle Convention Recap

| DDD attribute | EF Core delete behavior | Rationale |
|---|---|---|
| `[Composition]` | `Cascade` | Owned children — die with the parent |
| `[Aggregation]` | `Restrict` | Referenced but not owned — protect from accidental loss |
| `[Association]` | `NoAction` | Independent lifecycles — application manages cleanup |

Override at the property level when the domain demands it. The default is the documented expectation, not a guess.
