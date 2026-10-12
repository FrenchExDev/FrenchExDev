# ENTITY-DSL — How To

Recipes for the common cases. Each one assumes the Entity DSL SG and Ddd.Entity.Dsl bridge SG are referenced from your model project.

## Declaring an Aggregate Root

```csharp
[AggregateRoot("Order")]
[Table("Orders", Schema = "sales")]
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
```

The class **must** be `partial` — generated behavior partials need to merge with it.

## Declaring a Child Entity

```csharp
[Entity]
public partial class OrderItem
{
    [EntityId]
    [PrimaryKey]
    public Guid Id { get; set; }

    [Property("Quantity", Required = true)]
    public int Quantity { get; set; }

    [Property("UnitPrice", Required = true)]
    public decimal UnitPrice { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
}
```

`[Entity]` is for owned children that participate in the same aggregate. `[AggregateRoot]` is for the root.

## Adding Behaviors

Stack behavior attributes on the class. The generator weaves the properties, configuration, and SaveChanges hooks for free.

```csharp
[AggregateRoot("Order")]
[Timestampable]      // generates CreatedAt, UpdatedAt + auto-set
[SoftDeletable]      // generates DeletedAt + global query filter
public partial class Order { ... }
```

The behavior properties are generated in `Order.Behaviors.g.cs`. You don't write them.

## Declaring a DbContext

```csharp
[DbContext]
public partial class SalesDbContext : DbContext
{
    public SalesDbContext(DbContextOptions<SalesDbContext> options) : base(options) { }
}
```

The generator emits a partial that adds:
- A `DbSet<T>` for every `[Entity]` / `[AggregateRoot]` in scope
- The `OnModelCreating` overrides applying every generated configuration
- The `SaveChangesAsync` overrides for every behavior in scope

You write 5 lines, the generator produces 100.

## Wiring DI

```csharp
services.AddSalesDbContext(o => o.UseSqlServer(connectionString));
services.AddMyAppInjectables();   // discovers all [Injectable] repos + UoW
```

The first call uses the generated `AddSalesDbContext` extension. The second invokes the Injectable SG output, which registers every generated repository and the UoW.

## Repositories

The generator emits `IOrderRepository`, `OrderRepositoryBase`, `OrderRepository` per entity. Use them as-is for CRUD, or extend the partial stub for custom queries:

```csharp
public partial class OrderRepository : OrderRepositoryBase
{
    public Task<List<Order>> FindRecentByCustomerAsync(Guid customerId, CancellationToken ct)
        => DbContext.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToListAsync(ct);
}
```

Generated members live in `OrderRepositoryBase`. Custom members live in the partial. Regenerating the base never destroys custom code.

## Overriding the Generated Configuration

The Generation Gap pattern lets you extend the generated `IEntityTypeConfiguration<T>` without forking it:

```csharp
public partial class OrderConfiguration : OrderConfigurationBase
{
    public override void Configure(EntityTypeBuilder<Order> builder)
    {
        base.Configure(builder);
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.Property(o => o.Total).HasPrecision(18, 4);
    }
}
```

Always call `base.Configure(builder)` first.

## Lifecycle Convention Cheat Sheet

| You write | EF Core gets | Why |
|---|---|---|
| `[Composition] List<OrderItem> Items` | `OnDelete(Cascade)` | Owned children die with the parent |
| `[Aggregation] Customer Customer` | `OnDelete(Restrict)` | Referenced; protect from accidental delete |
| `[Association] Currency Currency` | `OnDelete(NoAction)` | Independent lifecycle |

If the default is wrong for one specific relationship:

```csharp
[Composition]
[NavigationProperty("Items", OnDelete = DeleteBehavior.Restrict)]
public List<OrderItem> Items { get; set; } = new();
```

## Composite Primary Keys

```csharp
[Entity]
[PrimaryKey("ProductId", "WarehouseId")]   // class-level, names the columns
public partial class StockLevel
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public int Quantity { get; set; }
}
```

Class-level `[PrimaryKey]` accepts any number of property names. The generated config emits `builder.HasKey(s => new { s.ProductId, s.WarehouseId })`.

## Adding a Custom Behavior

If you need a behavior the package doesn't ship (e.g. `[VersionedConcurrency]`), the pattern to follow:

1. Define the attribute in `MyApp.Persistence.Attributes`
2. Define the marker interface (`IVersionedConcurrency`) in `MyApp.Persistence.Abstractions`
3. Write a small contributor that extends `EntityConfigurationEmitter` (or run a sibling SG that adds a partial)
4. Apply the attribute in DbContext SaveChanges hook generation

The Entity DSL is open to extension via attribute-driven contributors. Forking the emitter is wrong.

## Testing the Persistence Layer

Use an in-memory provider or sqlite. Don't test the generated code itself — test that **your domain rules** survive a round-trip:

```csharp
[Fact]
public async Task Order_round_trips_with_items()
{
    using var ctx = new SalesDbContext(options);
    await ctx.Database.EnsureCreatedAsync();

    ctx.Orders.Add(new Order
    {
        Id = Guid.NewGuid(),
        OrderNumber = "X-100",
        Total = 99.50m,
        Items = { new OrderItem { Id = Guid.NewGuid(), Quantity = 2, UnitPrice = 49.75m } }
    });
    await ctx.SaveChangesAsync();

    var loaded = await ctx.Orders.Include(o => o.Items).SingleAsync();
    Assert.Single(loaded.Items);
}
```

Test the generated emitters separately, in the `Lib` test project, with `Emit(model)` and string assertions.

## Anti-Patterns

| Don't | Why |
|---|---|
| Edit `*.g.cs` files | They are regenerated. Edit the partial stub instead. |
| Reference EF Core from your domain assembly | The whole point of the pattern is to keep EF Core out of the domain. |
| Skip `partial` on entity classes | Generated behavior partials need it. The build will fail. |
| Hand-write `IEntityTypeConfiguration<T>` for entities the SG already configures | Drift will happen. Trust the generator. |
| Add `[Composition]` and then override `OnDelete` everywhere | The default is wrong for your domain — change the convention or use `[Aggregation]` instead. |
| Read `*.Behaviors.g.cs` to know what properties exist | Look at the attributes on the class. The generated members shadow the behavior. |
