# Entity.Dsl — Full EF Core Source Generator Plan

## Context

**Problem**: Configuring EF Core entities requires repetitive, error-prone Fluent API boilerplate. Relationship lifecycle semantics (composition vs aggregation vs association) have no first-class representation and must be manually translated to `DeleteBehavior` settings.

**Goal**: Create an attribute-based DSL that decorates POCO classes to define entities, relationships, and lifecycle semantics. A source generator reads these attributes and emits production-ready EF Core code (`IEntityTypeConfiguration<T>`, DbContext with DbSets, full Fluent API).

**Outcome**: Developers describe *what* their domain model is; the generator produces *how* EF Core configures it. The emit models are a **public contract** from day one, clean enough for DSL-to-DSL generation.

**Location**: `Net/FrenchExDev/Entity.Dsl/` (currently empty skeleton)

---

## 1. Project Decomposition (Clean Architecture)

### Source projects (`Entity.Dsl/src/`) — 5 projects

| Project | TFM | EF Core? | Purpose |
|---------|-----|----------|---------|
| `Entity.Dsl.Attributes` | `netstandard2.0;net10.0` | No | DSL attributes + MetaConcept companions |
| `Entity.Dsl.Abstractions` | `net10.0` | **No** | Pure interfaces: `IRepository<T>`, `IReadOnlyRepository<T>`, `IUnitOfWork`, `ISpecification<T>`, `ICurrentUserProvider`, `SlugHelper`, `PagedResult<T>`, `FakeRepository<T>`, `FakeUnitOfWork` |
| `Entity.Dsl.Infra` | `net10.0` | **Yes** | EF Core implementations: `RepositoryBase<T>`, `IEntityListener<T>`, `IGlobalEntityListener`, `IUnitOfWork<TContext>` |
| `Entity.Dsl.SourceGenerator` | `netstandard2.0` | No | Incremental Roslyn SG (IsRoslynComponent) |
| `Entity.Dsl.SourceGenerator.Lib` | `netstandard2.0` | No | Roslyn-free emitters + public emit models (DSL-to-DSL contract) |

### Test projects (`Entity.Dsl/test/`) — 2 projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Entity.Dsl.Tests` | `net10.0` | Emitter unit tests + attribute/concept tests + diagnostic tests (no EF Core needed) |
| `Entity.Dsl.Integration.Tests` | `net10.0` | End-to-end: SG + SQLite in-memory EF Core + RepositoryBase<T> tests |

### Solution: `Entity.Dsl/FrenchExDev.Net.Entity.Dsl.slnx`

### Dependency Graph

```
                           ┌──────────────┐
                           │  Attributes  │ (netstandard2.0;net10.0)
                           │  [Table],    │
                           │  [PrimaryKey]│
                           └──────┬───────┘
                                  │ references Dsl, Ddd.Attributes, Injectable.Attributes
                                  │
              ┌───────────────────┼───────────────────┐
              │                   │                   │
     ┌────────▼────────┐  ┌──────▼───────┐  ┌───────▼────────┐
     │  Abstractions   │  │    Infra      │  │  SG + SG.Lib   │
     │  (net10.0)      │  │  (net10.0)    │  │ (netstandard2.0)│
     │  NO EF Core     │  │  EF Core      │  │  NO EF Core    │
     │                 │  │               │  │                │
     │ IRepository<T>  │  │ RepositoryBase│  │ Emitters       │
     │ IUnitOfWork     │◄─┤ IEntityList.  │  │ EmitModels     │
     │ ISpecification  │  │ IGlobalList.  │  │ NamingHelper   │
     │ SlugHelper      │  │ IUoW<TCtx>    │  │                │
     │ FakeRepo<T>     │  │               │  │                │
     │ PagedResult<T>  │  │               │  │                │
     └────────┬────────┘  └──────┬────────┘  └───────┬────────┘
              │                   │                   │
              └───────────┬───────┘                   │
                          │                           │
              ┌───────────▼───────────┐   ┌───────────▼───────────┐
              │  Integration.Tests    │   │       Tests           │
              │  (net10.0)            │   │  (net10.0)            │
              │  EF Core SQLite       │   │  NO EF Core           │
              │  RepositoryBase tests │   │  Emitter unit tests   │
              └───────────────────────┘   └───────────────────────┘
```

### Consuming Project References (Clean Architecture)

```
Developer's Domain Layer    ──> Attributes + Abstractions         (zero EF Core)
Developer's Infra Layer     ──> Attributes + Abstractions + Infra (EF Core)
                            ──> SourceGenerator (OutputItemType=Analyzer)
Developer's Test Layer      ──> Abstractions (FakeRepository, FakeUnitOfWork)
```

### Dependencies

```
Attributes ──> FrenchExDev.Net.Dsl (MetaConcept, MetaPropertyAttribute, etc.)
           ──> FrenchExDev.Net.Injectable.Attributes ([Injectable] on generated repos)
           ──  NO dependency on Ddd.Attributes (decoupled — bridge handles DDD mapping)

Abstractions ──> (no dependencies — pure .NET interfaces)

Infra ──> Abstractions
      ──> Microsoft.EntityFrameworkCore

SourceGenerator ──> Lib (linked source files, not project ref)
                ──> Microsoft.CodeAnalysis.CSharp (PrivateAssets=all)
                ──> Microsoft.CodeAnalysis.Analyzers (PrivateAssets=all)

SourceGenerator.Lib ──> (no dependencies — pure string emission)

Tests ──> Attributes, Abstractions, Lib, xunit, Shouldly
Integration.Tests ──> Attributes, Abstractions, Infra, Lib + Microsoft.EntityFrameworkCore.Sqlite
```

### CPM additions (`Directory.Packages.props`)

```xml
<PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.5" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.5" />
```

### Standalone Design — No DDD Dependency

Entity.Dsl is **fully standalone** — no reference to `Ddd.Attributes`. It defines its own entry point:

```csharp
// Entity.Dsl's own entry point — SG discovers classes with this attribute
[MetaConcept(typeof(MappedEntityConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class MappedEntityAttribute : Attribute { }
```

DDD integration is handled by a **separate bridge project**: `Ddd.Entity.Dsl` (see Section 1.1).
The bridge SG reads `[AggregateRoot]`/`[Entity]` from Ddd and auto-generates `[MappedEntity]` on partial classes.

### 1.1 Bridge Project: `Ddd.Entity.Dsl` (separate solution)

```
Ddd.Entity.Dsl/
├── src/
│   ├── FrenchExDev.Net.Ddd.Entity.Dsl.SourceGenerator/      ← bridge SG
│   └── FrenchExDev.Net.Ddd.Entity.Dsl.SourceGenerator.Lib/  ← bridge emitters
├── test/
│   └── FrenchExDev.Net.Ddd.Entity.Dsl.Tests/
└── FrenchExDev.Net.Ddd.Entity.Dsl.slnx
```

**Bridge SG dependencies:**
```
Ddd.Entity.Dsl.SourceGenerator ──> reads Ddd.Attributes FQNs (by string, not project ref)
                               ──> emits Entity.Dsl attributes on partial classes
```

**What the bridge SG maps:**

| DDD Attribute | Bridge Emits (partial class) |
|---|---|
| `[AggregateRoot("Order")]` | `[MappedEntity]` |
| `[Entity("OrderItem")]` | `[MappedEntity]` |
| `[EntityId]` on property | `[PrimaryKey]` |
| `[Composition]` on nav | `OnDelete = "Cascade"` on relationship attr |
| `[Aggregation]` on nav | `OnDelete = "Restrict"` |
| `[Association]` on nav | `OnDelete = "NoAction"` |
| `[ValueObject]` on class | `[Owned]` |
| `[Property(Required=true)]` | `[Required]` |
| `[Property(MaxLength=50)]` | `[MaxLength(50)]` |

**Usage modes:**

1. **Entity.Dsl standalone** (no DDD):
```csharp
[MappedEntity]
[Table("Orders")]
public partial class Order
{
    [PrimaryKey] public Guid Id { get; set; }
    [HasMany(WithOne = "Order", ForeignKey = "OrderId", OnDelete = "Cascade")]
    public List<OrderItem> Items { get; set; } = new();
}
```

2. **Entity.Dsl + DDD** (with bridge):
```csharp
[AggregateRoot("Order")]              // DDD
[Table("Orders", Schema = "sales")]   // Entity.Dsl override
[Timestampable]                       // Entity.Dsl behavior
public partial class Order
{
    [EntityId] public Guid Id { get; set; }                    // DDD → bridge emits [PrimaryKey]
    [Composition]                                               // DDD → bridge emits OnDelete="Cascade"
    [HasMany(WithOne = "Order", ForeignKey = "OrderId")]
    public List<OrderItem> Items { get; set; } = new();
}
// Bridge SG auto-generates: Order.DddBridge.g.cs with [MappedEntity] on partial class
```

---

## 2. Attribute Catalog

### Design Principle: Complement DDD, Don't Duplicate

Existing DDD attributes (`[Entity]`, `[AggregateRoot]`, `[EntityId]`, `[ValueObject]`, `[Composition]`, `[Aggregation]`, `[Association]`) stay in `Ddd.Attributes`. Entity.Dsl attributes add **persistence-specific** EF Core semantics. The SG reads **both** attribute families from the same class.

**Why custom attributes instead of EF Core's built-in `[Table]`, `[Column]`?**
1. Domain layer stays EF-Core-free — no `Microsoft.EntityFrameworkCore` dependency on POCOs
2. MetaConcept validation — compile-time constraint checking via the DSL metamodel
3. DSL-to-DSL readiness — another generator can produce Entity.Dsl attributes programmatically
4. Superset of EF Core annotations — covers Fluent-API-only features like `HasCheckConstraint`, `ToView`, query filters

### 2.1 Class-Level Attributes

#### `[Table]` — EF Core table mapping
```csharp
[MetaConcept(typeof(TableConcept))]
[MetaConstraint("RequiresEntity", nameof(RequiresEntityConstraint),
    Message = "[Table] must be on a class with [Entity] or [AggregateRoot]")]
[AttributeUsage(AttributeTargets.Class)]
public sealed class TableAttribute : Attribute
{
    [MetaProperty("Name", "string")]
    public string? Name { get; set; }              // ToTable("name") — null = class name convention

    [MetaProperty("Schema", "string")]
    public string? Schema { get; set; }             // ToTable("name", "schema")

    public static ConstraintResult RequiresEntityConstraint(ConceptValidationContext ctx)
    {
        foreach (var a in ctx.ClassAttributes)
            if (a.Name == "Entity" || a.Name == "AggregateRoot")
                return ConstraintResult.Satisfied();
        return ConstraintResult.Failed("[Table] requires [Entity] or [AggregateRoot]");
    }
}
```

#### `[View]` — maps entity to a database view (keyless by default)
```csharp
[MetaConcept(typeof(ViewConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class ViewAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; }
    [MetaProperty("Schema", "string")]
    public string? Schema { get; set; }
    public ViewAttribute(string name) { Name = name; }
}
```
Emits: `builder.ToView("vw_name", "schema"); builder.HasNoKey();`

#### `[Keyless]` — explicit HasNoKey() (for raw SQL result types, views)
```csharp
[MetaConcept(typeof(KeylessConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class KeylessAttribute : Attribute { }
```

#### `[Inheritance]` — TPH/TPT/TPC strategy
```csharp
[MetaConcept(typeof(InheritanceConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class InheritanceAttribute : Attribute
{
    [MetaProperty("Strategy", "string", Required = true)]
    public InheritanceStrategy Strategy { get; set; }      // TPH, TPT, TPC
    [MetaProperty("DiscriminatorColumn", "string")]
    public string? DiscriminatorColumn { get; set; }       // TPH only
    [MetaProperty("DiscriminatorValue", "string")]
    public string? DiscriminatorValue { get; set; }        // TPH per-type value
}
public enum InheritanceStrategy { TPH, TPT, TPC }
```

#### `[DbContext]` — marks partial class as generated DbContext
```csharp
[MetaConcept(typeof(DbContextConcept))]
[MetaConstraint("MustBePartial", nameof(MustBePartialConstraint),
    Message = "[DbContext] class must be partial")]
[AttributeUsage(AttributeTargets.Class)]
public sealed class DbContextAttribute : Attribute
{
    [MetaProperty("Name", "string")]
    public string? Name { get; set; }

    /// <summary>
    /// Scopes which entities this DbContext includes.
    /// If set, only entities whose [AggregateRoot(BoundedContext = "...")] matches are included.
    /// If null, all entities in the assembly are included.
    /// </summary>
    [MetaProperty("BoundedContext", "string")]
    public string? BoundedContext { get; set; }

    // ── DbContext-level configuration ──

    /// <summary>Enable lazy loading proxies. Default: false.</summary>
    [MetaProperty("LazyLoading", "bool")]
    public bool LazyLoading { get; set; }

    /// <summary>Default query tracking behavior. Default: "TrackAll".</summary>
    [MetaProperty("QueryTracking", "string")]
    public string? QueryTracking { get; set; }  // "TrackAll", "NoTracking", "NoTrackingWithIdentityResolution"

    /// <summary>Default query splitting behavior. Default: "SingleQuery".</summary>
    [MetaProperty("QuerySplitting", "string")]
    public string? QuerySplitting { get; set; } // "SingleQuery", "SplitQuery"

    /// <summary>Change tracking strategy. Default: "Snapshot".</summary>
    [MetaProperty("ChangeTracking", "string")]
    public string? ChangeTracking { get; set; } // "Snapshot", "ChangingAndChangedNotifications", "ChangedNotifications"

    /// <summary>Enable retry on transient failure. Default: false.</summary>
    [MetaProperty("EnableRetryOnFailure", "bool")]
    public bool EnableRetryOnFailure { get; set; }

    /// <summary>Max retry count for transient failures. Default: 6.</summary>
    [MetaProperty("MaxRetryCount", "int")]
    public int MaxRetryCount { get; set; } = 6;

    public static ConstraintResult MustBePartialConstraint(ConceptValidationContext ctx)
        => ConstraintResult.Satisfied(); // Actual check done in SG via Roslyn (ClassDeclarationSyntax modifiers)
}
```

DbContext-level config emits in the generated `OnConfiguring` override:
```csharp
// In DbContextBase.g.cs:
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    base.OnConfiguring(optionsBuilder);
    optionsBuilder.UseLazyLoadingProxies();                             // if LazyLoading = true
    optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // if QueryTracking set
    optionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery); // if QuerySplitting set
    // EnableRetryOnFailure is provider-specific — emitted as virtual ConfigureResiliency(optionsBuilder) hook
}
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Properties<string>().HaveMaxLength(256);      // example convention
    // ChangeTracking strategy applied per entity in config
}
```

#### `[ShadowProperty]` — properties not on CLR class (AllowMultiple)
```csharp
[MetaConcept(typeof(ShadowPropertyConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ShadowPropertyAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; }
    [MetaProperty("ClrType", "string", Required = true)]
    public string ClrType { get; }
    [MetaProperty("Required", "bool")]
    public bool Required { get; set; }
    [MetaProperty("DefaultValueSql", "string")]
    public string? DefaultValueSql { get; set; }
    public ShadowPropertyAttribute(string name, string clrType) { Name = name; ClrType = clrType; }
}
```

#### `[QueryFilter]` — global query filter
```csharp
[MetaConcept(typeof(QueryFilterConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class QueryFilterAttribute : Attribute
{
    /// <summary>nameof(StaticFilterMethod) — must return Expression&lt;Func&lt;T, bool&gt;&gt;</summary>
    [MetaProperty("FilterMethod", "string", Required = true)]
    public string FilterMethod { get; }
    public QueryFilterAttribute(string filterMethod) { FilterMethod = filterMethod; }
}
```
Emits: `builder.HasQueryFilter(MyEntity.SoftDeleteFilter());`

#### `[CheckConstraint]` — database-level check constraint (AllowMultiple)
```csharp
[MetaConcept(typeof(CheckConstraintConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class CheckConstraintAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; }
    [MetaProperty("Sql", "string", Required = true)]
    public string Sql { get; }
    public CheckConstraintAttribute(string name, string sql) { Name = name; Sql = sql; }
}
```
Emits: `builder.HasCheckConstraint("CK_Price", "\"Price\" > 0");`

#### `[SeedData]` — initial/reference data (AllowMultiple)
```csharp
[MetaConcept(typeof(SeedDataConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class SeedDataAttribute : Attribute
{
    /// <summary>nameof(StaticSeedMethod) — must return IEnumerable&lt;T&gt; or T[]</summary>
    [MetaProperty("SeedMethod", "string", Required = true)]
    public string SeedMethod { get; }
    public SeedDataAttribute(string seedMethod) { SeedMethod = seedMethod; }
}
```
Emits: `builder.HasData(MyEntity.GetSeedData());`

#### `[TemporalTable]` — SQL Server temporal table support
```csharp
[MetaConcept(typeof(TemporalTableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class TemporalTableAttribute : Attribute
{
    [MetaProperty("HistoryTable", "string")]
    public string? HistoryTable { get; set; }
    [MetaProperty("HistorySchema", "string")]
    public string? HistorySchema { get; set; }
}
```
Emits: `builder.ToTable(tb => tb.IsTemporal(t => { ... }));`

#### `[TableSplit]` — multiple entities mapped to same table
```csharp
[MetaConcept(typeof(TableSplitConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class TableSplitAttribute : Attribute
{
    [MetaProperty("TableName", "string", Required = true)]
    public string TableName { get; }
    [MetaProperty("Schema", "string")]
    public string? Schema { get; set; }
    public TableSplitAttribute(string tableName) { TableName = tableName; }
}
```

#### `[NamingConvention]` — column naming strategy (class-level)
```csharp
[MetaConcept(typeof(NamingConventionConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class NamingConventionAttribute : Attribute
{
    [MetaProperty("Strategy", "string", Required = true)]
    public NamingStrategy Strategy { get; set; }   // PascalCase, SnakeCase, CamelCase
}
public enum NamingStrategy { PascalCase, SnakeCase, CamelCase }
```
When `SnakeCase`: the emitter converts all property names to `snake_case` for `HasColumnName()`.

#### `[Comment]` — database table/column documentation (class or property level)
```csharp
[MetaConcept(typeof(CommentConcept))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class CommentAttribute : Attribute
{
    [MetaProperty("Text", "string", Required = true)]
    public string Text { get; }
    public CommentAttribute(string text) { Text = text; }
}
```
Emits: `builder.HasComment("...")` (class) or `builder.Property(e => e.X).HasComment("...")` (property).

#### `[BackingField]` — encapsulated properties with private setters
```csharp
[MetaConcept(typeof(BackingFieldConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class BackingFieldAttribute : Attribute
{
    [MetaProperty("FieldName", "string", Required = true)]
    public string FieldName { get; }
    [MetaProperty("AccessMode", "string")]
    public string? AccessMode { get; set; }  // "Field", "Property", "PreferField", "PreferFieldDuringConstruction"
    public BackingFieldAttribute(string fieldName) { FieldName = fieldName; }
}
```
Emits: `builder.Property(e => e.X).HasField("_x").UsePropertyAccessMode(PropertyAccessMode.Field);`

#### `[Owned]` — class-level: marks a type as always-owned (DDD value object)
```csharp
[MetaConcept(typeof(OwnedConcept))]
[MetaInherits(typeof(ValueObjectConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class OwnedAttribute : Attribute
{
    [MetaProperty("TableName", "string")]
    public string? TableName { get; set; }          // null = same table as owner, set = separate table
    [MetaProperty("JsonColumn", "string")]
    public string? JsonColumn { get; set; }         // ToJson() for EF Core 8+
}
```

**`[Owned]` (class-level) vs `[OwnedEntity]` (property-level):**
- `[Owned]` on the **type itself** → any navigation to this type auto-generates `OwnsOne`/`OwnsMany`
- `[OwnedEntity]` on a **navigation property** → explicit opt-in for a specific navigation
- If the type has `[Owned]`, no need to also put `[OwnedEntity]` on each navigation — the SG infers it
- If the type has `[ValueObject]` (from DDD) **without** `[Owned]`, the SG maps it as `ComplexProperty` instead

| Type Attribute | Navigation Attribute | Result |
|---|---|---|
| `[Owned]` | *(none)* | `OwnsOne`/`OwnsMany` (auto-inferred) |
| `[Owned(JsonColumn="x")]` | *(none)* | `OwnsOne` + `ToJson("x")` |
| *(none)* | `[OwnedEntity]` | `OwnsOne`/`OwnsMany` (explicit) |
| `[ValueObject]` | *(none)* | `ComplexProperty` (EF Core 8+) |
| `[ValueObject]` | `[ComplexType]` | `ComplexProperty` (explicit) |

#### `[EntitySplit]` — one entity mapped across multiple tables (AllowMultiple)
```csharp
[MetaConcept(typeof(EntitySplitConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class EntitySplitAttribute : Attribute
{
    [MetaProperty("TableName", "string", Required = true)]
    public string TableName { get; }
    [MetaProperty("Schema", "string")]
    public string? Schema { get; set; }
    [MetaProperty("Properties", "string", Required = true)]
    public string Properties { get; }   // Comma-separated property names mapped to this table
    public EntitySplitAttribute(string tableName, string properties) { TableName = tableName; Properties = properties; }
}
```
Emits: `builder.SplitToTable("SecondTable", tb => { tb.Property(e => e.X); tb.Property(e => e.Y); });`

**`[TableSplit]` vs `[EntitySplit]`:**
| | `[TableSplit]` | `[EntitySplit]` |
|---|---|---|
| Direction | Multiple entities → one table | One entity → multiple tables |
| Use case | Narrow views onto a wide table | Wide entity split for perf |

#### `[StoredProcedure]` — map CUD operations to stored procedures
```csharp
[MetaConcept(typeof(StoredProcedureConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class StoredProcedureAttribute : Attribute
{
    [MetaProperty("InsertProcedure", "string")]
    public string? InsertProcedure { get; set; }    // InsertUsingStoredProcedure("sp_name")
    [MetaProperty("UpdateProcedure", "string")]
    public string? UpdateProcedure { get; set; }    // UpdateUsingStoredProcedure("sp_name")
    [MetaProperty("DeleteProcedure", "string")]
    public string? DeleteProcedure { get; set; }    // DeleteUsingStoredProcedure("sp_name")
}
```
Emits:
```csharp
builder.InsertUsingStoredProcedure("sp_InsertOrder", sp => { sp.HasParameter(e => e.Id); ... });
builder.UpdateUsingStoredProcedure("sp_UpdateOrder", sp => { ... });
builder.DeleteUsingStoredProcedure("sp_DeleteOrder", sp => { ... });
```

#### `[Trigger]` — declare database triggers (EF Core 7+, AllowMultiple)
```csharp
[MetaConcept(typeof(TriggerConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TriggerAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; }
    public TriggerAttribute(string name) { Name = name; }
}
```
Emits: `builder.ToTable(tb => tb.HasTrigger("trg_name"));`
**Why this matters:** EF Core 7+ needs trigger declarations for correct SaveChanges behavior (OUTPUT clause vs SELECT after INSERT).

### 2.2 Property-Level Attributes

| Attribute | Key Properties | EF Core Call |
|-----------|---------------|-------------|
| `[Column]` | `Name?`, `TypeName?`, `Order` | `HasColumnName()`, `HasColumnType()`, `HasColumnOrder()` |
| `[PrimaryKey]` (inherits EntityIdConcept) | `Order`, `ValueGenerated` | `HasKey()`, `ValueGeneratedOnAdd/OnUpdate/Never()` |
| `[AlternateKey]` | `GroupName?` | `HasAlternateKey()` (grouped by GroupName) |
| `[Index]` (AllowMultiple) | `Name?`, `IsUnique`, `GroupName?`, `IsDescending?` | `HasIndex().IsUnique().HasDatabaseName().IsDescending()` |
| `[Precision]` | `P`, `S` (ctor) | `HasPrecision(p, s)` |
| `[DefaultValue]` | `Value?`, `Sql?` (mutually exclusive constraint) | `HasDefaultValue()` / `HasDefaultValueSql()` |
| `[Conversion]` | `ConverterType` | `HasConversion<T>()` |
| `[EnumStorage]` | `AsString` (bool, default true) | `HasConversion<string>()` or no conversion |
| `[ConcurrencyToken]` | `IsRowVersion` | `IsConcurrencyToken()` / `IsRowVersion()` |
| `[ComputedColumn]` | `Sql`, `Stored` (bool) | `HasComputedColumnSql("...", stored)` |
| `[Sequence]` | `Name`, `Schema?`, `StartsAt?`, `IncrementsBy?` | `HasSequence().StartsAt().IncrementsBy()` + `UseSequence()` |
| `[Required]` | *(none)* | `.IsRequired()` — standalone, no DDD `[Property]` dependency |
| `[MaxLength]` | `Length` (ctor) | `.HasMaxLength(n)` — standalone, no DDD `[Property]` dependency |
| `[BackingField]` | `FieldName`, `AccessMode?` | `.HasField("_x").UsePropertyAccessMode(...)` — for encapsulated DDD entities |
| `[Comment]` | `Text` (ctor) | `.HasComment("...")` — database column/table documentation |
| `[AutoInclude]` | *(none)* | `.Navigation(e => e.X).AutoInclude()` — always eagerly loaded |
| `[ValueGenerator]` | `GeneratorType` | `.HasValueGenerator<T>()` — custom ID/value generation |
| `[Collation]` | `Name` (ctor) | `.UseCollation("...")` — text sorting/comparison |
| `[HiLo]` | `SequenceName`, `Schema?` | `.UseHiLo("seq")` — batch-friendly key generation |
| `[ValueComparer]` | `ComparerType` | `.Metadata.SetValueComparer(new T())` — change tracking for complex values |
| `[NotMapped]` | *(none)* | Property excluded from EF Core mapping — domain-layer alternative to `System.ComponentModel.DataAnnotations.Schema.NotMapped` |
| `[ValueGeneratedOnAdd]` | *(none)* | `.ValueGeneratedOnAdd()` for non-key columns (e.g., identity columns, DB defaults) |
| `[Encrypted]` | `ConverterType?` | Auto-encrypt on write, decrypt on read via `HasConversion`. Uses `IEncryptionProvider` from Abstractions. GDPR/PII compliance |
| `[Cacheable]` | `Duration` (TimeSpan), `Strategy` | Mark entity for second-level caching. SG generates cache-aside in RepositoryBase. Uses `ICacheProvider` from Abstractions |
| `[DatabaseProvider]` | `Provider` (string) | Marks property/class config as provider-specific. SG wraps in `if (Database.IsXxx())` check. Values: `"SqlServer"`, `"PostgreSql"`, `"Sqlite"` |

#### `[PrimaryKey]` full definition
```csharp
[MetaConcept(typeof(PrimaryKeyConcept))]
[MetaInherits(typeof(EntityIdConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class PrimaryKeyAttribute : Attribute
{
    [MetaProperty("Order", "int")]
    public int Order { get; set; } = 0;                    // For composite keys — sorted ascending
    [MetaProperty("ValueGenerated", "string")]
    public ValueGeneration ValueGenerated { get; set; } = ValueGeneration.OnAdd;
}
public enum ValueGeneration { None, OnAdd, OnUpdate, OnAddOrUpdate }
```

#### `[ComputedColumn]` — database-computed column
```csharp
[MetaConcept(typeof(ComputedColumnConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class ComputedColumnAttribute : Attribute
{
    [MetaProperty("Sql", "string", Required = true)]
    public string Sql { get; }
    [MetaProperty("Stored", "bool")]
    public bool Stored { get; set; }
    public ComputedColumnAttribute(string sql) { Sql = sql; }
}
```
Emits: `builder.Property(e => e.Prop).HasComputedColumnSql("sql", stored: true);`

#### `[EnumStorage]` — enum-to-string or enum-to-int
```csharp
[MetaConcept(typeof(EnumStorageConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class EnumStorageAttribute : Attribute
{
    [MetaProperty("AsString", "bool")]
    public bool AsString { get; set; } = true;
}
```
Emits: `builder.Property(e => e.Status).HasConversion<string>();` (when AsString=true)

#### `[Sequence]` — key generation via database sequence
```csharp
[MetaConcept(typeof(SequenceConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class SequenceAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; }
    [MetaProperty("Schema", "string")]
    public string? Schema { get; set; }
    [MetaProperty("StartsAt", "long")]
    public long StartsAt { get; set; } = 1;
    [MetaProperty("IncrementsBy", "int")]
    public int IncrementsBy { get; set; } = 1;
    public SequenceAttribute(string name) { Name = name; }
}
```

#### `[AutoInclude]` — always eagerly load this navigation
```csharp
[MetaConcept(typeof(AutoIncludeConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class AutoIncludeAttribute : Attribute { }
```
Emits: `builder.Navigation(e => e.Address).AutoInclude();`

#### `[ValueGenerator]` — custom value generation
```csharp
[MetaConcept(typeof(ValueGeneratorConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class ValueGeneratorAttribute : Attribute
{
    [MetaProperty("GeneratorType", "Type", Required = true)]
    public Type GeneratorType { get; }
    public ValueGeneratorAttribute(Type generatorType) { GeneratorType = generatorType; }
}
```
Emits: `builder.Property(e => e.Code).HasValueGenerator<MyGenerator>();`

#### `[Collation]` — text sorting/comparison
```csharp
[MetaConcept(typeof(CollationConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class CollationAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; }
    public CollationAttribute(string name) { Name = name; }
}
```
Emits: `builder.Property(e => e.Name).UseCollation("SQL_Latin1_General_CP1_CI_AS");`

#### `[HiLo]` — batch-friendly key generation
```csharp
[MetaConcept(typeof(HiLoConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class HiLoAttribute : Attribute
{
    [MetaProperty("SequenceName", "string")]
    public string? SequenceName { get; set; }
    [MetaProperty("Schema", "string")]
    public string? Schema { get; set; }
}
```
Emits: `builder.Property(e => e.Id).UseHiLo("seq_name", "schema");`

#### `[ValueComparer]` — custom change tracking comparer
```csharp
[MetaConcept(typeof(ValueComparerConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class ValueComparerAttribute : Attribute
{
    [MetaProperty("ComparerType", "Type", Required = true)]
    public Type ComparerType { get; }
    public ValueComparerAttribute(Type comparerType) { ComparerType = comparerType; }
}
```
Emits: `builder.Property(e => e.Tags).Metadata.SetValueComparer(new MyComparer());`
**When needed:** Custom value types, JSON-stored collections, or any property where default Equals/GetHashCode doesn't correctly detect changes.

#### `[TranslatableProperty]` — marks a property for multi-language support
```csharp
[MetaConcept(typeof(TranslatablePropertyConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class TranslatablePropertyAttribute : Attribute { }
```
Used on properties of entities that have the class-level `[Translatable]` behavior attribute. The SG generates a companion `{Entity}Translation` entity with one column per `[TranslatableProperty]` property + `Locale` + FK to parent.

**Naming**: Class-level = `[Translatable]` (the behavior). Property-level = `[TranslatableProperty]` (which fields are translatable). No collision.

### 2.3 Validation Attributes

The user asked for "static methods that can be used in attributes to validate entities, fields of entities."

There are **two levels** of validation, both expressed via `nameof()` pointing to static methods:

#### Compile-time validation (MetaConcept constraints — SG diagnostics)

These run during source generation. The SG calls the static method with a `ConceptValidationContext` and emits a compiler error/warning if it fails.

Already handled by `[MetaConstraint]` on the attributes themselves — e.g., `[Table]` requires `[Entity]`.

#### Design-time entity validation (`[Validate]`) — static methods on the entity class

```csharp
[MetaConcept(typeof(ValidateConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class ValidateAttribute : Attribute
{
    /// <summary>
    /// nameof(StaticMethod). Signature: static ConstraintResult MethodName(ConceptValidationContext ctx)
    /// Called by the SG at compile-time to validate the entity's structure.
    /// </summary>
    [MetaProperty("Method", "string", Required = true)]
    public string Method { get; }
    public ValidateAttribute(string method) { Method = method; }
}
```

#### Per-property design-time validation (`[ValidateProperty]`)

```csharp
[MetaConcept(typeof(ValidatePropertyConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class ValidatePropertyAttribute : Attribute
{
    /// <summary>
    /// nameof(StaticMethod). Signature: static ConstraintResult MethodName(ConceptValidationContext ctx)
    /// ctx contains the property info being validated.
    /// </summary>
    [MetaProperty("Method", "string", Required = true)]
    public string Method { get; }
    public ValidatePropertyAttribute(string method) { Method = method; }
}
```

**Validation method signature contract:**
```csharp
// Entity-level
public static ConstraintResult ValidateOrder(ConceptValidationContext ctx) { ... }

// Property-level
public static ConstraintResult ValidateTotal(ConceptValidationContext ctx) { ... }
```

The SG calls these at generation time. If `ConstraintResult.Failed(...)`, the SG emits a `DiagnosticSeverity.Error` with the message.

**Relationship to DDD `[Invariant]`:** Invariants are **runtime** domain rules (generated `EnsureInvariants()` method). Validate/ValidateProperty are **compile-time** structural checks on the DSL model itself.

### 2.4 Relationship Attributes

#### Convention-based inference (implicit relationships)

The SG **infers** relationship direction from property types **without requiring explicit attributes**:

| Property Type | Inference |
|---|---|
| `T` where T has `[Entity]` | `HasOne<T>` (reference navigation) |
| `ICollection<T>` / `List<T>` / `IList<T>` where T has `[Entity]` | `HasMany<T>` (collection navigation) |
| Nullable `T?` where T has `[Entity]` | `HasOne<T>` optional (IsRequired = false) |

**Attributes are for overrides only** — you only need `[HasOne]`, `[HasMany]`, etc. when the convention doesn't give you what you want (custom FK name, specific delete behavior, non-obvious inverse).

#### `[HasOne]` — override reference navigation config
```csharp
[MetaConcept(typeof(HasOneConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class HasOneAttribute : Attribute
{
    [MetaProperty("WithMany", "string")]
    public string? WithMany { get; set; }           // inverse collection name (null → WithOne)
    [MetaProperty("ForeignKey", "string")]
    public string? ForeignKey { get; set; }
    [MetaProperty("PrincipalKey", "string")]
    public string? PrincipalKey { get; set; }
    [MetaProperty("OnDelete", "string")]
    public string? OnDelete { get; set; }            // "Cascade","Restrict","SetNull","NoAction","ClientCascade","ClientSetNull"
    [MetaProperty("IsRequired", "bool")]
    public bool? IsRequired { get; set; }            // null = infer from nullability
}
```

**`OnDelete` as string**: Avoids an EF Core dependency in the attributes assembly. The emitter maps to `Microsoft.EntityFrameworkCore.DeleteBehavior.*`.

#### `[HasMany]` — override collection navigation config
```csharp
[MetaConcept(typeof(HasManyConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class HasManyAttribute : Attribute
{
    [MetaProperty("WithOne", "string")]
    public string? WithOne { get; set; }            // inverse reference name
    [MetaProperty("ForeignKey", "string")]
    public string? ForeignKey { get; set; }
    [MetaProperty("OnDelete", "string")]
    public string? OnDelete { get; set; }
}
```

#### `[ManyToMany]` — explicit M:M with optional association class
```csharp
[MetaConcept(typeof(ManyToManyConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class ManyToManyAttribute : Attribute
{
    [MetaProperty("JoinEntity", "Type")]
    public Type? JoinEntity { get; set; }           // UsingEntity<T>() — association class with payload
    [MetaProperty("LeftForeignKey", "string")]
    public string? LeftForeignKey { get; set; }
    [MetaProperty("RightForeignKey", "string")]
    public string? RightForeignKey { get; set; }
    [MetaProperty("JoinTable", "string")]
    public string? JoinTable { get; set; }          // table name for implicit join entity
}
```

#### `[OwnedEntity]` — OwnsOne/OwnsMany (inherits CompositionConcept)
```csharp
[MetaConcept(typeof(OwnedEntityConcept))]
[MetaInherits(typeof(CompositionConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class OwnedEntityAttribute : Attribute
{
    [MetaProperty("TableName", "string")]
    public string? TableName { get; set; }          // null = same table (flattened), set = separate table
    [MetaProperty("JsonColumn", "string")]
    public string? JsonColumn { get; set; }         // ToJson("col") for EF Core 8+ JSON columns
}
```

#### `[ComplexType]` — EF Core 8+ complex types for `[ValueObject]`
```csharp
[MetaConcept(typeof(ComplexTypeConcept))]
[MetaInherits(typeof(ValueObjectConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class ComplexTypeAttribute : Attribute { }
```
Emits: `builder.ComplexProperty(e => e.Address);`

**`[OwnedEntity]` vs `[ComplexType]`:**
| | OwnedEntity | ComplexType |
|---|---|---|
| Has identity | Yes (via owner's key) | No |
| Can be null | Yes | No |
| Can have navigation properties | Yes | No |
| Table mapping | Same table or separate | Same table only |
| DDD mapping | Composition | ValueObject |

#### `[SelfReference]` — self-referencing tree/hierarchy
```csharp
[MetaConcept(typeof(SelfReferenceConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class SelfReferenceAttribute : Attribute
{
    [MetaProperty("InverseNavigation", "string")]
    public string? InverseNavigation { get; set; }   // "Children" or "Parent"
    [MetaProperty("ForeignKey", "string")]
    public string? ForeignKey { get; set; }          // "ParentId"
    [MetaProperty("OnDelete", "string")]
    public string? OnDelete { get; set; }            // default: Restrict (prevent cycles)
}
```

Example:
```csharp
[Entity("Category")]
[Table("Categories")]
public class Category
{
    [PrimaryKey] public int Id { get; set; }
    public string Name { get; set; } = "";

    [SelfReference(InverseNavigation = "Children", ForeignKey = "ParentId", OnDelete = "Restrict")]
    public Category? Parent { get; set; }

    public List<Category> Children { get; set; } = new();
}
```

### 2.5 Association Classes — `[AssociationClass]` Attribute

An association class is a **specialized entity** that materializes a M:M relationship and carries payload. It is a first-class DSL concept, not a workaround.

#### `[AssociationClass]` — derives from `[Entity]` at the metamodel level
```csharp
[MetaConcept(typeof(AssociationClassConcept))]
[MetaInherits(typeof(EntityConcept))]              // IS-A Entity
[MetaConstraint("MustHaveTwoEndpoints", nameof(MustHaveTwoEndpointsConstraint),
    Message = "[AssociationClass] must specify both A and B endpoint types")]
[MetaConstraint("EndpointsMustBeEntities", nameof(EndpointsMustBeEntitiesConstraint),
    Message = "[AssociationClass] endpoints must be [Entity] or [AggregateRoot] types")]
[AttributeUsage(AttributeTargets.Class)]
public sealed class AssociationClassAttribute : Attribute
{
    [MetaProperty("Name", "string", Required = true)]
    public string Name { get; }

    [MetaProperty("Left", "Type", Required = true)]
    public Type Left { get; }                           // Left endpoint entity type

    [MetaProperty("Right", "Type", Required = true)]
    public Type Right { get; }                          // Right endpoint entity type

    [MetaProperty("LeftMultiplicity", "string")]
    public string LeftMultiplicity { get; set; } = "*"; // "1", "*", "0..1"

    [MetaProperty("RightMultiplicity", "string")]
    public string RightMultiplicity { get; set; } = "*";// "1", "*", "0..1"

    [MetaProperty("OnDeleteLeft", "string")]
    public string? OnDeleteLeft { get; set; }           // DeleteBehavior for Left endpoint

    [MetaProperty("OnDeleteRight", "string")]
    public string? OnDeleteRight { get; set; }          // DeleteBehavior for Right endpoint

    public AssociationClassAttribute(string name, Type left, Type right)
    {
        Name = name; Left = left; Right = right;
    }

    public static ConstraintResult MustHaveTwoEndpointsConstraint(ConceptValidationContext ctx) { ... }
    public static ConstraintResult EndpointsMustBeEntitiesConstraint(ConceptValidationContext ctx) { ... }
}
```

**What the SG auto-generates from `[AssociationClass]`:**

1. **Composite PK** from `{A}Id` + `{B}Id` (or from explicit `[PrimaryKey]` if developer overrides)
2. **FK navigation properties** to both endpoints (if not already declared)
3. **Skip navigations** on both endpoint entities (injected via SG partial classes)
4. **`IEntityTypeConfiguration<T>`** with full relationship wiring
5. **Repository interface** `IAssociationRepository<TAssoc, TA, TB>` with specialized queries

#### Usage Example

```csharp
// The association class — a specialized entity
[AssociationClass("Enrollment", typeof(Student), typeof(Course),
    OnDeleteLeft = "Cascade", OnDeleteRight = "Cascade")]
[Table("Enrollments")]
public partial class Enrollment
{
    // FK properties — auto-detected by convention ({EndpointName}Id)
    public int StudentId { get; set; }
    public int CourseId { get; set; }

    // Navigations — auto-generated if missing, but can be declared explicitly
    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;

    // Payload — this is what makes it an association CLASS, not just a join table
    [Property("Grade")] public string? Grade { get; set; }
    [Property("EnrolledAt", Required = true)]
    [DefaultValue(Sql = "GETUTCDATE()")]
    public DateTimeOffset EnrolledAt { get; set; }
}

// Endpoints — SG auto-generates skip navigation + direct navigation as partial class
[AggregateRoot("Student")]
[Table("Students")]
public partial class Student
{
    [PrimaryKey] public int Id { get; set; }
    [Property("Name", Required = true)] public string Name { get; set; } = "";
    // SG generates: public ICollection<Course> Courses (skip nav)
    // SG generates: public ICollection<Enrollment> Enrollments (direct nav)
}

[AggregateRoot("Course")]
[Table("Courses")]
public partial class Course
{
    [PrimaryKey] public int Id { get; set; }
    [Property("Title", Required = true)] public string Title { get; set; } = "";
    // SG generates: public ICollection<Student> Students (skip nav)
    // SG generates: public ICollection<Enrollment> Enrollments (direct nav)
}
```

#### Generated Code

**`Student.AssociationNavigations.g.cs`** (auto-generated partial):
```csharp
// <auto-generated/>
namespace MyApp.Domain;

public partial class Student
{
    public ICollection<global::MyApp.Domain.Course> Courses { get; set; } = new List<global::MyApp.Domain.Course>();
    public ICollection<global::MyApp.Domain.Enrollment> Enrollments { get; set; } = new List<global::MyApp.Domain.Enrollment>();
}
```

**`EnrollmentConfigurationBase.g.cs`:**
```csharp
protected virtual void ConfigurePrimaryKey(EntityTypeBuilder<Enrollment> builder)
{
    builder.HasKey(e => new { e.StudentId, e.CourseId });
}

protected virtual void ConfigureStudentEndpoint(EntityTypeBuilder<Enrollment> builder)
{
    builder.HasOne(e => e.Student)
        .WithMany(e => e.Enrollments)
        .HasForeignKey(e => e.StudentId)
        .OnDelete(DeleteBehavior.Cascade);
}

protected virtual void ConfigureCourseEndpoint(EntityTypeBuilder<Enrollment> builder)
{
    builder.HasOne(e => e.Course)
        .WithMany(e => e.Enrollments)
        .HasForeignKey(e => e.CourseId)
        .OnDelete(DeleteBehavior.Cascade);
}
```

**`StudentConfigurationBase.g.cs`** (skip navigation injected):
```csharp
protected virtual void ConfigureCourses(EntityTypeBuilder<Student> builder)
{
    builder.HasMany(e => e.Courses)
        .WithMany(e => e.Students)
        .UsingEntity<global::MyApp.Domain.Enrollment>();
}
```

#### `[AssociationClass]` vs plain `[Entity]` with `[ManyToMany]`

| | `[AssociationClass(A, B)]` | `[Entity]` + manual config |
|---|---|---|
| Intent | Explicit M:M with payload | Just another entity |
| PK generation | Auto composite from endpoints | Manual `[PrimaryKey]` |
| Skip navs | Auto-generated on both endpoints | Manual declaration |
| Endpoint FK navs | Auto-generated if missing | Manual declaration |
| Repository | Specialized `IAssociationRepository<,,>` | Standard `IRepository<T>` |
| Validation | Enforces 2 endpoints exist + are entities | None |
| DSL-to-DSL | Clearly identifiable as association | Ambiguous |

### 2.6 Lifecycle → DeleteBehavior Convention

When no explicit `OnDelete` is set on the relationship attribute, the SG infers from the DDD attribute on the same property:

| DDD Attribute | Default DeleteBehavior | Rationale |
|---|---|---|
| `[Composition]` | `Cascade` | Parent owns child lifecycle completely |
| `[Aggregation]` | `Restrict` | Shared reference — prevent accidental orphan |
| `[Association]` | `NoAction` | Independent lifecycle — no cascade |
| *(none)* | Infer from nullability | Nullable nav → `ClientSetNull`, non-nullable → `Cascade` (EF Core default) |

### 2.7 Attribute Conflict Resolution Rules

| Conflict | Resolution |
|---|---|
| `[Column(Name="x")]` + `[NamingConvention(SnakeCase)]` | Explicit `[Column]` wins — convention only applies to properties without explicit column names |
| `[Table]` + `[View]` | SG diagnostic `EDSL0006` error — mutually exclusive |
| `[OwnedEntity]` + `[Aggregation]` | SG diagnostic `EDSL0013` error — owned = composition semantics only |
| `[Required]` + DDD `[Property(Required=true)]` | Either one → `IsRequired()`. No conflict, additive |
| `[MaxLength(50)]` + DDD `[Property(MaxLength=100)]` | Entity.Dsl `[MaxLength]` wins — more specific DSL overrides DDD |
| `[PrimaryKey]` + `[EntityId]` | `[PrimaryKey]` inherits `EntityIdConcept` — they're the same thing. If both present, `[PrimaryKey]` config is used |
| `[Keyless]` + `[PrimaryKey]` | SG diagnostic error — contradictory |
| `[ComputedColumn]` + `[DefaultValue]` | SG diagnostic error — computed columns can't have defaults |
| `[BackingField]` + `[ComputedColumn]` | SG diagnostic warning — computed columns are read-only, backing field is irrelevant |
| `[HiLo]` + `[Sequence]` | SG diagnostic error — mutually exclusive key generation strategies |
| `[HiLo]` + `[ValueGenerator]` | SG diagnostic error — only one value generation strategy per property |
| `[AutoInclude]` on non-navigation | SG diagnostic error — only valid on navigation properties |
| `[Translatable]` without `ITranslatable` | SG diagnostic error — entity must implement `ITranslatable` |
| `[EntitySplit]` + `[TableSplit]` on same class | SG diagnostic error — contradictory splitting directions |
| `[StoredProcedure]` + `[Trigger]` | Allowed — triggers fire on SP execution too |
| `[LazyLoading]` + `[AutoInclude]` | Allowed but SG emits info diagnostic — AutoInclude overrides lazy for that navigation |
| `[NotMapped]` + `[Column]`/`[Index]`/etc. | `[NotMapped]` wins — SG emits warning, skips property config |
| `[NotMapped]` + `[PrimaryKey]` | SG diagnostic error — PK cannot be unmapped |

### 2.8 Bidirectional Relationship Consistency

**Problem**: If both sides of a relationship are decorated, the SG could emit conflicting config.

**Rule — Principal wins**: The SG emits relationship configuration **only on the principal side** (the side that declares `HasOne`/`HasMany`). The dependent side's attributes are used for validation only (verifying FK names match, etc.).

**How to determine principal:**
1. If `[HasOne(WithMany=...)]` → the class declaring this is the dependent, the `WithMany` target is principal
2. If `[HasMany(WithOne=...)]` → the class declaring this is the principal
3. If both sides have attributes → the side with `HasMany` wins (collection = principal)
4. If ambiguous → SG emits diagnostic `EDSL0010: Ambiguous relationship principal`

---

## 3. Source Generator Design

### File: `EntityDslGenerator.cs` (IIncrementalGenerator)

**Discovery pipeline:**
```
1. ForAttributeWithMetadataName("FrenchExDev.Net.Entity.Dsl.Attributes.MappedEntityAttribute")
   + ForAttributeWithMetadataName("FrenchExDev.Net.Entity.Dsl.Attributes.AssociationClassAttribute")
   → merge → IncrementalValuesProvider<EntityEmitModel>
   → emit per-entity: ConfigurationBase.g.cs, Configuration.g.cs, ConfigurationRegistration.g.cs
   → emit per-entity: IRepository.g.cs, Repository.g.cs
   → emit per-association: skip navigation partials on Left/Right endpoint entities
   NOTE: [MappedEntity] is Entity.Dsl's own entry point. DDD users get it via bridge SG.

2. ForAttributeWithMetadataName("FrenchExDev.Net.Entity.Dsl.Attributes.DbContextAttribute")
   + collected entities
   → emit: DbContextBase.g.cs, DbContext.g.cs
   → emit: IUnitOfWork.g.cs, UnitOfWorkBase.g.cs, UnitOfWork.g.cs
   → emit: DbContextRegistration.g.cs (AddDbContext extension)
   → emit: Conventions.g.cs (naming, soft delete, etc.)

3. Behavior attribute detection (per entity class):
   - [Timestampable] → generate CreatedAt/UpdatedAt properties + config + auto-populate
   - [SoftDeletable] → generate IsDeleted/DeletedAt + query filter + intercept Remove
   - [Blameable] → generate CreatedBy/UpdatedBy + resolve ICurrentUserProvider from DI
   - [Versionable] → generate RowVersion + IsRowVersion() config
   - [Sluggable("Source")] → generate Slug + compute on Add/Modify
   - [Loggable] → generate companion AuditLog entity
   - [Translatable] → generate companion Translation entity
   - [Sortable] → generate Position + auto-manage ordering
   - [TreeNode] → generate ParentId/MaterializedPath/Depth + auto-maintain

4. ForAttributeWithMetadataName("FrenchExDev.Net.Entity.Dsl.Attributes.EntityListenerAttribute")
   → collect per entity → generate dispatcher in DbContextBase
```

**Model extraction** (Roslyn → Roslyn-free emit models):
- Read class-level attrs: `[Table]`, `[View]`, `[Keyless]`, `[Owned]`, `[Inheritance]`, `[ShadowProperty]`, `[QueryFilter]`, `[CheckConstraint]`, `[SeedData]`, `[TemporalTable]`, `[TableSplit]`, `[EntitySplit]`, `[NamingConvention]`, `[StoredProcedure]`, `[Trigger]`, `[Validate]`, `[Comment]`, `[EntityListener]`
- Read property-level attrs: `[Column]`, `[PrimaryKey]`, `[Index]`, `[AlternateKey]`, `[Precision]`, `[DefaultValue]`, `[Conversion]`, `[EnumStorage]`, `[ConcurrencyToken]`, `[ComputedColumn]`, `[Sequence]`, `[AutoInclude]`, `[ValueGenerator]`, `[Collation]`, `[HiLo]`, `[ValueComparer]`, `[TranslatableProperty]`, `[ValidateProperty]`, `[Required]`, `[MaxLength]`, `[BackingField]`, `[Comment]`, `[NotMapped]`
- Read relationship attrs: `[HasOne]`, `[HasMany]`, `[ManyToMany]`, `[OwnedEntity]`, `[ComplexType]`, `[SelfReference]`
- Read `[AssociationClass]` class-level: extract Left/Right endpoint types, multiplicities, delete behaviors
- Read DDD attrs on same property: `[Composition]`/`[Aggregation]`/`[Association]` → infer DeleteBehavior
- Read DDD `[Property]` for Required/MaxLength (Entity.Dsl `[Required]`/`[MaxLength]` override if both present)
- **Behavior attributes**: Detect `[Timestampable]`, `[SoftDeletable]`, `[Blameable]`, `[Versionable]`, `[Sluggable]`, `[Loggable]`, `[Translatable]`, `[Sortable]`, `[TreeNode]` → set flags + config on emit model
- **Convention inference**: For properties with no relationship attribute, inspect type — if it's an `[Entity]`-decorated type or `ICollection<[Entity]>`, auto-infer the relationship

### 3.1 SG Implementation Note: `typeof()` in Attributes

Several attributes use `Type` properties: `[AssociationClass(typeof(A), typeof(B))]`, `[ValueGenerator(typeof(...))]`, `[Conversion(typeof(...))]`, `[ValueComparer(typeof(...))]`, `[EntityListener(typeof(...))]`.

In Roslyn, `typeof()` attribute arguments are read via `TypedConstant.Value` as `INamedTypeSymbol`. The SG extracts the **fully-qualified type name as a string** for the emit model. The emitter then uses this string in generated code (e.g., `new global::MyApp.MyGenerator()`). The SG never instantiates or references the actual `Type` at generation time.

### 3.2 Diagnostic Catalog

The SG emits diagnostics (errors/warnings) for invalid attribute usage:

| ID | Severity | Condition |
|---|---|---|
| `EDSL0001` | Error | `[PrimaryKey]` on a navigation property |
| `EDSL0002` | Error | `[HasMany]` on a non-collection property |
| `EDSL0003` | Error | `[HasOne]` on a collection property |
| `EDSL0004` | Error | `[DefaultValue]` with both `Value` and `Sql` set |
| `EDSL0005` | Error | `[DbContext]` on a non-partial class |
| `EDSL0006` | Error | `[View]` and `[Table]` on the same class |
| `EDSL0007` | Warning | Entity has no `[PrimaryKey]` and no `[EntityId]` (will use convention) |
| `EDSL0008` | Error | `[Inheritance(TPH)]` base class not decorated with `[Entity]` |
| `EDSL0009` | Error | `[Validate]`/`[ValidateProperty]` method not found or wrong signature |
| `EDSL0010` | Error | Ambiguous relationship principal — both sides declare ownership |
| `EDSL0011` | Warning | `[ComplexType]` on nullable property (complex types cannot be null) |
| `EDSL0012` | Error | `[SelfReference]` on property whose type differs from declaring class |
| `EDSL0013` | Error | `[OwnedEntity]` + `[Aggregation]` on same property (owned = composition only) |
| `EDSL0014` | Warning | `[Composition]`/`[Aggregation]` without relationship attribute — using convention |
| `EDSL0015` | Error | `[Keyless]` + `[PrimaryKey]` on same class — contradictory |
| `EDSL0016` | Error | `[ComputedColumn]` + `[DefaultValue]` on same property — contradictory |
| `EDSL0017` | Warning | `[BackingField]` on `[ComputedColumn]` property — irrelevant (read-only) |
| `EDSL0018` | Error | `[AssociationClass]` endpoint type not decorated with `[Entity]` or `[AggregateRoot]` |
| `EDSL0019` | Error | `[AssociationClass]` missing both endpoint FK properties (`{Left}Id`, `{Right}Id`) |
| `EDSL0020` | Error | `[TranslatableProperty]` on property of entity without class-level `[Translatable]` behavior |
| `EDSL0021` | Error | `[AutoInclude]` on non-navigation property |
| `EDSL0022` | Error | `[EntitySplit]` references unknown property names |
| `EDSL0023` | Warning | `[StoredProcedure]` with no procedures specified (at least one required) |
| `EDSL0024` | Error | `[HiLo]` and `[Sequence]` on same property — mutually exclusive key strategies |
| `EDSL0025` | Error | `[ValueGenerator]` type does not inherit `ValueGenerator<T>` |
| `EDSL0026` | Warning | `[Column]` or `[Index]` on a `[NotMapped]` property — conflicting, `[NotMapped]` wins |
| `EDSL0027` | Error | `[NotMapped]` on a `[PrimaryKey]` property — primary key cannot be unmapped |

#### Cross-Entity Relationship Validation

| ID | Severity | Condition |
|---|---|---|
| `EDSL0100` | Error | FK property type mismatch — e.g. `OrderId` is `int` but `Order.Id` is `Guid` |
| `EDSL0101` | Error | Navigation references a type not decorated with `[Entity]`/`[AggregateRoot]`/`[AssociationClass]` |
| `EDSL0102` | Error | `[HasMany(WithOne="X")]` but property `X` doesn't exist on target entity |
| `EDSL0103` | Error | `[HasOne(WithMany="Y")]` but property `Y` doesn't exist on target entity |
| `EDSL0104` | Error | Bidirectional FK name mismatch — side A says `ForeignKey="OrderId"`, side B says `ForeignKey="OrdId"` |
| `EDSL0105` | Error | FK property declared in `[HasOne]`/`[HasMany]` doesn't exist on the entity |
| `EDSL0106` | Warning | Orphan FK property — `CustomerId` exists but no relationship attribute references it |
| `EDSL0107` | Error | Required relationship cycle — `A` requires `B` requires `A` (can't insert either) |
| `EDSL0108` | Error | Composition cycle — `A -[Composition]→ B -[Composition]→ A` (infinite cascade delete) |
| `EDSL0109` | Warning | `[AssociationClass]` Left and Right are the same type (self-join — valid but unusual) |

#### Schema Integrity

| ID | Severity | Condition |
|---|---|---|
| `EDSL0110` | Error | Duplicate table name — two entities map to same `[Table("X")]` in same schema |
| `EDSL0111` | Error | Duplicate column name — two properties on same entity resolve to same column (after `[Column]` + `[NamingConvention]`) |
| `EDSL0112` | Error | Duplicate index name — two `[Index(Name="IX_X")]` in same table |
| `EDSL0113` | Warning | Composite key order gap — `[PrimaryKey(Order=0)]` and `[PrimaryKey(Order=2)]` but no `Order=1` |
| `EDSL0114` | Warning | Generated column name exceeds 128 chars (SQL Server max identifier length) |
| `EDSL0115` | Warning | `[NamingConvention(SnakeCase)]` produces duplicate column names (e.g., `MyURL` and `MyUrl` both → `my_url`) |
| `EDSL0116` | Error | `[CheckConstraint]` duplicate name within same table |
| `EDSL0117` | Warning | Entity has no properties besides primary key — possibly incomplete |

#### Inheritance Validation

| ID | Severity | Condition |
|---|---|---|
| `EDSL0120` | Error | TPH derived type missing `DiscriminatorValue` |
| `EDSL0121` | Error | TPH duplicate `DiscriminatorValue` across siblings |
| `EDSL0122` | Error | TPC on non-abstract base class |
| `EDSL0123` | Warning | Derived entity in different `BoundedContext` than base — will be in different DbContext |
| `EDSL0124` | Error | `[Inheritance]` on a class with no derived types |
| `EDSL0125` | Error | Derived type has its own `[PrimaryKey]` (PK inherited from base) |
| `EDSL0250` | Error | TPH derived type has `[Table]` with different table name than base |
| `EDSL0251` | Error | TPC base class is not abstract |
| `EDSL0252` | Warning | `[Composition]` on base class points to type only valid for one derived type (TPH: FK column exists for all) |

#### Type/Value Validation

| ID | Severity | Condition |
|---|---|---|
| `EDSL0130` | Error | `[Precision]` on non-numeric property |
| `EDSL0131` | Error | `[MaxLength]` on non-string/non-byte[] property |
| `EDSL0132` | Error | `[EnumStorage]` on non-enum property |
| `EDSL0133` | Error | `[Sequence]`/`[HiLo]` on non-numeric key property |
| `EDSL0134` | Error | `[Collation]` on non-string property |
| `EDSL0135` | Warning | `[ConcurrencyToken]` on a collection or navigation property |
| `EDSL0136` | Error | `[ComputedColumn]` on a `[PrimaryKey]` property |
| `EDSL0137` | Warning | `[DefaultValue(Value="abc")]` can't be parsed as the property's CLR type |

#### Behavior Validation

| ID | Severity | Condition |
|---|---|---|
| `EDSL0140` | Error | `[Sluggable(nameof(X))]` but property `X` doesn't exist on the entity |
| `EDSL0141` | Error | `[Sluggable]` source property is not a string |
| `EDSL0142` | Error | `[Sortable(GroupBy="X")]` but property `X` doesn't exist on the entity |
| `EDSL0143` | Warning | `[SoftDeletable]` entity is target of `[Composition]` from a non-`[SoftDeletable]` parent — cascade will bypass soft delete |
| `EDSL0144` | Warning | `[TreeNode]` on an `[AssociationClass]` — unusual |
| `EDSL0145` | Warning | `[Timestampable]` + explicit `[ShadowProperty("CreatedAt", ...)]` — redundant |
| `EDSL0146` | Error | `[TranslatableProperty]` on a navigation property — only scalars can be translated |
| `EDSL0147` | Warning | Behavior-generated property name conflicts with developer-declared property |

#### Ownership Validation

| ID | Severity | Condition |
|---|---|---|
| `EDSL0150` | Error | `[Owned]` type registered as a `DbSet` — owned types are accessed through their owner |
| `EDSL0151` | Error | `[Owned]` type has `[PrimaryKey]` — owned types get identity from owner |
| `EDSL0152` | Warning | `[Owned]` type has `[HasOne]`/`[HasMany]` to a non-owned entity — restricted in EF Core |
| `EDSL0153` | Warning | Navigation to `[ValueObject]`/`[Owned]` without `[OwnedEntity]` or `[ComplexType]` — will auto-infer |

#### DbContext / Bounded Context Validation

| ID | Severity | Condition |
|---|---|---|
| `EDSL0160` | Warning | Entity has `BoundedContext="X"` but no `[DbContext(BoundedContext="X")]` exists — orphaned |
| `EDSL0161` | Warning | `[DbContext]` has no matching entities — empty DbContext |
| `EDSL0162` | Info | Entity matches multiple `[DbContext]` definitions — included in all |
| `EDSL0163` | Error | `[DbContext]` class doesn't inherit from `DbContext` |

#### Stored Procedure / View Validation

| ID | Severity | Condition |
|---|---|---|
| `EDSL0170` | Warning | `[StoredProcedure]` + `[SoftDeletable]` — SP delete bypasses soft-delete interceptor |
| `EDSL0171` | Warning | `[View]` + `[StoredProcedure(Insert=...)]` — views with SP insert is unusual |
| `EDSL0172` | Warning | `[View]` + `[Timestampable]`/`[Blameable]` — views are read-only, behaviors won't fire |

#### Cycle Detection & Structural Impossibilities (Graph Analysis)

| ID | Severity | Condition |
|---|---|---|
| `EDSL0200` | Error | Composition cycle: `A -[Composition]→ B -[Composition]→ A` — infinite cascade delete |
| `EDSL0201` | Error | Required reference cycle: `A.B` required + `B.A` required — can't INSERT either |
| `EDSL0202` | Error | Owned type cycle: `A -[Owned]→ B -[Owned]→ A` — circular ownership impossible |
| `EDSL0203` | Error | Owned type shared across owners: `A -[Owned]→ X` and `B -[Owned]→ X` — each instance belongs to exactly one owner |
| `EDSL0204` | Error | Cascade delete path ambiguity: multiple independent cascade paths converge on same entity |
| `EDSL0205` | Error | Required self-reference with non-nullable FK — root node can never exist |
| `EDSL0206` | Error | Navigation to `[Keyless]`/`[View]` entity — can't FK to entity with no PK |
| `EDSL0207` | Error | `[Composition]` to a `[View]` entity — views are read-only, can't cascade |
| `EDSL0230` | Error | Three-way cascade convergence — EF Core / SQL Server rejects multiple cascade paths |
| `EDSL0231` | Warning | `[SoftDeletable]` child is cascade-delete target of non-`[SoftDeletable]` parent — hard delete bypasses soft delete |
| `EDSL0232` | Warning | `[Composition]` + `[SoftDeletable]` — composition says "delete child" but soft-delete says "never delete" |
| `EDSL0233` | Warning | `[Aggregation]` with explicit `OnDelete="Cascade"` — aggregation semantics say shared lifecycle, but cascade says exclusive |

#### DDD Structural Violations

| ID | Severity | Condition |
|---|---|---|
| `EDSL0210` | Error | `[Composition]` from Entity/AggregateRoot to another `[AggregateRoot]` — can't compose one aggregate into another |
| `EDSL0211` | Error | Two `[AggregateRoot]`s both `[Composition]` to same `[Entity]` — entity belongs to one aggregate only |
| `EDSL0212` | Warning | `[Entity]` unreachable from any `[AggregateRoot]` — orphan entity with no aggregate |
| `EDSL0213` | Warning | `[ValueObject]`/`[Owned]` with navigation to `[Entity]`/`[AggregateRoot]` — value objects shouldn't reference entities |
| `EDSL0214` | Warning | `[AggregateRoot]` with no `[Composition]` children — standalone entity, verify intent |
| `EDSL0182` | Info | No `[ConcurrencyToken]`/`[Versionable]` on any aggregate root — no optimistic concurrency |
| `EDSL0183` | Warning | `[AggregateRoot]` without any `[Invariant]` methods — aggregate boundary may be too loose |

#### Owned Type Graph Rules

| ID | Severity | Condition |
|---|---|---|
| `EDSL0220` | Error | `[Owned]` type has `[PrimaryKey]` — identity comes from owner |
| `EDSL0221` | Error | `[Owned]` type registered as `DbSet` — query through owner only |
| `EDSL0222` | Error | `[Owned]` type has `[Composition]` to non-owned entity — breaks ownership boundary |
| `EDSL0223` | Error | `[Owned]` type used in `[ManyToMany]` — no independent identity for join FK |
| `EDSL0224` | Error | `[Owned]` type used as `[AssociationClass]` endpoint — can't be FK target |

#### Association Class Structural Rules

| ID | Severity | Condition |
|---|---|---|
| `EDSL0240` | Error | `[AssociationClass]` where Left or Right is `[Owned]`/`[ValueObject]` — needs independent key |
| `EDSL0241` | Error | `[AssociationClass]` where Left or Right is `[Keyless]`/`[View]` — can't FK to keyless |
| `EDSL0242` | Warning | `[AssociationClass]` where Left == Right (self-join) — valid but unusual |
| `EDSL0243` | Error | `[AssociationClass]` is itself `[Owned]` — association classes have independent identity |
| `EDSL0244` | Error | Nested `[AssociationClass]` — endpoint is also an `[AssociationClass]` with composite key (EF Core FK chain limitation) |

### 3.3 Emit Models (in Lib — public API from day one)

```csharp
public sealed class EntityEmitModel
{
    public string Namespace { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ClassFullName { get; set; } = "";

    // Table / View
    public string? TableName { get; set; }
    public string? Schema { get; set; }
    public string? ViewName { get; set; }
    public string? ViewSchema { get; set; }
    public bool IsKeyless { get; set; }
    public NamingStrategy? NamingConvention { get; set; }

    // Key
    public List<KeyPropertyModel> PrimaryKeyProperties { get; set; } = new();

    // Properties
    public List<PropertyConfigModel> Properties { get; set; } = new();

    // Alternate keys (grouped by GroupName)
    public List<AlternateKeyModel> AlternateKeys { get; set; } = new();

    // Indexes (grouped by GroupName)
    public List<IndexModel> Indexes { get; set; } = new();

    // Relationships
    public List<RelationshipModel> Relationships { get; set; } = new();

    // Owned entities
    public List<OwnedEntityModel> OwnedEntities { get; set; } = new();

    // Complex types
    public List<ComplexTypeModel> ComplexTypes { get; set; } = new();

    // Shadow properties
    public List<ShadowPropertyModel> ShadowProperties { get; set; } = new();

    // Inheritance
    public InheritanceModel? Inheritance { get; set; }

    // Query filter
    public string? QueryFilterMethodName { get; set; }

    // Check constraints
    public List<CheckConstraintModel> CheckConstraints { get; set; } = new();

    // Seed data
    public string? SeedDataMethodName { get; set; }

    // Temporal table
    public TemporalTableModel? TemporalTable { get; set; }

    // Table splitting
    public TableSplitModel? TableSplit { get; set; }

    // Validation method names (compile-time, for SG diagnostics)
    public string? ValidateMethodName { get; set; }

    // Association class (null if regular entity)
    // Association class (null if regular entity)
    public AssociationClassModel? AssociationClass { get; set; }

    // Class-level [Owned] — any navigation to this type auto-infers OwnsOne/OwnsMany
    public bool IsOwnedType { get; set; }
    public string? OwnedTableName { get; set; }
    public string? OwnedJsonColumn { get; set; }

    // Behavior models (null = behavior not applied, non-null = active with config)
    public TimestampableBehaviorModel? Timestampable { get; set; }
    public SoftDeletableBehaviorModel? SoftDeletable { get; set; }
    public BlameableBehaviorModel? Blameable { get; set; }
    public VersionableBehaviorModel? Versionable { get; set; }
    public SluggableBehaviorModel? Sluggable { get; set; }
    public LoggableBehaviorModel? Loggable { get; set; }
    public TranslatableBehaviorModel? Translatable { get; set; }
    public SortableBehaviorModel? Sortable { get; set; }
    public TreeNodeBehaviorModel? TreeNode { get; set; }

    // Entity listeners declared via [EntityListener(typeof(...))]
    public List<string> ListenerTypesFull { get; set; } = new();

    // Comments
    public string? TableComment { get; set; }

    // Entity splitting
    public List<EntitySplitModel> EntitySplits { get; set; } = new();

    // Stored procedures
    public StoredProcedureModel? StoredProcedure { get; set; }

    // Triggers
    public List<string> TriggerNames { get; set; } = new();

    // Auto-included navigations
    public List<string> AutoIncludeNavigations { get; set; } = new();

    // Translatable properties (for ITranslatable companion generation)
    public List<string> TranslatablePropertyNames { get; set; } = new();
}
```

#### Sub-models (all in Lib, all Roslyn-free)

```csharp
public sealed class KeyPropertyModel
{
    public string PropertyName { get; set; } = "";
    public int Order { get; set; }
    public string ValueGenerated { get; set; } = "OnAdd"; // None, OnAdd, OnUpdate, OnAddOrUpdate
    public string? SequenceName { get; set; }
    public string? SequenceSchema { get; set; }
    public long SequenceStartsAt { get; set; } = 1;
    public int SequenceIncrementsBy { get; set; } = 1;
}

public sealed class PropertyConfigModel
{
    public string PropertyName { get; set; } = "";
    public string PropertyTypeFull { get; set; } = "";
    public string? ColumnName { get; set; }
    public string? ColumnType { get; set; }
    public int ColumnOrder { get; set; } = -1;
    public bool IsRequired { get; set; }
    public int MaxLength { get; set; }
    public int Precision { get; set; }
    public int Scale { get; set; }
    public string? DefaultValue { get; set; }
    public string? DefaultValueSql { get; set; }
    public string? ConverterTypeFull { get; set; }
    public bool IsConcurrencyToken { get; set; }
    public bool IsRowVersion { get; set; }
    public string? ComputedColumnSql { get; set; }
    public bool ComputedColumnStored { get; set; }
    public bool? EnumAsString { get; set; }          // null = not enum, true = string, false = int
    public string? Comment { get; set; }             // HasComment("...")
    public string? BackingFieldName { get; set; }    // HasField("_x")
    public string? BackingFieldAccessMode { get; set; } // UsePropertyAccessMode(...)
    public string? ValueGeneratorTypeFull { get; set; } // HasValueGenerator<T>()
    public string? ValueComparerTypeFull { get; set; }  // Metadata.SetValueComparer()
    public string? Collation { get; set; }           // UseCollation("...")
    public string? HiLoSequenceName { get; set; }    // UseHiLo("seq")
    public string? HiLoSchema { get; set; }
    public bool IsAutoInclude { get; set; }          // Navigation(e => e.X).AutoInclude()
    public bool IsTranslatable { get; set; }         // Marked for translation companion
    public bool IsNotMapped { get; set; }            // Skip this property in config emission
}

public sealed class RelationshipModel
{
    public string NavigationProperty { get; set; } = "";
    public string RelatedTypeFull { get; set; } = "";
    public string Kind { get; set; } = "";          // "HasOne.WithOne", "HasOne.WithMany", "HasMany.WithOne", "HasMany.WithMany"
    public string? InverseNavigation { get; set; }
    public string? ForeignKey { get; set; }
    public string? PrincipalKey { get; set; }
    public string DeleteBehavior { get; set; } = "ClientSetNull";
    public bool IsRequired { get; set; }
    public bool IsSelfReference { get; set; }
    // For M:M
    public string? JoinEntityTypeFull { get; set; }
    public string? LeftForeignKey { get; set; }
    public string? RightForeignKey { get; set; }
    public string? JoinTableName { get; set; }
}

public sealed class OwnedEntityModel
{
    public string NavigationProperty { get; set; } = "";
    public string OwnedTypeFull { get; set; } = "";
    public bool IsCollection { get; set; }           // OwnsMany vs OwnsOne
    public string? TableName { get; set; }
    public string? JsonColumn { get; set; }
}

public sealed class ComplexTypeModel
{
    public string NavigationProperty { get; set; } = "";
    public string TypeFull { get; set; } = "";
}

public sealed class IndexModel
{
    public string? Name { get; set; }
    public List<string> PropertyNames { get; set; } = new();
    public bool IsUnique { get; set; }
    public List<bool>? IsDescending { get; set; }
}

public sealed class AlternateKeyModel
{
    public string? GroupName { get; set; }
    public List<string> PropertyNames { get; set; } = new();
}

public sealed class ShadowPropertyModel
{
    public string Name { get; set; } = "";
    public string ClrTypeFull { get; set; } = "";
    public bool IsRequired { get; set; }
    public string? DefaultValueSql { get; set; }
}

public sealed class InheritanceModel
{
    public string Strategy { get; set; } = "TPH";   // TPH, TPT, TPC
    public string? DiscriminatorColumn { get; set; }
    public string? DiscriminatorValue { get; set; }
    public List<DerivedTypeModel> DerivedTypes { get; set; } = new();
}

public sealed class DerivedTypeModel
{
    public string TypeFull { get; set; } = "";
    public string? DiscriminatorValue { get; set; }
    public string? TableName { get; set; }           // For TPT
}

public sealed class CheckConstraintModel
{
    public string Name { get; set; } = "";
    public string Sql { get; set; } = "";
}

public sealed class TemporalTableModel
{
    public string? HistoryTable { get; set; }
    public string? HistorySchema { get; set; }
}

public sealed class TableSplitModel
{
    public string TableName { get; set; } = "";
    public string? Schema { get; set; }
}

public sealed class DbContextEmitModel
{
    public string Namespace { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string? BoundedContext { get; set; }
    public List<DbSetModel> DbSets { get; set; } = new();
    public List<string> SequenceDefinitions { get; set; } = new();

    // Context-level configuration
    public bool LazyLoading { get; set; }
    public string? QueryTracking { get; set; }     // "TrackAll", "NoTracking", etc.
    public string? QuerySplitting { get; set; }    // "SingleQuery", "SplitQuery"
    public string? ChangeTracking { get; set; }    // "Snapshot", "ChangingAndChanged", etc.
    public bool EnableRetryOnFailure { get; set; }
    public int MaxRetryCount { get; set; } = 6;

    // Behaviors detected across entities — the DbContextBase needs to know which
    // behaviors exist to generate the PopulateBehaviors() method. Each entry carries
    // the entity type + its behavior config (custom property names).
    public List<EntityBehaviorSummary> EntityBehaviors { get; set; } = new();
    public bool HasGlobalListeners { get; set; }
}

/// <summary>Per-entity behavior summary — used by DbContextBase emitter to generate PopulateBehaviors().</summary>
public sealed class EntityBehaviorSummary
{
    public string EntityTypeFull { get; set; } = "";
    public TimestampableBehaviorModel? Timestampable { get; set; }
    public SoftDeletableBehaviorModel? SoftDeletable { get; set; }
    public BlameableBehaviorModel? Blameable { get; set; }
    public SluggableBehaviorModel? Sluggable { get; set; }
    public SortableBehaviorModel? Sortable { get; set; }
    public TreeNodeBehaviorModel? TreeNode { get; set; }
}

public sealed class DbSetModel
{
    public string EntityTypeFull { get; set; } = "";
    public string PropertyName { get; set; } = "";   // Pluralized class name
    public bool IsKeyless { get; set; }              // No DbSet for keyless, but still needs config
}

public sealed class AssociationClassModel
{
    public string LeftTypeFull { get; set; } = "";
    public string RightTypeFull { get; set; } = "";
    public string LeftMultiplicity { get; set; } = "*";
    public string RightMultiplicity { get; set; } = "*";
    public string? OnDeleteLeft { get; set; }
    public string? OnDeleteRight { get; set; }
    public string LeftForeignKey { get; set; } = "";     // e.g. "StudentId"
    public string RightForeignKey { get; set; } = "";    // e.g. "CourseId"
    public string? LeftNavigationProperty { get; set; }  // e.g. "Student"
    public string? RightNavigationProperty { get; set; } // e.g. "Course"
}

public sealed class RepositoryEmitModel
{
    public string Namespace { get; set; } = "";
    public string EntityClassName { get; set; } = "";
    public string EntityClassFull { get; set; } = "";
    public string PrimaryKeyTypeFull { get; set; } = "";  // e.g. "System.Guid", or tuple for composite
    public bool IsCompositeKey { get; set; }
    public List<string> CompositeKeyPropertyNames { get; set; } = new();
    public bool IsAssociationClass { get; set; }
    public string? LeftTypeFull { get; set; }              // for IAssociationRepository
    public string? RightTypeFull { get; set; }
    public string DbContextTypeFull { get; set; } = "";   // typed context for constructor
}

public sealed class UnitOfWorkEmitModel
{
    public string Namespace { get; set; } = "";
    public string DbContextClassName { get; set; } = "";
    public string DbContextClassFull { get; set; } = "";
    public List<UnitOfWorkRepositoryModel> Repositories { get; set; } = new();
}

public sealed class UnitOfWorkRepositoryModel
{
    public string InterfaceTypeFull { get; set; } = "";    // IOrderRepository
    public string ImplementationTypeFull { get; set; } = "";// OrderRepository
    public string PropertyName { get; set; } = "";          // "Orders"
}

public sealed class BackingFieldModel
{
    public string PropertyName { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string? AccessMode { get; set; }  // null = default, "Field", "Property", etc.
}

public sealed class EntitySplitModel
{
    public string TableName { get; set; } = "";
    public string? Schema { get; set; }
    public List<string> PropertyNames { get; set; } = new();
}

public sealed class StoredProcedureModel
{
    public string? InsertProcedure { get; set; }
    public string? UpdateProcedure { get; set; }
    public string? DeleteProcedure { get; set; }
}

// ── Behavior models (carry custom property names from attribute parameters) ──

public sealed class TimestampableBehaviorModel
{
    public string CreatedAtName { get; set; } = "CreatedAt";
    public string UpdatedAtName { get; set; } = "UpdatedAt";
    public string Type { get; set; } = "DateTimeOffset";
    public int Precision { get; set; } = 7;
    public string TimeZone { get; set; } = "Utc";
    public bool CreatedAtImmutable { get; set; } = true;
    public bool UpdateOnChildChange { get; set; }
    public string? CreatedAtColumnName { get; set; }
    public string? UpdatedAtColumnName { get; set; }
}

public sealed class SoftDeletableBehaviorModel
{
    public string IsDeletedName { get; set; } = "IsDeleted";
    public string DeletedAtName { get; set; } = "DeletedAt";
    public bool CascadeToChildren { get; set; } = true;
    public bool AllowHardDelete { get; set; }
    public bool FilterEnabled { get; set; } = true;
    public bool AllowRestore { get; set; } = true;
    public string? IsDeletedColumnName { get; set; }
    public string? DeletedAtColumnName { get; set; }
}

public sealed class BlameableBehaviorModel
{
    public string CreatedByName { get; set; } = "CreatedBy";
    public string UpdatedByName { get; set; } = "UpdatedBy";
    public bool TrackDeletedBy { get; set; }
    public string DeletedByName { get; set; } = "DeletedBy";
    public string IdentifierType { get; set; } = "string";
    public int MaxLength { get; set; } = 256;
    public bool Required { get; set; }
    public string? CreatedByColumnName { get; set; }
    public string? UpdatedByColumnName { get; set; }
}

public sealed class VersionableBehaviorModel
{
    public string PropertyName { get; set; } = "RowVersion";
    public string Strategy { get; set; } = "RowVersion";  // RowVersion, Guid, Timestamp, Increment
    public string? TypeOverride { get; set; }
    public string? ColumnName { get; set; }
}

public sealed class SluggableBehaviorModel
{
    public string Source { get; set; } = "";               // single or comma-separated property names
    public string Separator { get; set; } = "-";
    public string PropertyName { get; set; } = "Slug";
    public int MaxLength { get; set; } = 256;
    public bool Unique { get; set; } = true;
    public string? UniqueScope { get; set; }
    public string DuplicateSuffix { get; set; } = "-{n}";
    public string Regenerate { get; set; } = "OnCreate";   // OnCreate, Always
    public bool Transliterate { get; set; } = true;
    public bool Lowercase { get; set; } = true;
    public string? ColumnName { get; set; }
}

public sealed class LoggableBehaviorModel
{
    public string? AuditTableName { get; set; }
    public string? AuditSchema { get; set; }
    public List<string>? TrackProperties { get; set; }     // null = all
    public List<string>? IgnoreProperties { get; set; }
    public bool LogInsert { get; set; } = true;
    public bool LogUpdate { get; set; } = true;
    public bool LogDelete { get; set; } = true;
    public bool CaptureOldValues { get; set; } = true;
    public bool CaptureNewValues { get; set; } = true;
    public string ValueFormat { get; set; } = "Json";      // Json, Columns
    public bool TrackUser { get; set; } = true;
    public int RetentionDays { get; set; }                  // 0 = unlimited
}

public sealed class TranslatableBehaviorModel
{
    public string DefaultLocale { get; set; } = "en";
    public string? TranslationTableName { get; set; }
    public string? TranslationSchema { get; set; }
    public string LocaleColumnName { get; set; } = "Locale";
    public int LocaleMaxLength { get; set; } = 10;
    public string Fallback { get; set; } = "DefaultLocale"; // DefaultLocale, Null, Throw
    public bool UniquePerLocale { get; set; } = true;
    public List<string> TranslatablePropertyNames { get; set; } = new();
}

public sealed class SortableBehaviorModel
{
    public string PropertyName { get; set; } = "Position";
    public string? GroupBy { get; set; }                    // comma-separated group keys
    public int StartAt { get; set; }                        // 0 or 1
    public string OnDelete { get; set; } = "Reorder";      // Reorder, LeaveGap
    public string OnInsert { get; set; } = "AppendLast";   // AppendLast, PrependFirst
    public string? ColumnName { get; set; }
    public bool CreateIndex { get; set; } = true;
}

public sealed class TreeNodeBehaviorModel
{
    public string Strategy { get; set; } = "MaterializedPath"; // MaterializedPath, AdjacencyList, ClosureTable
    public string ParentIdName { get; set; } = "ParentId";
    public string ParentNavigationName { get; set; } = "Parent";
    public string ChildrenNavigationName { get; set; } = "Children";
    public string PathName { get; set; } = "MaterializedPath";
    public string DepthName { get; set; } = "Depth";
    public string PathSeparator { get; set; } = "/";
    public int PathMaxLength { get; set; } = 1024;
    public string? ClosureTableName { get; set; }
    public int MaxDepth { get; set; }                       // 0 = unlimited
    public string OnDelete { get; set; } = "Restrict";     // Restrict, Cascade, SetNull
    public string? OrderChildrenBy { get; set; }
    public bool GenerateQueryExtensions { get; set; } = true;
}
```

---

## 4. Emitters (in Lib)

The emitters follow the **Generation Gap** pattern (see Section 9). Each entity produces **3 config files + 3 repo files**, and the DbContext produces **2 context files + 3 UoW files + 1 registration file**.

### `EntityConfigurationEmitter` — 3 static methods

```csharp
public static class EntityConfigurationEmitter
{
    /// <summary>Emits the abstract base class with all virtual Configure* methods.
    /// Always regenerated.</summary>
    public static string EmitBase(EntityEmitModel model) { ... }

    /// <summary>Emits the partial class stub extending Base (empty overrides).
    /// Always regenerated — developer adds a second partial file to override.</summary>
    public static string EmitPartialStub(EntityEmitModel model) { ... }

    /// <summary>Emits the IEntityTypeConfiguration<T> registration that delegates to the partial class.
    /// Always regenerated.</summary>
    public static string EmitRegistration(EntityEmitModel model) { ... }
}
```

**EmitBase** produces the `OrderConfigurationBase` abstract class with:
- `PreConfigure(builder)` / `PostConfigure(builder)` hooks
- One `virtual Configure{Property}(builder)` per property
- One `virtual Configure{Relationship}(builder)` per navigation
- `virtual ConfigureTable(builder)`, `ConfigurePrimaryKey(builder)`, `ConfigureIndexes(builder)`, `ConfigureShadowProperties(builder)`, `ConfigureCheckConstraints(builder)`
- `Configure(builder)` orchestrator that calls all virtuals in order

**EmitPartialStub** produces `partial class OrderConfiguration : OrderConfigurationBase { }` — empty.

**EmitRegistration** produces `OrderConfigurationRegistration : IEntityTypeConfiguration<T>` that delegates to `new OrderConfiguration().Configure(builder)`.

### `DbContextEmitter` — 2 static methods

```csharp
public static class DbContextEmitter
{
    /// <summary>Emits the abstract base DbContext with DbSets, OnModelCreating, SaveChanges hooks.
    /// Always regenerated.</summary>
    public static string EmitBase(DbContextEmitModel model) { ... }

    /// <summary>Emits the partial class stub extending Base.
    /// Always regenerated — developer adds a second partial file to override.</summary>
    public static string EmitPartialStub(DbContextEmitModel model) { ... }
}
```

**EmitBase** produces `AppDbContextBase : DbContext` with:
- DbSet properties
- `PreModelCreating` / `PostModelCreating` hooks
- `RegisterConfigurations(modelBuilder)` — calls all `*Registration` classes
- `ConfigureSequences(modelBuilder)`
- `UpdateTimestamps()` — shadow property auto-population
- `OnEntitiesAdding/Modifying/Deleting(entries)` — Doctrine-style lifecycle hooks
- Sealed `OnModelCreating` orchestrator
- `SaveChanges`/`SaveChangesAsync` overrides calling `OnBeforeSaveChanges()`

### `NamingHelper` (in Lib)

Converts property names based on `NamingStrategy`:

```csharp
public static class NamingHelper
{
    public static string ToSnakeCase(string name) => ...;  // "OrderNumber" → "order_number"
    public static string ToCamelCase(string name) => ...;  // "OrderNumber" → "orderNumber"
    public static string Pluralize(string name) => ...;    // "Order" → "Orders" (simple English rules)
}
```

---

## 5. Multi-DbContext & Bounded Context Support

**Problem**: In a large solution, entities may belong to different bounded contexts requiring separate DbContexts.

**Solution**: The `[DbContext(BoundedContext = "Sales")]` attribute scopes which entities are included:

```csharp
[DbContext(BoundedContext = "Sales")]
public partial class SalesDbContext : DbContext { }

[DbContext(BoundedContext = "Inventory")]
public partial class InventoryDbContext : DbContext { }
```

Entities are routed by their `[AggregateRoot(BoundedContext = "Sales")]`. Entities without a BoundedContext go to any DbContext without a BoundedContext filter, or to all if multiple contexts exist.

**If `BoundedContext` is null** on `[DbContext]` → all entities in the assembly are included (default for simple projects).

### Multi-DbContext Generation Gap — Per-Context File Set

Each `[DbContext]` gets its **own complete set of generated files**, following the same Generation Gap pattern. All repositories and UnitOfWork are scoped to their context.

```
Per [DbContext(BoundedContext = "Sales")]:
  SalesDbContextBase.g.cs                  ← always regenerated (abstract)
  SalesDbContext.g.cs                      ← partial stub (developer extends)
  ISalesUnitOfWork.g.cs                    ← always regenerated
  SalesUnitOfWorkBase.g.cs                 ← always regenerated (abstract, virtual factories)
  SalesUnitOfWork.g.cs                     ← partial stub with [Injectable]
  SalesDbContextRegistration.g.cs          ← AddDbContext extension

Per [DbContext(BoundedContext = "Inventory")]:
  InventoryDbContextBase.g.cs              ← always regenerated (abstract)
  InventoryDbContext.g.cs                  ← partial stub
  IInventoryUnitOfWork.g.cs                ← always regenerated
  InventoryUnitOfWorkBase.g.cs             ← always regenerated
  InventoryUnitOfWork.g.cs                 ← partial stub with [Injectable]
  InventoryDbContextRegistration.g.cs      ← AddDbContext extension
```

**Repository scoping**: The generated `OrderRepositoryBase` takes `SalesDbContext` (typed), not a generic `DbContext`. If an entity belongs to multiple contexts (unusual but possible), the SG generates separate repository bases per context.

**Developer extension**: Each generated stub is a partial class. The developer adds their own partial file to override any virtual method:

```csharp
// Developer: SalesDbContext.cs
public partial class SalesDbContext
{
    protected override void PostModelCreating(ModelBuilder modelBuilder) { /* custom */ }
}

// Developer: InventoryDbContext.cs  
public partial class InventoryDbContext
{
    protected override void ConfigureResiliency(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableRetryOnFailure(maxRetryCount: 3);
    }
}
```

**Startup registration** — one call per context:
```csharp
services.AddSalesDbContext(o => o.UseSqlServer("..."));
services.AddInventoryDbContext(o => o.UseSqlite("..."));
services.AddMyAppInjectables();  // Injectable SG registers all repos + UoWs
```

---

## 6. SaveChanges Hooks — Two Mechanisms

There are **two distinct mechanisms** for auto-populating properties in SaveChanges. They do NOT overlap:

| Mechanism | Declares via | Properties are | Used for |
|---|---|---|---|
| **Behavior attributes** (`[Timestampable]`, `[Blameable]`, etc.) | Class-level attribute | Real CLR properties (SG-generated partial) | Domain-visible timestamps, audit, soft delete |
| **`[ShadowProperty]`** | Class-level attribute | Shadow (not on CLR type) | Infrastructure-only fields invisible to domain |

**Behavior attributes** (section 8.5) generate `PopulateBehaviors()` in DbContextBase — uses property names from the behavior models (e.g., `TimestampableBehaviorModel.CreatedAtName`).

**`[ShadowProperty]`** generates `UpdateShadowProperties()` in DbContextBase — only for explicit `[ShadowProperty]` declarations. Example: `[ShadowProperty("TenantId", "int")]` for multi-tenant filtering invisible to the domain.

**Do NOT use `[ShadowProperty("CreatedAt", "DateTimeOffset")]` for timestamps** — use `[Timestampable]` instead. Shadow properties are for truly infrastructure-level concerns that the domain should never see.

---

## 7. Migration Story

The SG does **not** generate migrations. Migrations remain fully manual via `dotnet ef migrations add`. The generated `IEntityTypeConfiguration<T>` classes are what EF Core reads during migration scaffolding — they are the source of truth.

The SG output is deterministic: same attributes → same generated code → same migration diff.

---

## 8. SOLID Extensibility Architecture

Following the Doctrine/Diem approach, the SG generates a full **vertical slice per entity**: configuration, repository interface, repository base, entity listener interface, and contributes to the UnitOfWork. Each layer follows SOLID — developers extend via interfaces and virtual methods, never by modifying generated code.

### 8.1 Project Structure: Abstractions vs Infra (Clean Architecture Split)

**Principle**: Abstractions has **zero** EF Core dependency. All EF Core implementations go to a separate Infra project.

| Project | TFM | EF Core? | Contents |
|---------|-----|----------|----------|
| `Entity.Dsl.Abstractions` | `netstandard2.0;net10.0` | **No** | Pure interfaces: `IRepository<T>`, `IReadOnlyRepository<T>`, `IAssociationRepository<,,>`, `ISpecification<T>`, `Specification<T>`, `IUnitOfWork`, `IUnitOfWorkTransaction`, `ICurrentUserProvider`, `SlugHelper`, `PagedResult<T>`, `FakeRepository<T>`, `FakeUnitOfWork` |
| `Entity.Dsl.Infra` | `net10.0` | **Yes** | EF Core implementations: `RepositoryBase<T>`, `IEntityListener<T>`, `IGlobalEntityListener`, `IUnitOfWork<TContext>` |

**Why `netstandard2.0` for Abstractions?** Domain layer projects can target netstandard2.0 — they should never need EF Core. The Infra project targets net10.0 because EF Core 10 requires it.

**Dependencies:**
```
Abstractions ──> (no dependencies — pure .NET)

Infra ──> Abstractions
      ──> Microsoft.EntityFrameworkCore

SourceGenerator.Lib ──> (no dependencies — pure string emission)
SourceGenerator ──> Lib (linked files)

Tests ──> Abstractions, Lib (unit testable without EF Core)
Integration.Tests ──> Abstractions, Infra, EF Core Sqlite
```

**What moves from Abstractions → Infra:**

| Type | Current Location | New Location | Reason |
|---|---|---|---|
| `RepositoryBase<T>` | Abstractions | **Infra** | Depends on `DbContext`, `DbSet<T>`, EF Core LINQ |
| `IUnitOfWork<TContext>` | Abstractions | **Infra** | `where TContext : DbContext` constraint |
| `IEntityListener<T>` | Abstractions | **Infra** | `OnLoadedAsync` ties to materialization; keep pure entity hooks in Abstractions as `IEntityListener<T>` without `OnLoadedAsync`, add `IEntityMaterializationListener<T>` in Infra |
| `IGlobalEntityListener` | Abstractions | **Infra** | Uses `EntityEntry` |

**What stays in Abstractions (pure, unit-testable):**

| Type | Why |
|---|---|
| `IReadOnlyRepository<T>` | Pure interface — `Expression`, `Task`, `IQueryable` only |
| `IRepository<T>` | Pure interface extending `IReadOnlyRepository<T>` |
| `IAssociationRepository<,,>` | Pure interface |
| `ISpecification<T>` | Pure interface |
| `Specification<T>` (base class) | Pure — `And/Or/Not` composition |
| `IUnitOfWork` | Pure interface (no DbContext reference) |
| `IUnitOfWorkTransaction` | Pure interface |
| `ICurrentUserProvider` | Pure interface |
| `ITenantProvider` | Pure interface |
| `SlugHelper` | Static utility — zero deps |
| `PagedResult<T>` | Pure DTO |
| `FakeRepository<T>` | In-memory fake — zero EF Core |
| `FakeUnitOfWork` | In-memory fake — zero EF Core |

**IEntityListener split:**
- `IEntityLifecycleListener<T>` stays in **Abstractions** — pure hooks: `OnAddingAsync(T)`, `OnAddedAsync(T)`, `OnModifyingAsync(T)`, `OnModifiedAsync(T)`, `OnRemovingAsync(T)`, `OnRemovedAsync(T)` — typed `T`, no EF Core types
- `IEntityMaterializationListener<T>` moves to **Infra** — `OnLoadedAsync(T, EntityEntry)` needs `EntityEntry`
- `IGlobalEntityListener` moves to **Infra** — all methods take `EntityEntry`

Updated project list:
```
src/
  FrenchExDev.Net.Entity.Dsl.Attributes          (netstandard2.0;net10.0)
  FrenchExDev.Net.Entity.Dsl.Abstractions         (netstandard2.0;net10.0) — NO EF Core
  FrenchExDev.Net.Entity.Dsl.Infra                (net10.0) — EF Core implementations
  FrenchExDev.Net.Entity.Dsl.SourceGenerator       (netstandard2.0)
  FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib   (netstandard2.0)
test/
  FrenchExDev.Net.Entity.Dsl.Tests                 (net10.0) — unit tests, NO EF Core needed
  FrenchExDev.Net.Entity.Dsl.Integration.Tests     (net10.0) — EF Core SQLite
```

**SG emitter string reference update:**
The `RepositoryEmitter` default base changes from:
```
global::FrenchExDev.Net.Entity.Dsl.Abstractions.RepositoryBase<T>
```
to:
```
global::FrenchExDev.Net.Entity.Dsl.Infra.RepositoryBase<T>
```

**Coverage impact:** Abstractions is now 100% unit-testable (no EF Core). `RepositoryBase<T>` in Infra is tested by Integration.Tests with SQLite in-memory.

### 8.2 Repository Pattern — Generation Gap (Same 3-Layer as Config)

Repositories follow the exact same Generation Gap pattern as entity configurations:

```
┌─────────────────────────────────────────────────┐
│  Layer 1: {Entity}RepositoryBase.g.cs           │  ← SG overwrites on every build
│  Contains: all IRepository<T> methods (virtual) │
│  Abstract class, all methods overridable         │
├─────────────────────────────────────────────────┤
│  Layer 2: {Entity}Repository.g.cs (partial)     │  ← SG overwrites (stub + [Injectable])
│  Contains: empty partial extending Base          │
├─────────────────────────────────────────────────┤
│  Layer 3: {Entity}Repository.cs (partial)       │  ← Developer-written (optional)
│  Contains: domain queries, overrides             │
│  Merges with Layer 2 via partial class           │
└─────────────────────────────────────────────────┘
    +
  I{Entity}Repository.g.cs                         ← SG overwrites (typed interface)
```

For each entity, the SG generates:
1. **`I{Entity}Repository`** — entity-specific interface extending `IRepository<T>` (always regenerated)
2. **`{Entity}RepositoryBase`** — abstract base with all virtual methods (always regenerated)
3. **`{Entity}Repository`** — partial class stub with `[Injectable]` (always regenerated)
4. **Developer's `{Entity}Repository.cs`** — partial class with domain queries (developer-owned, optional)

#### Generic Interfaces (in Abstractions — hand-written, not generated)

```csharp
namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

/// <summary>Read-only repository — queries only. Use for CQRS read side.</summary>
/// <summary>
/// Read-only repository — queries only. NO IQueryable exposure (that's infra).
/// All queries go through typed methods or ISpecification&lt;T&gt;.
/// </summary>
public interface IReadOnlyRepository<T> where T : class
{
    ValueTask<T?> FindByIdAsync(params object[] keyValues);
    Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindBySpecAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<PagedResult<T>> FindPagedAsync(int pageNumber, int pageSize, ISpecification<T>? spec = null, CancellationToken ct = default);
    // NO IQueryable<T> Query — that leaks ORM into domain.
    // IQueryable stays protected in RepositoryBase<T> (Infra) for internal use only.
}

/// <summary>Full repository — extends read-only with commands.</summary>
public interface IRepository<T> : IReadOnlyRepository<T> where T : class
{
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);

    /// <summary>
    /// Marks a disconnected entity as modified. For tracked entities, EF Core
    /// change tracking handles updates automatically — no need to call this.
    /// </summary>
    void Update(T entity);

    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);

    /// <summary>Attaches a disconnected entity without marking it as modified.</summary>
    void Attach(T entity);
}

/// <summary>Reusable query specification (DDD pattern).</summary>
public interface ISpecification<T> where T : class
{
    Expression<Func<T, bool>> Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }
    int? Take { get; }
    int? Skip { get; }
}
```

**Note:** Repositories do **not** implement `IAsyncDisposable` — they don't own the DbContext. Lifetime is managed by the UnitOfWork/DI container.

#### Association-Specific Interface (in Abstractions)

```csharp
public interface IAssociationRepository<TAssoc, TLeft, TRight> : IRepository<TAssoc>
    where TAssoc : class
    where TLeft : class
    where TRight : class
{
    Task<TAssoc?> FindByEndpointsAsync(object leftKey, object rightKey, CancellationToken ct = default);
    Task<IReadOnlyList<TAssoc>> FindByLeftAsync(object leftKey, CancellationToken ct = default);
    Task<IReadOnlyList<TAssoc>> FindByRightAsync(object rightKey, CancellationToken ct = default);
    Task<IReadOnlyList<TRight>> FindRightsByLeftAsync(object leftKey, CancellationToken ct = default);
    Task<IReadOnlyList<TLeft>> FindLeftsByRightAsync(object rightKey, CancellationToken ct = default);
}
```

#### Generated Entity-Specific Interface

```csharp
// Generated: IOrderRepository.g.cs
namespace MyApp.Domain.Repositories;

public interface IOrderRepository : IRepository<global::MyApp.Domain.Order>
{
    // SG generates typed FindById based on PrimaryKey type
    ValueTask<global::MyApp.Domain.Order?> FindByIdAsync(global::System.Guid id);

    // Developer adds domain-specific queries in a partial interface:
    // Task<IReadOnlyList<Order>> FindByCustomerAsync(Guid customerId);
}
```

#### Generated Base Repository

```csharp
// Generated: OrderRepositoryBase.g.cs — always regenerated
namespace MyApp.Domain.Repositories;

public abstract class OrderRepositoryBase : IOrderRepository
{
    private readonly global::MyApp.Infrastructure.SalesDbContext _context;
    private readonly Microsoft.EntityFrameworkCore.DbSet<global::MyApp.Domain.Order> _dbSet;

    protected OrderRepositoryBase(global::MyApp.Infrastructure.SalesDbContext context)
    {
        _context = context;
        _dbSet = context.Set<global::MyApp.Domain.Order>();
    }

    // ── IRepository<Order> implementation ──
    public virtual ValueTask<global::MyApp.Domain.Order?> FindByIdAsync(params object[] keyValues)
        => _dbSet.FindAsync(keyValues);

    public virtual ValueTask<global::MyApp.Domain.Order?> FindByIdAsync(global::System.Guid id)
        => _dbSet.FindAsync(id);

    public virtual Task<global::MyApp.Domain.Order?> FindAsync(
        Expression<Func<global::MyApp.Domain.Order, bool>> predicate,
        CancellationToken ct = default)
        => _dbSet.FirstOrDefaultAsync(predicate, ct);

    public virtual Task<IReadOnlyList<global::MyApp.Domain.Order>> FindAllAsync(CancellationToken ct = default)
        => _dbSet.ToListAsync(ct).ContinueWith(t => (IReadOnlyList<global::MyApp.Domain.Order>)t.Result, ct);

    public virtual Task<IReadOnlyList<global::MyApp.Domain.Order>> FindWhereAsync(
        Expression<Func<global::MyApp.Domain.Order, bool>> predicate,
        CancellationToken ct = default)
        => _dbSet.Where(predicate).ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<global::MyApp.Domain.Order>)t.Result, ct);

    public virtual Task<bool> ExistsAsync(
        Expression<Func<global::MyApp.Domain.Order, bool>> predicate,
        CancellationToken ct = default)
        => _dbSet.AnyAsync(predicate, ct);

    public virtual Task<int> CountAsync(CancellationToken ct = default)
        => _dbSet.CountAsync(ct);

    public virtual void Add(global::MyApp.Domain.Order entity) => _dbSet.Add(entity);
    public virtual void AddRange(IEnumerable<global::MyApp.Domain.Order> entities) => _dbSet.AddRange(entities);
    public virtual void Update(global::MyApp.Domain.Order entity) => _dbSet.Update(entity);
    public virtual void Remove(global::MyApp.Domain.Order entity) => _dbSet.Remove(entity);
    public virtual void RemoveRange(IEnumerable<global::MyApp.Domain.Order> entities) => _dbSet.RemoveRange(entities);

    public virtual async Task<IReadOnlyList<global::MyApp.Domain.Order>> FindBySpecAsync(
        ISpecification<global::MyApp.Domain.Order> spec,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsQueryable();
        query = query.Where(spec.Criteria);
        foreach (var include in spec.Includes)
            query = query.Include(include);
        if (spec.OrderBy != null) query = query.OrderBy(spec.OrderBy);
        if (spec.OrderByDescending != null) query = query.OrderByDescending(spec.OrderByDescending);
        if (spec.Skip.HasValue) query = query.Skip(spec.Skip.Value);
        if (spec.Take.HasValue) query = query.Take(spec.Take.Value);
        return await query.ToListAsync(ct);
    }

    public IQueryable<global::MyApp.Domain.Order> Query => _dbSet.AsQueryable();

    // ── Protected helpers for derived repositories ──
    protected global::MyApp.Infrastructure.SalesDbContext Context => _context;
    protected Microsoft.EntityFrameworkCore.DbSet<global::MyApp.Domain.Order> DbSet => _dbSet;
}
```

#### Developer's Repository (partial class)

```csharp
// Developer: OrderRepository.cs — never overwritten
namespace MyApp.Domain.Repositories;

public partial class OrderRepository : OrderRepositoryBase
{
    public OrderRepository(SalesDbContext context) : base(context) { }

    // Domain-specific queries
    public Task<IReadOnlyList<Order>> FindByCustomerAsync(Guid customerId, CancellationToken ct = default)
        => FindWhereAsync(o => o.CustomerId == customerId, ct);

    public Task<IReadOnlyList<Order>> FindActiveAsync(CancellationToken ct = default)
        => DbSet.Where(o => o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Order>)t.Result, ct);

    // Override base behavior
    public override void Remove(Order entity)
    {
        // Soft delete instead of hard delete
        entity.Status = OrderStatus.Cancelled;
        Update(entity);
    }
}
```

### 8.3 Unit of Work Pattern — Generation Gap (Same 3-Layer)

```
I{Context}UnitOfWork.g.cs           ← always regenerated (interface with all repo properties)
{Context}UnitOfWorkBase.g.cs        ← always regenerated (abstract, virtual factory methods)
{Context}UnitOfWork.g.cs            ← always regenerated (partial stub + [Injectable])
{Context}UnitOfWork.cs              ← developer-written (partial, optional overrides)
```

The UnitOfWork coordinates multiple repositories in a single transaction. The SG generates the interface, base, and stub. Developer extends via partial class.

#### Interface (in Abstractions — hand-written)

```csharp
namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
    // Commit
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    int SaveChanges();

    // Transaction control
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);

    // Change tracker access
    void DetachAll();
    bool HasChanges { get; }
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

/// <summary>
/// Typed UnitOfWork that exposes all repositories for a given DbContext.
/// Generated per [DbContext].
/// </summary>
public interface IUnitOfWork<TContext> : IUnitOfWork where TContext : Microsoft.EntityFrameworkCore.DbContext
{
    TContext Context { get; }
}
```

#### Generated UnitOfWork (per DbContext)

```csharp
// Generated: ISalesUnitOfWork.g.cs
namespace MyApp.Infrastructure;

public interface ISalesUnitOfWork : IUnitOfWork<global::MyApp.Infrastructure.SalesDbContext>
{
    global::MyApp.Domain.Repositories.IOrderRepository Orders { get; }
    global::MyApp.Domain.Repositories.ICustomerRepository Customers { get; }
    global::MyApp.Domain.Repositories.IAssociationRepository<
        global::MyApp.Domain.Enrollment,
        global::MyApp.Domain.Student,
        global::MyApp.Domain.Course> Enrollments { get; }
}
```

```csharp
// Generated: SalesUnitOfWorkBase.g.cs — always regenerated
namespace MyApp.Infrastructure;

public abstract class SalesUnitOfWorkBase : ISalesUnitOfWork
{
    private readonly global::MyApp.Infrastructure.SalesDbContext _context;

    // Lazy repository initialization
    private global::MyApp.Domain.Repositories.IOrderRepository? _orders;
    private global::MyApp.Domain.Repositories.ICustomerRepository? _customers;

    protected SalesUnitOfWorkBase(global::MyApp.Infrastructure.SalesDbContext context)
    {
        _context = context;
    }

    public global::MyApp.Infrastructure.SalesDbContext Context => _context;

    public global::MyApp.Domain.Repositories.IOrderRepository Orders
        => _orders ??= CreateOrderRepository();

    public global::MyApp.Domain.Repositories.ICustomerRepository Customers
        => _customers ??= CreateCustomerRepository();

    // ── Factory methods — override to inject custom repositories ──
    protected virtual global::MyApp.Domain.Repositories.IOrderRepository CreateOrderRepository()
        => new global::MyApp.Domain.Repositories.OrderRepository(_context);

    protected virtual global::MyApp.Domain.Repositories.ICustomerRepository CreateCustomerRepository()
        => new global::MyApp.Domain.Repositories.CustomerRepository(_context);

    // ── IUnitOfWork ──
    public virtual Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public virtual int SaveChanges()
        => _context.SaveChanges();

    public virtual async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var tx = await _context.Database.BeginTransactionAsync(ct);
        return new UnitOfWorkTransaction(tx);
    }

    public virtual void DetachAll()
        => _context.ChangeTracker.Clear();

    public virtual bool HasChanges
        => _context.ChangeTracker.HasChanges();

    // ── IDisposable / IAsyncDisposable ──
    public void Dispose() => _context.Dispose();
    public ValueTask DisposeAsync() => _context.DisposeAsync();

    // ── Transaction wrapper ──
    private sealed class UnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _tx;
        public UnitOfWorkTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) => _tx = tx;
        public Task CommitAsync(CancellationToken ct) => _tx.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct) => _tx.RollbackAsync(ct);
        public ValueTask DisposeAsync() => _tx.DisposeAsync();
    }
}
```

```csharp
// Generated: SalesUnitOfWork.g.cs — partial stub
namespace MyApp.Infrastructure;

public partial class SalesUnitOfWork : SalesUnitOfWorkBase
{
    public SalesUnitOfWork(global::MyApp.Infrastructure.SalesDbContext context)
        : base(context) { }
}
```

#### Developer Override Example

```csharp
// Developer: SalesUnitOfWork.cs
public partial class SalesUnitOfWork
{
    // Inject a custom repository implementation
    protected override IOrderRepository CreateOrderRepository()
        => new AuditedOrderRepository(Context);

    // Add cross-repository business logic
    public async Task<Order> PlaceOrderAsync(Order order, CancellationToken ct = default)
    {
        Orders.Add(order);
        // Cross-cutting concern: update customer stats
        var customer = await Customers.FindByIdAsync(order.CustomerId);
        customer!.OrderCount++;
        Customers.Update(customer);
        await SaveChangesAsync(ct);
        return order;
    }
}
```

### 8.4 Entity Listeners — Per-Entity Lifecycle Hooks (Doctrine-Style)

Instead of only having global DbContext lifecycle hooks, the SG generates **per-entity listener interfaces** following Doctrine's `EntityListeners` pattern.

#### Interface (in Abstractions)

```csharp
// ── In Abstractions (pure — no EF Core dependency) ──
namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

/// <summary>
/// Per-entity lifecycle listener. Pure typed hooks — no EF Core types.
/// Implement only the hooks you need (all have default empty implementations).
/// </summary>
public interface IEntityLifecycleListener<T> where T : class
{
    Task OnAddingAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    Task OnAddedAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    Task OnModifyingAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    Task OnModifiedAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    Task OnRemovingAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    Task OnRemovedAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
}

// ── In Infra (EF Core dependent) ──
namespace FrenchExDev.Net.Entity.Dsl.Infra;

/// <summary>
/// Per-entity post-materialization listener (Doctrine: postLoad).
/// </summary>
public interface IEntityMaterializationListener<T> where T : class
{
    Task OnLoadedAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// Global listener — fires for ALL entity types (Doctrine EventSubscriber equivalent).
/// Uses EntityEntry (EF Core type) so must live in Infra.
/// </summary>
public interface IGlobalEntityListener
{
    Task OnAddingAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnAddedAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnModifyingAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnModifiedAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnRemovingAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnRemovedAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnLoadedAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
}
```

All methods have **default implementations** — ISP: implement only the hooks you need.

**Interface split (Clean Architecture):**
| Interface | Project | EF Core? | When dispatched |
|---|---|---|---|
| `IEntityLifecycleListener<T>` | **Abstractions** | No | `OnBeforeSaveChanges` (persistence hooks) |
| `IEntityMaterializationListener<T>` | **Infra** | No (conceptually EF) | `IMaterializationInterceptor` (post-load) |
| `IGlobalEntityListener` | **Infra** | Yes (`EntityEntry`) | Both persistence + materialization |

**`[EntityListener(typeof(...))]`** — attribute name stays short. The referenced type can implement one, two, or all three interfaces. The SG dispatcher checks which interfaces are implemented and dispatches accordingly.

#### `[EntityListener]` Attribute — Declares listeners on entity

```csharp
[MetaConcept(typeof(EntityListenerConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class EntityListenerAttribute : Attribute
{
    [MetaProperty("ListenerType", "Type", Required = true)]
    public Type ListenerType { get; }
    public EntityListenerAttribute(Type listenerType) { ListenerType = listenerType; }
}
```

#### Usage

```csharp
// Declare the listener on the entity
[AggregateRoot("Order")]
[Table("Orders")]
[EntityListener(typeof(OrderAuditListener))]
[EntityListener(typeof(OrderNotificationListener))]
public partial class Order { ... }

// Implement the listener — only override what you need
public class OrderAuditListener : IEntityListener<Order>
{
    private readonly IAuditService _audit;
    public OrderAuditListener(IAuditService audit) => _audit = audit;

    public async Task OnAddedAsync(Order entity, CancellationToken ct)
        => await _audit.LogAsync($"Order {entity.Id} created", ct);

    public async Task OnModifyingAsync(Order entity, CancellationToken ct)
        => await _audit.LogAsync($"Order {entity.Id} updating", ct);
}

public class OrderNotificationListener : IEntityListener<Order>
{
    private readonly INotificationService _notifications;
    public OrderNotificationListener(INotificationService notifications) => _notifications = notifications;

    public async Task OnAddedAsync(Order entity, CancellationToken ct)
        => await _notifications.SendAsync($"New order {entity.OrderNumber}", ct);
}
```

#### Generated Dispatcher in DbContextBase

The generated DbContextBase discovers `IEntityListener<T>` implementations from DI and dispatches to them:

```csharp
// In SalesDbContextBase.g.cs
private readonly IServiceProvider? _serviceProvider;

protected SalesDbContextBase(DbContextOptions options, IServiceProvider? serviceProvider = null)
    : base(options)
{
    _serviceProvider = serviceProvider;
}

private async Task DispatchEntityListenersAsync(
    IEnumerable<EntityEntry> entries,
    Func<object, object, CancellationToken, Task> dispatch,
    CancellationToken ct)
{
    if (_serviceProvider == null) return;

    foreach (var entry in entries)
    {
        var entityType = entry.Entity.GetType();
        var listenerType = typeof(IEntityListener<>).MakeGenericType(entityType);
        var listeners = _serviceProvider.GetServices(listenerType);
        foreach (var listener in listeners)
        {
            await dispatch(listener!, entry.Entity, ct);
        }
    }
}

// Called in OnBeforeSaveChanges:
await DispatchEntityListenersAsync(
    adding,
    (listener, entity, ct) => ((dynamic)listener).OnAddingAsync((dynamic)entity, ct),
    ct);
```

### 8.5 Behaviors — Attribute-Based Opt-In (Doctrine Style)

Following Doctrine's `@Gedmo\Timestampable` approach, behaviors are **opt-in via class-level attributes**. The SG detects them and **generates the properties via partial class** — zero boilerplate for the developer.

#### Behavior Attributes (in Entity.Dsl.Attributes)

##### `[Timestampable]` — auto-generated timestamps

```csharp
[MetaConcept(typeof(TimestampableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class TimestampableAttribute : Attribute
{
    // ── Property names ──
    [MetaProperty("CreatedAtName", "string")]
    public string CreatedAtName { get; set; } = "CreatedAt";

    [MetaProperty("UpdatedAtName", "string")]
    public string UpdatedAtName { get; set; } = "UpdatedAt";

    // ── Type & precision ──
    [MetaProperty("Type", "string")]
    public string Type { get; set; } = "DateTimeOffset";       // "DateTimeOffset", "DateTime", "long" (unix ticks)

    [MetaProperty("Precision", "int")]
    public int Precision { get; set; } = 7;                    // fractional seconds (0-7). 7 = 100ns ticks, 3 = ms, 0 = seconds

    // ── Timezone ──
    [MetaProperty("TimeZone", "string")]
    public string TimeZone { get; set; } = "Utc";              // "Utc" (DateTimeOffset.UtcNow), "Local" (Now), "Unspecified"

    // ── Mutability ──
    [MetaProperty("CreatedAtImmutable", "bool")]
    public bool CreatedAtImmutable { get; set; } = true;       // true = never overwrite after first set (even on re-Add)

    // ── Propagation ──
    [MetaProperty("UpdateOnChildChange", "bool")]
    public bool UpdateOnChildChange { get; set; } = false;     // true = update parent's UpdatedAt when [Composition] children change

    // ── Column config ──
    [MetaProperty("CreatedAtColumnName", "string")]
    public string? CreatedAtColumnName { get; set; }           // null = use NamingConvention or property name

    [MetaProperty("UpdatedAtColumnName", "string")]
    public string? UpdatedAtColumnName { get; set; }
}
```

##### `[SoftDeletable]` — soft-delete with query filter

```csharp
[MetaConcept(typeof(SoftDeletableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class SoftDeletableAttribute : Attribute
{
    // ── Property names ──
    [MetaProperty("IsDeletedName", "string")]
    public string IsDeletedName { get; set; } = "IsDeleted";

    [MetaProperty("DeletedAtName", "string")]
    public string DeletedAtName { get; set; } = "DeletedAt";

    // ── Cascade soft-delete ──
    [MetaProperty("CascadeToChildren", "bool")]
    public bool CascadeToChildren { get; set; } = true;        // true = when parent soft-deleted, soft-delete [Composition] children too

    // ── Hard-delete escape hatch ──
    [MetaProperty("AllowHardDelete", "bool")]
    public bool AllowHardDelete { get; set; } = false;         // true = generates HardDelete() method on repository (bypasses filter)

    // ── Query filter ──
    [MetaProperty("FilterEnabled", "bool")]
    public bool FilterEnabled { get; set; } = true;            // true = auto-add HasQueryFilter(e => !e.IsDeleted)

    // ── Restore support ──
    [MetaProperty("AllowRestore", "bool")]
    public bool AllowRestore { get; set; } = true;             // true = generates Restore() method (sets IsDeleted=false, DeletedAt=null)

    // ── Column config ──
    [MetaProperty("IsDeletedColumnName", "string")]
    public string? IsDeletedColumnName { get; set; }

    [MetaProperty("DeletedAtColumnName", "string")]
    public string? DeletedAtColumnName { get; set; }
}
```

##### `[Blameable]` — user tracking

```csharp
[MetaConcept(typeof(BlameableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class BlameableAttribute : Attribute
{
    // ── Property names ──
    [MetaProperty("CreatedByName", "string")]
    public string CreatedByName { get; set; } = "CreatedBy";

    [MetaProperty("UpdatedByName", "string")]
    public string UpdatedByName { get; set; } = "UpdatedBy";

    // ── Track on delete (useful with [SoftDeletable]) ──
    [MetaProperty("TrackDeletedBy", "bool")]
    public bool TrackDeletedBy { get; set; } = false;          // true = generates DeletedBy property

    [MetaProperty("DeletedByName", "string")]
    public string DeletedByName { get; set; } = "DeletedBy";

    // ── User identifier type ──
    [MetaProperty("IdentifierType", "string")]
    public string IdentifierType { get; set; } = "string";     // "string", "Guid", "int", "long"

    // ── Max length (for string identifiers) ──
    [MetaProperty("MaxLength", "int")]
    public int MaxLength { get; set; } = 256;

    // ── Nullable ──
    [MetaProperty("Required", "bool")]
    public bool Required { get; set; } = false;                // false = nullable (anonymous actions allowed)

    // ── Column config ──
    [MetaProperty("CreatedByColumnName", "string")]
    public string? CreatedByColumnName { get; set; }

    [MetaProperty("UpdatedByColumnName", "string")]
    public string? UpdatedByColumnName { get; set; }
}
```

##### `[Versionable]` — optimistic concurrency

```csharp
[MetaConcept(typeof(VersionableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class VersionableAttribute : Attribute
{
    // ── Property name ──
    [MetaProperty("PropertyName", "string")]
    public string PropertyName { get; set; } = "RowVersion";

    // ── Strategy ──
    [MetaProperty("Strategy", "string")]
    public string Strategy { get; set; } = "RowVersion";       // "RowVersion" (byte[], DB-managed),
                                                                // "Guid" (new Guid on each update),
                                                                // "Timestamp" (DateTimeOffset, app-managed),
                                                                // "Increment" (int/long, app-managed +1)

    // ── Type (auto-derived from Strategy, but overridable) ──
    // RowVersion → byte[], Guid → Guid, Timestamp → DateTimeOffset, Increment → long
    [MetaProperty("TypeOverride", "string")]
    public string? TypeOverride { get; set; }                  // null = auto from Strategy

    // ── Column config ──
    [MetaProperty("ColumnName", "string")]
    public string? ColumnName { get; set; }
}
```

**Strategy details:**
| Strategy | CLR Type | EF Core Config | Managed By |
|---|---|---|---|
| `RowVersion` | `byte[]` | `IsRowVersion()` | Database (auto-increment on UPDATE) |
| `Guid` | `Guid` | `IsConcurrencyToken()` | App (new `Guid.NewGuid()` on every save) |
| `Timestamp` | `DateTimeOffset` | `IsConcurrencyToken()` | App (set to `UtcNow` on every save) |
| `Increment` | `long` | `IsConcurrencyToken()` | App (increment +1 on every save) |

##### `[Sluggable]` — URL slug generation

```csharp
[MetaConcept(typeof(SluggableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class SluggableAttribute : Attribute
{
    // ── Source ──
    [MetaProperty("Source", "string", Required = true)]
    public string Source { get; }                              // nameof(Title) — single property
                                                               // or "Title,SubTitle" — concatenated

    [MetaProperty("Separator", "string")]
    public string Separator { get; set; } = "-";               // word separator in slug

    // ── Output property ──
    [MetaProperty("PropertyName", "string")]
    public string PropertyName { get; set; } = "Slug";

    [MetaProperty("MaxLength", "int")]
    public int MaxLength { get; set; } = 256;

    // ── Uniqueness ──
    [MetaProperty("Unique", "bool")]
    public bool Unique { get; set; } = true;                   // unique index on slug

    [MetaProperty("UniqueScope", "string")]
    public string? UniqueScope { get; set; }                   // property to scope uniqueness (e.g., "TenantId")

    [MetaProperty("DuplicateSuffix", "string")]
    public string DuplicateSuffix { get; set; } = "-{n}";      // "-1", "-2" on collision. {n} = counter

    // ── Regeneration ──
    [MetaProperty("Regenerate", "string")]
    public string Regenerate { get; set; } = "OnCreate";       // "OnCreate" (never changes), "Always" (re-slugify on source change)

    // ── Transliteration ──
    [MetaProperty("Transliterate", "bool")]
    public bool Transliterate { get; set; } = true;            // true = é→e, ü→u, ñ→n

    [MetaProperty("Lowercase", "bool")]
    public bool Lowercase { get; set; } = true;                // true = force lowercase

    // ── Column config ──
    [MetaProperty("ColumnName", "string")]
    public string? ColumnName { get; set; }

    public SluggableAttribute(string source) { Source = source; }
}
```

##### `[Loggable]` — audit trail

```csharp
[MetaConcept(typeof(LoggableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class LoggableAttribute : Attribute
{
    // ── Audit table ──
    [MetaProperty("AuditTableName", "string")]
    public string? AuditTableName { get; set; }                // null = "{Entity}AuditLogs"

    [MetaProperty("AuditSchema", "string")]
    public string? AuditSchema { get; set; }                   // null = same schema as entity

    // ── What to track ──
    [MetaProperty("TrackProperties", "string")]
    public string? TrackProperties { get; set; }               // null = all properties. "Title,Status" = only these

    [MetaProperty("IgnoreProperties", "string")]
    public string? IgnoreProperties { get; set; }              // "RowVersion,UpdatedAt" = exclude these

    // ── Which operations ──
    [MetaProperty("LogInsert", "bool")]
    public bool LogInsert { get; set; } = true;

    [MetaProperty("LogUpdate", "bool")]
    public bool LogUpdate { get; set; } = true;

    [MetaProperty("LogDelete", "bool")]
    public bool LogDelete { get; set; } = true;

    // ── What values to capture ──
    [MetaProperty("CaptureOldValues", "bool")]
    public bool CaptureOldValues { get; set; } = true;

    [MetaProperty("CaptureNewValues", "bool")]
    public bool CaptureNewValues { get; set; } = true;

    // ── Storage format for values ──
    [MetaProperty("ValueFormat", "string")]
    public string ValueFormat { get; set; } = "Json";          // "Json" (single JSON column), "Columns" (one column per property)

    // ── Track who made the change (requires [Blameable] or ICurrentUserProvider) ──
    [MetaProperty("TrackUser", "bool")]
    public bool TrackUser { get; set; } = true;

    // ── Retention ──
    [MetaProperty("RetentionDays", "int")]
    public int RetentionDays { get; set; } = 0;                // 0 = unlimited. >0 = generates purge helper method
}
```

**Generated companion entity `{Entity}AuditLog`:**
```csharp
// Order.AuditLog.g.cs
public class OrderAuditLog
{
    public long Id { get; set; }
    public Guid EntityId { get; set; }                  // FK to Order
    public string Operation { get; set; } = "";          // "Insert", "Update", "Delete"
    public string? OldValues { get; set; }               // JSON (when ValueFormat=Json)
    public string? NewValues { get; set; }               // JSON
    public string? ChangedProperties { get; set; }       // comma-separated property names
    public string? UserId { get; set; }                  // from ICurrentUserProvider (when TrackUser=true)
    public DateTimeOffset Timestamp { get; set; }
}
```

##### `[Translatable]` — multi-language support

```csharp
[MetaConcept(typeof(TranslatableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class TranslatableAttribute : Attribute
{
    // ── Default locale ──
    [MetaProperty("DefaultLocale", "string")]
    public string DefaultLocale { get; set; } = "en";

    // ── Translation table ──
    [MetaProperty("TranslationTableName", "string")]
    public string? TranslationTableName { get; set; }          // null = "{Entity}Translations"

    [MetaProperty("TranslationSchema", "string")]
    public string? TranslationSchema { get; set; }

    // ── Locale column config ──
    [MetaProperty("LocaleColumnName", "string")]
    public string LocaleColumnName { get; set; } = "Locale";

    [MetaProperty("LocaleMaxLength", "int")]
    public int LocaleMaxLength { get; set; } = 10;             // "en", "en-US", "zh-Hans"

    // ── Fallback strategy ──
    [MetaProperty("Fallback", "string")]
    public string Fallback { get; set; } = "DefaultLocale";    // "DefaultLocale" (return default if missing),
                                                                // "Null" (return null if no translation),
                                                                // "Throw" (throw if no translation)

    // ── Index ──
    [MetaProperty("UniquePerLocale", "bool")]
    public bool UniquePerLocale { get; set; } = true;          // unique index on (EntityId, Locale)
}
// + [TranslatableProperty] on individual properties marks which props are translatable
```

**Generated companion entity `{Entity}Translation`:**
```csharp
// Article.Translation.g.cs
public class ArticleTranslation
{
    public int Id { get; set; }
    public Guid ArticleId { get; set; }                  // FK to Article
    public Article Article { get; set; } = null!;
    public string Locale { get; set; } = "";             // "en", "fr", "de"

    // One property per [TranslatableProperty] on Article:
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}
```

##### `[Sortable]` — automatic ordering

```csharp
[MetaConcept(typeof(SortableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class SortableAttribute : Attribute
{
    // ── Property ──
    [MetaProperty("PropertyName", "string")]
    public string PropertyName { get; set; } = "Position";

    // ── Grouping ──
    [MetaProperty("GroupBy", "string")]
    public string? GroupBy { get; set; }                       // "ParentId" — position resets per group
                                                               // "ParentId,TenantId" — multiple group keys

    // ── Start value ──
    [MetaProperty("StartAt", "int")]
    public int StartAt { get; set; } = 0;                     // 0-based or 1-based

    // ── On delete behavior ──
    [MetaProperty("OnDelete", "string")]
    public string OnDelete { get; set; } = "Reorder";         // "Reorder" (close gap, decrement others),
                                                               // "LeaveGap" (don't touch others)

    // ── On insert behavior ──
    [MetaProperty("OnInsert", "string")]
    public string OnInsert { get; set; } = "AppendLast";      // "AppendLast" (max+1),
                                                               // "PrependFirst" (shift others, insert at StartAt)

    // ── Column config ──
    [MetaProperty("ColumnName", "string")]
    public string? ColumnName { get; set; }

    // ── Index ──
    [MetaProperty("CreateIndex", "bool")]
    public bool CreateIndex { get; set; } = true;              // index on (GroupBy, Position)
}
```

##### `[TreeNode]` — hierarchical tree structure

```csharp
[MetaConcept(typeof(TreeNodeConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class TreeNodeAttribute : Attribute
{
    // ── Strategy ──
    [MetaProperty("Strategy", "string")]
    public string Strategy { get; set; } = "MaterializedPath"; // "MaterializedPath" (path string),
                                                                // "AdjacencyList" (ParentId only, no path/depth),
                                                                // "ClosureTable" (separate closure table)

    // ── Property names ──
    [MetaProperty("ParentIdName", "string")]
    public string ParentIdName { get; set; } = "ParentId";

    [MetaProperty("ParentNavigationName", "string")]
    public string ParentNavigationName { get; set; } = "Parent";

    [MetaProperty("ChildrenNavigationName", "string")]
    public string ChildrenNavigationName { get; set; } = "Children";

    // ── MaterializedPath-specific ──
    [MetaProperty("PathName", "string")]
    public string PathName { get; set; } = "MaterializedPath"; // only generated when Strategy=MaterializedPath

    [MetaProperty("DepthName", "string")]
    public string DepthName { get; set; } = "Depth";

    [MetaProperty("PathSeparator", "string")]
    public string PathSeparator { get; set; } = "/";

    [MetaProperty("PathMaxLength", "int")]
    public int PathMaxLength { get; set; } = 1024;             // max length of materialized path string

    // ── ClosureTable-specific ──
    [MetaProperty("ClosureTableName", "string")]
    public string? ClosureTableName { get; set; }              // null = "{Entity}TreeClosure"

    // ── Depth limit ──
    [MetaProperty("MaxDepth", "int")]
    public int MaxDepth { get; set; } = 0;                     // 0 = unlimited. >0 = SG emits validation

    // ── Self-referencing FK ──
    [MetaProperty("OnDelete", "string")]
    public string OnDelete { get; set; } = "Restrict";         // "Restrict" (prevent delete if has children),
                                                                // "Cascade" (delete subtree),
                                                                // "SetNull" (orphan children to root)

    // ── Sibling ordering (combine with [Sortable] semantics) ──
    [MetaProperty("OrderChildrenBy", "string")]
    public string? OrderChildrenBy { get; set; }               // null = no ordering. "Position" = combine with [Sortable]
                                                                // "Name" = order by property

    // ── Query helpers ──
    [MetaProperty("GenerateQueryExtensions", "bool")]
    public bool GenerateQueryExtensions { get; set; } = true;  // generates IsAncestorOf, IsDescendantOf,
                                                                // GetAncestors, GetDescendants, GetSiblings, GetSubtree
}
```

**Strategy comparison:**
| | AdjacencyList | MaterializedPath | ClosureTable |
|---|---|---|---|
| Generated properties | `ParentId`, `Parent`, `Children` | + `MaterializedPath`, `Depth` | + separate closure table |
| Read ancestors | N+1 queries (recursive) | Single `LIKE` query on path | Single JOIN on closure table |
| Read descendants | Recursive CTE | Single `LIKE` prefix query | Single JOIN |
| Move subtree | Update 1 row | Update all descendant paths | Delete + re-insert closure rows |
| Insert | O(1) | O(1) + path computation | O(depth) closure inserts |
| Storage overhead | Minimal | Path string per node | O(n*depth) closure rows |
| Best for | Shallow trees, rare reads | Read-heavy, moderate depth | Deep trees, frequent subtree queries |

#### Required DI Service for `[Blameable]`

```csharp
// In Abstractions — developer implements and registers with [Injectable]
namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

public interface ICurrentUserProvider
{
    string? GetCurrentUserId();
}

// Developer implementation example:
[Injectable(Scope = Scope.Scoped, As = typeof(ICurrentUserProvider))]
public class HttpCurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public HttpCurrentUserProvider(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public string? GetCurrentUserId()
        => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
```

#### Slug Helper (in Abstractions)

```csharp
public static class SlugHelper
{
    public static string ToSlug(string input)
    {
        // Lowercase, replace spaces/special chars with hyphens, trim, collapse
        // Unicode-aware (remove diacritics: é → e, ü → u)
    }
}
```

#### What the SG Generates Per Behavior

For each behavior attribute, the SG generates **3 things**:

**1. Properties** — in `{Entity}.Behaviors.g.cs` (partial class):

```csharp
// Order.Behaviors.g.cs — always regenerated
public partial class Order
{
    // [Timestampable]
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // [SoftDeletable]
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // [Blameable]
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // [Sluggable(nameof(Title))]
    public string Slug { get; set; } = "";

    // [Versionable]
    public byte[] RowVersion { get; set; } = null!;
}
```

**2. Configuration** — virtual methods in `{Entity}ConfigurationBase.g.cs`:

```csharp
protected virtual void ConfigureTimestampable(EntityTypeBuilder<Order> builder)
{
    builder.Property(e => e.CreatedAt).IsRequired();
    builder.Property(e => e.UpdatedAt).IsRequired();
}

protected virtual void ConfigureSoftDeletable(EntityTypeBuilder<Order> builder)
{
    builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
    builder.Property(e => e.DeletedAt);
    builder.HasQueryFilter(e => !e.IsDeleted);
}

protected virtual void ConfigureSluggable(EntityTypeBuilder<Order> builder)
{
    builder.Property(e => e.Slug).IsRequired().HasMaxLength(256);
    builder.HasIndex(e => e.Slug).IsUnique();
}

protected virtual void ConfigureVersionable(EntityTypeBuilder<Order> builder)
{
    builder.Property(e => e.RowVersion).IsRowVersion();
}
```

**3. SaveChanges hooks** — in `DbContextBase.g.cs`:

```csharp
private void PopulateBehaviors(IEnumerable<EntityEntry> entries)
{
    var now = DateTimeOffset.UtcNow;
    var userId = _serviceProvider?.GetService<ICurrentUserProvider>()?.GetCurrentUserId();

    foreach (var entry in entries)
    {
        // [Timestampable] — any entity with these generated properties
        if (entry.Metadata.FindProperty("CreatedAt") != null)
        {
            if (entry.State == EntityState.Added)
                entry.Property("CreatedAt").CurrentValue = now;
            entry.Property("UpdatedAt").CurrentValue = now;
        }

        // [Blameable]
        if (entry.Metadata.FindProperty("CreatedBy") != null && userId != null)
        {
            if (entry.State == EntityState.Added)
                entry.Property("CreatedBy").CurrentValue = userId;
            entry.Property("UpdatedBy").CurrentValue = userId;
        }

        // [SoftDeletable] — intercept Remove → soft-delete instead
        if (entry.State == EntityState.Deleted
            && entry.Metadata.FindProperty("IsDeleted") != null)
        {
            entry.State = EntityState.Modified;
            entry.Property("IsDeleted").CurrentValue = true;
            entry.Property("DeletedAt").CurrentValue = now;
        }

        // [Sluggable] — compute slug on Add/Modify
        // (entity-specific — emitted per entity that has [Sluggable])
    }
}
```

#### Summary Table

| Behavior | Generated Properties | Generated Config | SaveChanges Hook | Extra |
|---|---|---|---|---|
| `[Timestampable]` | `CreatedAt`, `UpdatedAt` | `IsRequired()` | Set on Add/Modify | — |
| `[SoftDeletable]` | `IsDeleted`, `DeletedAt` | `HasDefaultValue(false)`, `HasQueryFilter` | Intercept Remove → soft-delete | Override `Remove()` in RepoBase |
| `[Blameable]` | `CreatedBy`, `UpdatedBy` | — | Resolve `ICurrentUserProvider` → set | `[Injectable(Scope.Scoped)]` on impl |
| `[Versionable]` | `RowVersion` (byte[]) | `IsRowVersion()` | — (EF Core handles) | — |
| `[Sluggable("Title")]` | `Slug` | `HasMaxLength`, unique index | Compute slug on Add/Modify | `SlugHelper` in Abstractions |
| `[Loggable]` | — | — | Capture old/new on Modify/Remove | Generates companion `{Entity}AuditLog` entity + config |
| `[Translatable]` | `DefaultLocale` | — | — | Generates companion `{Entity}Translation` entity |
| `[Sortable]` | `Position` | — | Auto-assign on Add (max+1) | Reorder on Remove |
| `[TreeNode]` | `ParentId`, `MaterializedPath`, `Depth` | FK to self, index on path | Auto-maintain on Add/Move | Query extension methods |

#### Generated Artifact Per Behavior

| File | Purpose |
|---|---|
| `{Entity}.Behaviors.g.cs` | Partial class with generated properties |
| `ConfigureXxx()` in `{Entity}ConfigurationBase.g.cs` | Virtual method per behavior (overridable) |
| `PopulateBehaviors()` in `DbContextBase.g.cs` | SaveChanges auto-population |
| `{Entity}AuditLog.g.cs` | Companion entity (only for `[Loggable]`) |
| `{Entity}AuditLogConfiguration.g.cs` | Companion config (only for `[Loggable]`) |
| `{Entity}Translation.g.cs` | Companion entity (only for `[Translatable]`) |
| `{Entity}TranslationConfiguration.g.cs` | Companion config (only for `[Translatable]`) |

#### Usage — Zero Boilerplate

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
[Timestampable]
[SoftDeletable]
[Blameable]
[Sluggable(nameof(OrderNumber))]
[Versionable]
public partial class Order
{
    [PrimaryKey] public Guid Id { get; set; }
    [Property("OrderNumber", Required = true, MaxLength = 50)] public string OrderNumber { get; set; } = "";
    [Property("Total", Required = true)] public decimal Total { get; set; }
    // Zero behavior properties declared — SG generates them all in Order.Behaviors.g.cs
}
```

**Behavior vs `[ShadowProperty]`**: Behaviors generate **real CLR properties** on the entity (visible to domain code, queryable with LINQ). Shadow properties are invisible to the domain. Use behaviors when the domain needs to read/query the values; use shadow properties when it's purely infrastructure.

### 8.6 Custom Conventions — Generated Convention Classes

The SG generates `IModelFinalizingConvention` implementations for cross-cutting concerns that apply to all entities.

```csharp
// Generated: EntityDslConventions.g.cs — always regenerated
namespace MyApp.Infrastructure.Conventions;

/// <summary>Applies NamingConvention to all entities that declare one.</summary>
public sealed class NamingConventionModelConvention
    : Microsoft.EntityFrameworkCore.Metadata.Conventions.IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        // NamingStrategy.SnakeCase applied to all properties of entities
        // that declared [NamingConvention(Strategy = SnakeCase)]
    }
}

/// <summary>Applies ISoftDeletable query filter globally.</summary>
public sealed class SoftDeletableConvention
    : Microsoft.EntityFrameworkCore.Metadata.Conventions.IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                // Auto-add HasQueryFilter for ISoftDeletable
            }
        }
    }
}
```

Registered in DbContextBase:
```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Conventions.Add(() => new NamingConventionModelConvention());
    configurationBuilder.Conventions.Add(() => new SoftDeletableConvention());
}
```

### 8.7 DI Registration — `[Injectable]` from Injectable Project

Entity.Dsl **reuses** the existing `[Injectable]` attribute from `FrenchExDev.Net.Injectable.Attributes` for all standard service registrations. The Entity.Dsl SG emits `[Injectable]` on generated classes — the Injectable SG then picks them up and generates the DI extension method.

**Rule: own service = `[Injectable]`**. Only things that require special EF Core registration APIs (like `AddDbContext`) get a dedicated generated extension method.

#### Generated Repository with `[Injectable]`

```csharp
// Generated: OrderRepository.g.cs — partial stub
using FrenchExDev.Net.Injectable.Attributes;

namespace MyApp.Domain.Repositories;

[Injectable(Scope = Scope.Scoped, As = typeof(IOrderRepository))]
public partial class OrderRepository : OrderRepositoryBase
{
    public OrderRepository(Microsoft.EntityFrameworkCore.DbContext context) : base(context) { }
}
```

The Injectable SG sees `[Injectable(Scope.Scoped, As = typeof(IOrderRepository))]` and generates:
```csharp
services.AddScoped<IOrderRepository, OrderRepository>();
```

#### Generated UnitOfWork with `[Injectable]`

```csharp
// Generated: SalesUnitOfWork.g.cs — partial stub
[Injectable(Scope = Scope.Scoped, As = typeof(ISalesUnitOfWork))]
public partial class SalesUnitOfWork : SalesUnitOfWorkBase
{
    public SalesUnitOfWork(SalesDbContext context) : base(context) { }
}
```

#### Entity Listeners — Developer-Written with `[Injectable]`

Listeners are written by the developer, so the developer adds `[Injectable]` themselves:

```csharp
[Injectable(Scope = Scope.Scoped, As = typeof(IEntityListener<Order>))]
public class OrderAuditListener : IEntityListener<Order>
{
    private readonly IAuditService _audit;
    public OrderAuditListener(IAuditService audit) => _audit = audit;

    public async Task OnAddedAsync(Order entity, CancellationToken ct)
        => await _audit.LogAsync($"Order {entity.Id} created", ct);
}
```

#### DbContext — Dedicated Extension (cannot use `[Injectable]`)

`AddDbContext` requires a configuration lambda and special EF Core lifetime management. This is the **only** thing that needs a generated extension method:

```csharp
// Generated: SalesDbContextRegistration.g.cs
namespace Microsoft.Extensions.DependencyInjection;

public static class SalesDbContextRegistration
{
    public static IServiceCollection AddSalesDbContext(
        this IServiceCollection services,
        Action<Microsoft.EntityFrameworkCore.DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<global::MyApp.Infrastructure.SalesDbContext>(configureDbContext);
        return services;
    }
}
```

#### Startup Registration

```csharp
// In Program.cs / Startup.cs:
services.AddSalesDbContext(o => o.UseSqlite("..."));  // DbContext (generated, EF-specific)
services.AddMyAppInjectables();                        // Everything else (Injectable SG)
```

#### Dependency: Attributes project references Injectable.Attributes

```
FrenchExDev.Net.Entity.Dsl.Attributes
    ──> FrenchExDev.Net.Injectable.Attributes   ← NEW dependency
```

The Entity.Dsl SG emits `[Injectable]` attributes on generated classes. The Injectable SG (already an analyzer in the consuming project) picks them up and generates the DI registration extension method. **Two SGs cooperating, each doing what it's good at.**

### 8.8 Full Generated Artifact Summary Per Entity

For each `[Entity]` / `[AggregateRoot]` / `[AssociationClass]`:

| File | Regenerated? | Purpose |
|---|---|---|
| `{Entity}ConfigurationBase.g.cs` | Always | Abstract base with virtual Configure* methods |
| `{Entity}Configuration.g.cs` | Always | Partial stub extending Base |
| `{Entity}ConfigurationRegistration.g.cs` | Always | `IEntityTypeConfiguration<T>` delegate |
| `I{Entity}Repository.g.cs` | Always | Entity-specific repository interface |
| `{Entity}RepositoryBase.g.cs` | Always | Repository base implementation |
| `{Entity}Repository.g.cs` | Always | Partial stub with `[Injectable]` (developer extends via second partial) |
| `{Entity}.AssociationNavigations.g.cs` | Only for `[AssociationClass]` | Skip navigations on Left/Right endpoint entities |

Per `[DbContext]`:

| File | Regenerated? | Purpose |
|---|---|---|
| `{Context}Base.g.cs` | Always | Abstract DbContext with hooks + lifecycle dispatch |
| `{Context}.g.cs` | Always | Partial stub |
| `I{Context}UnitOfWork.g.cs` | Always | UnitOfWork interface with all repositories |
| `{Context}UnitOfWorkBase.g.cs` | Always | UnitOfWork base with factory methods |
| `{Context}UnitOfWork.g.cs` | Always | Partial stub |
| `{Context}Registration.g.cs` | Always | `AddDbContext` extension (only EF-specific registration) |
| `EntityDslConventions.g.cs` | Always | Custom conventions (naming, soft delete) |

### 8.9 SOLID Principle Mapping

| Principle | How Entity.Dsl Applies It |
|---|---|
| **S** — Single Responsibility | Each generated file has one job: config, repo, listener, UoW |
| **O** — Open/Closed | Base classes are sealed behavior; partial classes + interfaces allow extension without modification |
| **L** — Liskov Substitution | `OrderRepository` is substitutable for `IOrderRepository` everywhere |
| **I** — Interface Segregation | `IReadOnlyRepository<T>` for queries, `IRepository<T>` extends with commands (CQRS). `IEntityListener<T>` has default methods — implement only what you need. `IAssociationRepository` extends only for association classes |
| **D** — Dependency Inversion | Services depend on `IOrderRepository`, `ISalesUnitOfWork` — never on concrete classes or DbContext directly |

---

## 9. Generation Gap Pattern — Intermediate Classes with Override Hooks

Following the Symfony/Diem CMF pattern, the SG produces a **3-layer class hierarchy** for both entity configurations and DbContext. Developers never touch generated code — they override behavior in an intermediate class that survives regeneration.

### 8.1 The 3-Layer Hierarchy

```
┌─────────────────────────────────────────────────┐
│  Layer 1: Base_Generated (auto-generated)       │  ← SG overwrites on every build
│  Contains: all Fluent API calls from attributes │
│  Class: abstract, all methods virtual           │
├─────────────────────────────────────────────────┤
│  Layer 2: Intermediate (auto-generated ONCE)    │  ← SG creates only if missing
│  Contains: empty overrides, developer extends   │  ← Developer edits THIS class
│  Class: inherits Base, overrides hooks          │
├─────────────────────────────────────────────────┤
│  Layer 3: Registration (auto-generated)         │  ← SG overwrites, wires it all up
│  Contains: IEntityTypeConfiguration<T> impl     │
│  Delegates to Intermediate                      │
└───��─────────────────────────────────────────────┘
```

### 8.2 Entity Configuration — Generated Files

For an entity `Order`, the SG produces **3 files**:

#### File 1: `OrderConfigurationBase.g.cs` — **always regenerated**

```csharp
// <auto-generated/> — DO NOT EDIT. Changes will be overwritten.
#nullable enable

namespace MyApp.Domain.Configuration;

/// <summary>
/// Base configuration for <see cref="global::MyApp.Domain.Order"/>.
/// Override methods in <see cref="OrderConfiguration"/> to customize behavior.
/// </summary>
[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public abstract class OrderConfigurationBase
{
    // ── Virtual hook: called before any configuration ──
    protected virtual void PreConfigure(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder) { }

    // ── Virtual hook: called after all configuration ──
    protected virtual void PostConfigure(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder) { }

    // ── Table mapping (override to change table/schema) ──
    protected virtual void ConfigureTable(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.ToTable("Orders", "sales");
    }

    // ── Primary key (override to change key strategy) ──
    protected virtual void ConfigurePrimaryKey(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
    }

    // ── Per-property configuration (one virtual method per property) ──
    protected virtual void ConfigureOrderNumber(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.Property(e => e.OrderNumber)
            .HasColumnName("order_number")
            .IsRequired()
            .HasMaxLength(50);
    }

    protected virtual void ConfigureTotal(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.Property(e => e.Total)
            .HasColumnName("total")
            .IsRequired()
            .HasPrecision(18, 2);
    }

    protected virtual void ConfigureStatus(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>();
    }

    protected virtual void ConfigureRowVersion(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();
    }

    // ── Shadow properties ──
    protected virtual void ConfigureShadowProperties(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.Property<global::System.DateTimeOffset>("CreatedAt")
            .HasColumnName("created_at")
            .HasDefaultValueSql("GETUTCDATE()");
        builder.Property<global::System.DateTimeOffset>("UpdatedAt")
            .HasColumnName("updated_at");
    }

    // ── Indexes ──
    protected virtual void ConfigureIndexes(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.HasIndex(e => e.OrderNumber).IsUnique();
    }

    // ── Per-relationship configuration (one virtual method per relationship) ──
    protected virtual void ConfigureItems(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.HasMany(e => e.Items)
            .WithOne(e => e.Order)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
    }

    protected virtual void ConfigureCustomer(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.HasOne(e => e.Customer)
            .WithMany(e => e.Orders)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }

    protected virtual void ConfigureShippingAddress(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.OwnsOne(e => e.ShippingAddress);
    }

    protected virtual void ConfigureTags(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.HasMany(e => e.Tags)
            .WithMany()
            .UsingEntity<global::MyApp.Domain.OrderTag>();
    }

    // ── Check constraints ──
    protected virtual void ConfigureCheckConstraints(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        builder.HasCheckConstraint("CK_Orders_Total", "\"Total\" >= 0");
    }

    // ── Orchestrator: calls all virtual methods in order ──
    public void Configure(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        PreConfigure(builder);
        ConfigureTable(builder);
        ConfigurePrimaryKey(builder);
        ConfigureOrderNumber(builder);
        ConfigureTotal(builder);
        ConfigureStatus(builder);
        ConfigureRowVersion(builder);
        ConfigureShadowProperties(builder);
        ConfigureIndexes(builder);
        ConfigureItems(builder);
        ConfigureCustomer(builder);
        ConfigureShippingAddress(builder);
        ConfigureTags(builder);
        ConfigureCheckConstraints(builder);
        PostConfigure(builder);
    }
}
```

#### File 2: `OrderConfiguration.cs` — **generated ONCE, then developer-owned**

The SG generates this file **only if it doesn't already exist**. Once created, it belongs to the developer. The SG never overwrites it.

```csharp
// This file is yours to customize. The source generator will NOT overwrite it.
// Override any method from OrderConfigurationBase to change behavior.

namespace MyApp.Domain.Configuration;

public sealed class OrderConfiguration : OrderConfigurationBase
{
    // Example overrides (uncomment and modify as needed):
    //
    // protected override void ConfigureTotal(EntityTypeBuilder<Order> builder)
    // {
    //     base.ConfigureTotal(builder);  // keep defaults
    //     // add custom config...
    // }
    //
    // protected override void PreConfigure(EntityTypeBuilder<Order> builder)
    // {
    //     // runs before all configuration
    // }
    //
    // protected override void PostConfigure(EntityTypeBuilder<Order> builder)
    // {
    //     // runs after all configuration
    // }
}
```

#### File 3: `OrderConfigurationRegistration.g.cs` — **always regenerated**

```csharp
// <auto-generated/>
#nullable enable

namespace MyApp.Domain.Configuration;

[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class OrderConfigurationRegistration
    : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<global::MyApp.Domain.Order>
{
    public void Configure(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        new OrderConfiguration().Configure(builder);
    }
}
```

### 8.3 Developer Override Examples

**Example 1: Add a custom index not expressible in attributes**
```csharp
public sealed class OrderConfiguration : OrderConfigurationBase
{
    protected override void PostConfigure(EntityTypeBuilder<Order> builder)
    {
        // Filtered index — not supported by DSL attributes
        builder.HasIndex(e => e.Status)
            .HasFilter("\"Status\" != 'Cancelled'")
            .HasDatabaseName("IX_Orders_ActiveStatus");
    }
}
```

**Example 2: Override a relationship's cascade behavior for a specific environment**
```csharp
public sealed class OrderConfiguration : OrderConfigurationBase
{
    protected override void ConfigureItems(EntityTypeBuilder<Order> builder)
    {
        // Don't call base — completely replace
        builder.HasMany(e => e.Items)
            .WithOne(e => e.Order)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.SetNull);  // different from DSL's Cascade
    }
}
```

**Example 3: Add provider-specific configuration**
```csharp
public sealed class OrderConfiguration : OrderConfigurationBase
{
    protected override void ConfigureTable(EntityTypeBuilder<Order> builder)
    {
        base.ConfigureTable(builder);
        // SQL Server-specific: partition by Status
        builder.ToTable(tb => tb.HasTrigger("trg_Orders_Audit"));
    }
}
```

**Example 4: Completely skip a generated property config**
```csharp
public sealed class OrderConfiguration : OrderConfigurationBase
{
    protected override void ConfigureTotal(EntityTypeBuilder<Order> builder)
    {
        // Intentionally empty — let EF Core conventions handle it
    }
}
```

### 8.4 DbContext — Same Pattern

```
AppDbContextBase.g.cs          ← always regenerated (DbSets, OnModelCreating, interceptor)
AppDbContext.cs                ← generated ONCE, developer-owned
```

#### `AppDbContextBase.g.cs`
```csharp
// <auto-generated/>
public abstract class AppDbContextBase : Microsoft.EntityFrameworkCore.DbContext
{
    protected AppDbContextBase(
        Microsoft.EntityFrameworkCore.DbContextOptions options) : base(options) { }

    // ── DbSets (always regenerated) ──
    public Microsoft.EntityFrameworkCore.DbSet<global::MyApp.Domain.Order> Orders { get; set; } = null!;
    public Microsoft.EntityFrameworkCore.DbSet<global::MyApp.Domain.Customer> Customers { get; set; } = null!;

    // ── Hooks ──
    protected virtual void PreModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder) { }
    protected virtual void PostModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder) { }

    // ── Entity registration (override to add/remove configurations) ──
    protected virtual void RegisterConfigurations(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new global::MyApp.Domain.Configuration.OrderConfigurationRegistration());
        modelBuilder.ApplyConfiguration(new global::MyApp.Domain.Configuration.CustomerConfigurationRegistration());
    }

    // ── Sequences ──
    protected virtual void ConfigureSequences(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        // modelBuilder.HasSequence<long>("OrderNumbers", "sales")...
    }

    // ── Shadow property timestamp interceptor ──
    protected virtual void UpdateTimestamps()
    {
        var now = global::System.DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Added
                && entry.Metadata.FindProperty("CreatedAt") != null)
                entry.Property("CreatedAt").CurrentValue = now;
            if ((entry.State == Microsoft.EntityFrameworkCore.EntityState.Added
                || entry.State == Microsoft.EntityFrameworkCore.EntityState.Modified)
                && entry.Metadata.FindProperty("UpdatedAt") != null)
                entry.Property("UpdatedAt").CurrentValue = now;
        }
    }

    // ── Lifecycle hooks (Doctrine-style) ──
    protected virtual void OnEntitiesAdding(
        global::System.Collections.Generic.IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> entries) { }
    protected virtual void OnEntitiesModifying(
        global::System.Collections.Generic.IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> entries) { }
    protected virtual void OnEntitiesDeleting(
        global::System.Collections.Generic.IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> entries) { }

    // ── Orchestration ──
    protected sealed override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        PreModelCreating(modelBuilder);
        ConfigureSequences(modelBuilder);
        RegisterConfigurations(modelBuilder);
        PostModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        OnBeforeSaveChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override global::System.Threading.Tasks.Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        global::System.Threading.CancellationToken cancellationToken = default)
    {
        OnBeforeSaveChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void OnBeforeSaveChanges()
    {
        UpdateTimestamps();
        var entries = ChangeTracker.Entries().ToList();
        OnEntitiesAdding(entries.Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added));
        OnEntitiesModifying(entries.Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Modified));
        OnEntitiesDeleting(entries.Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Deleted));
    }
}
```

#### `AppDbContext.cs` — generated ONCE
```csharp
// This file is yours to customize. The source generator will NOT overwrite it.

namespace MyApp.Infrastructure;

public partial class AppDbContext : AppDbContextBase
{
    public AppDbContext(
        Microsoft.EntityFrameworkCore.DbContextOptions<AppDbContext> options) : base(options) { }

    // Override hooks as needed:
    //
    // protected override void PreModelCreating(ModelBuilder modelBuilder) { }
    // protected override void PostModelCreating(ModelBuilder modelBuilder) { }
    // protected override void OnEntitiesAdding(IEnumerable<EntityEntry> entries) { }
    // protected override void OnEntitiesModifying(IEnumerable<EntityEntry> entries) { }
    // protected override void OnEntitiesDeleting(IEnumerable<EntityEntry> entries) { }
    // protected override void UpdateTimestamps() { base.UpdateTimestamps(); /* custom */ }
}
```

### 8.5 Override Hook Summary

| Hook | Layer | When | Use Case |
|---|---|---|---|
| `PreConfigure(builder)` | EntityConfig | Before all property/relationship config | Global entity config (e.g., table comments) |
| `Configure{Property}(builder)` | EntityConfig | Per property | Override column type, add provider-specific config |
| `Configure{Relationship}(builder)` | EntityConfig | Per relationship | Change cascade, add custom FK constraints |
| `ConfigureTable(builder)` | EntityConfig | Table mapping | Change table name, add triggers |
| `ConfigureIndexes(builder)` | EntityConfig | Index setup | Add filtered indexes, include columns |
| `ConfigureShadowProperties(builder)` | EntityConfig | Shadow props | Add/remove shadow properties |
| `ConfigureCheckConstraints(builder)` | EntityConfig | Constraints | Add/modify check constraints |
| `PostConfigure(builder)` | EntityConfig | After all config | Catch-all for anything not covered |
| `PreModelCreating(modelBuilder)` | DbContext | Before entity registration | Global conventions, shared config |
| `PostModelCreating(modelBuilder)` | DbContext | After entity registration | Provider-specific global config |
| `RegisterConfigurations(modelBuilder)` | DbContext | Entity registration | Add/remove entity configs dynamically |
| `ConfigureSequences(modelBuilder)` | DbContext | Sequence definitions | Override sequence parameters |
| `UpdateTimestamps()` | DbContext | Before SaveChanges | Custom timestamp logic |
| `OnEntitiesAdding(entries)` | DbContext | Before SaveChanges | Pre-insert hooks (Doctrine prePersist) |
| `OnEntitiesModifying(entries)` | DbContext | Before SaveChanges | Pre-update hooks (Doctrine preUpdate) |
| `OnEntitiesDeleting(entries)` | DbContext | Before SaveChanges | Pre-delete hooks (Doctrine preRemove) |

### 8.6 How the SG Handles the "Generate Once" File

The SG uses `context.RegisterImplementationSourceOutput` for always-regenerated files (`*Base.g.cs`, `*Registration.g.cs`) and checks for existing files for the intermediate class:

**Strategy**: The SG emits the intermediate file as an **AdditionalText hint** with a special `// entity-dsl:generate-once` marker. A custom MSBuild target:
1. Checks if `{ClassName}Configuration.cs` already exists in the project
2. If not → copies the generated template to the project directory
3. If yes → skips (developer's file is preserved)

Alternative (simpler): The SG always emits all 3 files but the intermediate file is marked `[GeneratedCode]` with instructions. The developer creates their own file that inherits from Base, and the Registration file is updated to instantiate the developer's class if it exists (discovered via `ForAttributeWithMetadataName` on a `[EntityConfiguration(typeof(Order))]` marker, or simply by convention: if a non-generated class inheriting `OrderConfigurationBase` exists, use it).

**Chosen approach**: Convention-based discovery.
- SG always generates `OrderConfigurationBase.g.cs` and `OrderConfigurationRegistration.g.cs`
- Registration file instantiates `OrderConfiguration` (no `new` qualifier — works whether it's the generated default or a developer-written class)
- SG also generates a **default** `OrderConfiguration.g.cs` that simply extends Base with no overrides
- If the developer creates their own `OrderConfiguration.cs` (non-generated, same class name), the compiler will error on duplicate — developer must delete the `.g.cs` or the SG detects the developer file and skips emitting the default

**Better approach — partial class**: Make `OrderConfiguration` a `partial class`:
- SG emits `OrderConfiguration.g.partial.cs` with `partial class OrderConfiguration : OrderConfigurationBase { }`
- Developer creates `OrderConfiguration.cs` with `partial class OrderConfiguration { override ... }`
- Both compile together, no conflict

### 8.7 Final File Layout Per Entity

```
Generated (obj/):
  OrderConfigurationBase.g.cs           ← always regenerated, all virtual methods
  OrderConfiguration.g.cs              ← partial class stub (extends Base, empty)
  OrderConfigurationRegistration.g.cs  ← IEntityTypeConfiguration<T>, delegates to OrderConfiguration

Developer (src/):
  OrderConfiguration.cs                ← partial class, developer overrides (optional)
```

If the developer never creates `OrderConfiguration.cs`, the generated partial stub is used — zero customization, pure DSL-driven. When they need to override, they add the partial file and override specific virtual methods.

---

## 10. Generated Code Examples

### Full Example: Order Aggregate

#### Input
```csharp
[AggregateRoot("Order", BoundedContext = "Sales")]
[Table("Orders", Schema = "sales")]
[ShadowProperty("CreatedAt", "DateTimeOffset", DefaultValueSql = "GETUTCDATE()")]
[ShadowProperty("UpdatedAt", "DateTimeOffset")]
[CheckConstraint("CK_Orders_Total", "\"Total\" >= 0")]
[NamingConvention(Strategy = NamingStrategy.SnakeCase)]
public partial class Order
{
    [EntityId]
    [PrimaryKey(ValueGenerated = ValueGeneration.OnAdd)]
    public Guid Id { get; set; }

    [Property("OrderNumber", Required = true, MaxLength = 50)]
    [Index(IsUnique = true)]
    public string OrderNumber { get; set; } = "";

    [Property("Status", Required = true)]
    [EnumStorage(AsString = true)]
    public OrderStatus Status { get; set; }

    [Property("Total", Required = true)]
    [Precision(18, 2)]
    public decimal Total { get; set; }

    [ConcurrencyToken(IsRowVersion = true)]
    public byte[] RowVersion { get; set; } = null!;

    [Composition]
    [HasMany(WithOne = "Order", ForeignKey = "OrderId")]
    public List<OrderItem> Items { get; set; } = new();

    [Aggregation]
    [HasOne(WithMany = "Orders", ForeignKey = "CustomerId")]
    public Customer Customer { get; set; } = null!;

    [OwnedEntity]
    public Address ShippingAddress { get; set; } = null!;

    [ManyToMany(JoinEntity = typeof(OrderTag))]
    public List<Tag> Tags { get; set; } = new();

    [Invariant("Total must be non-negative")]
    public Result TotalMustBeNonNegative()
        => Total >= 0 ? Result.Success() : Result.Failure("Total < 0");
}
```

#### Output → `OrderConfiguration.g.cs`
```csharp
// <auto-generated/>
#nullable enable

namespace MyApp.Domain;

[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class OrderConfiguration
    : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<global::MyApp.Domain.Order>
{
    public void Configure(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<global::MyApp.Domain.Order> builder)
    {
        // ── Table ──
        builder.ToTable("Orders", "sales");

        // ── Primary key ──
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        // ── Properties ──
        builder.Property(e => e.OrderNumber)
            .HasColumnName("order_number")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.Total)
            .HasColumnName("total")
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        // ── Shadow properties ──
        builder.Property<global::System.DateTimeOffset>("CreatedAt")
            .HasColumnName("created_at")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property<global::System.DateTimeOffset>("UpdatedAt")
            .HasColumnName("updated_at");

        // ── Indexes ──
        builder.HasIndex(e => e.OrderNumber)
            .IsUnique();

        // ── Relationships ──
        builder.HasMany(e => e.Items)
            .WithOne(e => e.Order)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);

        builder.HasOne(e => e.Customer)
            .WithMany(e => e.Orders)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);

        // ── Owned types ──
        builder.OwnsOne(e => e.ShippingAddress);

        // ── Many-to-many ──
        builder.HasMany(e => e.Tags)
            .WithMany()
            .UsingEntity<global::MyApp.Domain.OrderTag>();

        // ── Check constraints ──
        builder.HasCheckConstraint("CK_Orders_Total", "\"Total\" >= 0");
    }
}
```

### Self-Referencing Tree Example

```csharp
[Entity("Category")]
[Table("Categories")]
public class Category
{
    [PrimaryKey] public int Id { get; set; }
    [Property("Name", Required = true)] public string Name { get; set; } = "";
    public int? ParentId { get; set; }

    [SelfReference(InverseNavigation = "Children", ForeignKey = "ParentId", OnDelete = "Restrict")]
    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = new();
}
```

Generated:
```csharp
builder.HasOne(e => e.Parent)
    .WithMany(e => e.Children)
    .HasForeignKey(e => e.ParentId)
    .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
```

### Inheritance Example (TPH)

```csharp
[Entity("Payment")]
[Table("Payments")]
[Inheritance(Strategy = InheritanceStrategy.TPH, DiscriminatorColumn = "PaymentType")]
public class Payment
{
    [PrimaryKey] public Guid Id { get; set; }
    [Property("Amount", Required = true)] public decimal Amount { get; set; }
}

[Entity("CreditCardPayment")]
[Inheritance(Strategy = InheritanceStrategy.TPH, DiscriminatorValue = "CreditCard")]
public class CreditCardPayment : Payment
{
    public string CardLastFour { get; set; } = "";
}

[Entity("BankTransferPayment")]
[Inheritance(Strategy = InheritanceStrategy.TPH, DiscriminatorValue = "BankTransfer")]
public class BankTransferPayment : Payment
{
    public string IBAN { get; set; } = "";
}
```

Generated on base type config:
```csharp
builder.HasDiscriminator<string>("PaymentType")
    .HasValue<global::MyApp.Domain.Payment>("Payment")
    .HasValue<global::MyApp.Domain.CreditCardPayment>("CreditCard")
    .HasValue<global::MyApp.Domain.BankTransferPayment>("BankTransfer");
```

### View + Keyless Example

```csharp
[Entity("ActiveOrderSummary")]
[View("vw_ActiveOrders", Schema = "reports")]
public class ActiveOrderSummary
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
}
```

Generated:
```csharp
builder.ToView("vw_ActiveOrders", "reports");
builder.HasNoKey();
```

### Query Filter (Soft Delete) Example

```csharp
[AggregateRoot("Product")]
[Table("Products")]
[QueryFilter(nameof(NotDeleted))]
public class Product
{
    [PrimaryKey] public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsDeleted { get; set; }

    public static Expression<Func<Product, bool>> NotDeleted() => p => !p.IsDeleted;
}
```

Generated:
```csharp
builder.HasQueryFilter(global::MyApp.Domain.Product.NotDeleted());
```

### Complex Type (Value Object) Example

```csharp
[ValueObject("Money")]
public class Money
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
}

[Entity("Invoice")]
[Table("Invoices")]
public class Invoice
{
    [PrimaryKey] public int Id { get; set; }

    [ComplexType]
    public Money Total { get; set; } = new();

    [OwnedEntity(JsonColumn = "billing_address")]
    public Address BillingAddress { get; set; } = null!;
}
```

Generated:
```csharp
builder.ComplexProperty(e => e.Total);
builder.OwnsOne(e => e.BillingAddress, b => b.ToJson("billing_address"));
```

---

## 11. Testing Strategy

### Unit Tests (`Entity.Dsl.Tests`) — following Injectable emitter test pattern

Hand-build emit models, call emitter, assert output contains expected strings.

```csharp
public class EntityConfigurationEmitterTests
{
    [Fact]
    public void Emits_table_mapping()
    {
        var model = new EntityEmitModel { ClassName = "Order", ClassFullName = "global::App.Order",
            Namespace = "App", TableName = "Orders", Schema = "sales" };
        var code = EntityConfigurationEmitter.Emit(model);
        Assert.Contains("builder.ToTable(\"Orders\", \"sales\")", code);
    }

    [Fact]
    public void Emits_primary_key_single()
    {
        var model = new EntityEmitModel { /* ... */ };
        model.PrimaryKeyProperties.Add(new KeyPropertyModel { PropertyName = "Id", ValueGenerated = "OnAdd" });
        var code = EntityConfigurationEmitter.Emit(model);
        Assert.Contains("builder.HasKey(e => e.Id)", code);
        Assert.Contains("ValueGeneratedOnAdd()", code);
    }

    [Fact]
    public void Emits_composite_primary_key_ordered()
    {
        var model = new EntityEmitModel { /* ... */ };
        model.PrimaryKeyProperties.Add(new KeyPropertyModel { PropertyName = "CourseId", Order = 1 });
        model.PrimaryKeyProperties.Add(new KeyPropertyModel { PropertyName = "StudentId", Order = 0 });
        var code = EntityConfigurationEmitter.Emit(model);
        Assert.Contains("builder.HasKey(e => new { e.StudentId, e.CourseId })", code);
    }

    [Fact]
    public void Composition_defaults_to_cascade()
    {
        var model = new EntityEmitModel { /* ... */ };
        model.Relationships.Add(new RelationshipModel {
            NavigationProperty = "Items", Kind = "HasMany.WithOne",
            DeleteBehavior = "Cascade" /* inferred from [Composition] */ });
        var code = EntityConfigurationEmitter.Emit(model);
        Assert.Contains("OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade)", code);
    }

    [Fact]
    public void Aggregation_defaults_to_restrict() { /* similar */ }

    [Fact]
    public void Emits_view_with_no_key()
    {
        var model = new EntityEmitModel { ViewName = "vw_Active", IsKeyless = true, /* ... */ };
        var code = EntityConfigurationEmitter.Emit(model);
        Assert.Contains("builder.ToView(\"vw_Active\")", code);
        Assert.Contains("builder.HasNoKey()", code);
    }

    [Fact]
    public void Emits_snake_case_column_names()
    {
        var model = new EntityEmitModel { NamingConvention = NamingStrategy.SnakeCase, /* ... */ };
        model.Properties.Add(new PropertyConfigModel { PropertyName = "OrderNumber" });
        var code = EntityConfigurationEmitter.Emit(model);
        Assert.Contains("HasColumnName(\"order_number\")", code);
    }

    [Fact]
    public void Emits_enum_as_string_conversion() { /* ... */ }
    [Fact]
    public void Emits_computed_column() { /* ... */ }
    [Fact]
    public void Emits_check_constraint() { /* ... */ }
    [Fact]
    public void Emits_query_filter() { /* ... */ }
    [Fact]
    public void Emits_seed_data() { /* ... */ }
    [Fact]
    public void Emits_temporal_table() { /* ... */ }
    [Fact]
    public void Emits_self_reference() { /* ... */ }
    [Fact]
    public void Emits_complex_type() { /* ... */ }
    [Fact]
    public void Emits_owned_entity_json() { /* ... */ }
    [Fact]
    public void Emits_many_to_many_with_join_entity() { /* ... */ }
    [Fact]
    public void Emits_sequence_on_key() { /* ... */ }
    [Fact]
    public void Contains_auto_generated_header() { /* ... */ }
    [Fact]
    public void Emits_auto_include_navigation() { /* ... */ }
    [Fact]
    public void Emits_value_generator() { /* ... */ }
    [Fact]
    public void Emits_value_comparer() { /* ... */ }
    [Fact]
    public void Emits_collation() { /* ... */ }
    [Fact]
    public void Emits_hilo_key() { /* ... */ }
    [Fact]
    public void Emits_stored_procedure_mapping() { /* ... */ }
    [Fact]
    public void Emits_trigger() { /* ... */ }
    [Fact]
    public void Emits_entity_split() { /* ... */ }
    [Fact]
    public void Emits_not_mapped_skips_property() { /* ... */ }
    [Fact]
    public void Emits_owned_class_level_auto_infers_owns_one() { /* ... */ }
    [Fact]
    public void Emits_backing_field_with_access_mode() { /* ... */ }
    [Fact]
    public void Emits_tph_discriminator() { /* ... */ }
    [Fact]
    public void Emits_tpt_separate_tables() { /* ... */ }
    [Fact]
    public void Emits_tpc_mapping_strategy() { /* ... */ }
}

public class DbContextEmitterTests
{
    [Fact]
    public void EmitBase_contains_dbset_properties() { /* ... */ }
    [Fact]
    public void EmitBase_contains_register_configurations() { /* ... */ }
    [Fact]
    public void EmitBase_contains_sequence_definitions() { /* ... */ }
    [Fact]
    public void EmitBase_contains_pre_and_post_model_creating_hooks() { /* ... */ }
    [Fact]
    public void EmitBase_contains_on_entities_adding_hook() { /* ... */ }
    [Fact]
    public void EmitBase_contains_on_entities_modifying_hook() { /* ... */ }
    [Fact]
    public void EmitBase_contains_on_entities_deleting_hook() { /* ... */ }
    [Fact]
    public void EmitBase_contains_update_timestamps() { /* ... */ }
    [Fact]
    public void EmitBase_save_changes_calls_on_before_save() { /* ... */ }
    [Fact]
    public void EmitPartialStub_extends_base() { /* ... */ }
    [Fact]
    public void Excludes_keyless_entities_from_dbsets() { /* ... */ }
    [Fact]
    public void Filters_by_bounded_context() { /* ... */ }
}

public class BehaviorEmitterTests
{
    [Fact]
    public void Timestampable_generates_created_at_and_updated_at_properties() { /* ... */ }
    [Fact]
    public void Timestampable_custom_names_respected() { /* ... */ }
    [Fact]
    public void SoftDeletable_generates_is_deleted_and_deleted_at() { /* ... */ }
    [Fact]
    public void SoftDeletable_emits_query_filter() { /* ... */ }
    [Fact]
    public void Blameable_generates_created_by_and_updated_by() { /* ... */ }
    [Fact]
    public void Versionable_generates_row_version() { /* ... */ }
    [Fact]
    public void Sluggable_generates_slug_with_index() { /* ... */ }
    [Fact]
    public void Sortable_generates_position_property() { /* ... */ }
    [Fact]
    public void TreeNode_generates_parent_id_path_depth() { /* ... */ }
    [Fact]
    public void Loggable_generates_companion_audit_log_entity() { /* ... */ }
    [Fact]
    public void Translatable_generates_companion_translation_entity() { /* ... */ }
    [Fact]
    public void Multiple_behaviors_compose_in_single_partial() { /* ... */ }
}

public class GenerationGapEmitterTests
{
    [Fact]
    public void EmitBase_class_is_abstract() { /* ... */ }
    [Fact]
    public void EmitBase_configure_methods_are_virtual() { /* ... */ }
    [Fact]
    public void EmitBase_has_pre_and_post_configure_hooks() { /* ... */ }
    [Fact]
    public void EmitBase_orchestrator_calls_all_virtuals_in_order() { /* ... */ }
    [Fact]
    public void EmitBase_has_one_configure_method_per_property() { /* ... */ }
    [Fact]
    public void EmitBase_has_one_configure_method_per_relationship() { /* ... */ }
    [Fact]
    public void EmitPartialStub_is_partial_class() { /* ... */ }
    [Fact]
    public void EmitPartialStub_extends_base() { /* ... */ }
    [Fact]
    public void EmitPartialStub_is_empty() { /* ... */ }
    [Fact]
    public void EmitRegistration_implements_ientitytypeconfiguration() { /* ... */ }
    [Fact]
    public void EmitRegistration_delegates_to_configuration_class() { /* ... */ }
}

public class NamingHelperTests
{
    [Theory]
    [InlineData("OrderNumber", "order_number")]
    [InlineData("ID", "id")]
    [InlineData("HTMLParser", "html_parser")]
    public void ToSnakeCase(string input, string expected) { /* ... */ }

    [Theory]
    [InlineData("Order", "Orders")]
    [InlineData("Category", "Categories")]
    [InlineData("Address", "Addresses")]
    public void Pluralize(string input, string expected) { /* ... */ }
}
```

### Concept/Constraint Tests

```csharp
public class AttributeConstraintTests
{
    [Fact]
    public void Table_requires_entity_attribute() { /* call RequiresEntityConstraint with/without Entity */ }
    [Fact]
    public void DefaultValue_value_and_sql_mutually_exclusive() { /* ... */ }
    [Fact]
    public void OwnedEntity_inherits_composition_concept() { /* ... */ }
    [Fact]
    public void ComplexType_inherits_value_object_concept() { /* ... */ }
}
```

### Diagnostic Tests (negative tests)

```csharp
public class DiagnosticTests
{
    // Use CSharpGeneratorDriver to run SG against invalid input, verify diagnostics
    [Fact]
    public void EDSL0001_PrimaryKey_on_navigation() { /* ... */ }
    [Fact]
    public void EDSL0002_HasMany_on_non_collection() { /* ... */ }
    [Fact]
    public void EDSL0005_DbContext_not_partial() { /* ... */ }
    [Fact]
    public void EDSL0006_View_and_Table_on_same_class() { /* ... */ }
    [Fact]
    public void EDSL0013_OwnedEntity_with_Aggregation() { /* ... */ }
}
```

### Integration Tests (`Entity.Dsl.Integration.Tests`)

```csharp
public class EfCoreIntegrationTests
{
    [Fact]
    public async Task Creates_database_from_generated_config() { /* SQLite in-memory */ }
    [Fact]
    public async Task Cascade_delete_removes_children() { /* [Composition] → Cascade */ }
    [Fact]
    public async Task Restrict_delete_throws_on_parent_removal() { /* [Aggregation] → Restrict */ }
    [Fact]
    public async Task Owned_entity_persisted_in_same_table() { /* OwnsOne without TableName */ }
    [Fact]
    public async Task Owned_entity_json_column() { /* OwnsOne + ToJson */ }
    [Fact]
    public async Task Many_to_many_with_join_entity_payload() { /* association class */ }
    [Fact]
    public async Task Self_referencing_tree_crud() { /* Category parent/children */ }
    [Fact]
    public async Task Tph_discriminator_queries_correctly() { /* Payment hierarchy */ }
    [Fact]
    public async Task Query_filter_excludes_soft_deleted() { /* [QueryFilter] */ }
    [Fact]
    public async Task Complex_type_stored_as_columns() { /* [ComplexType] / Money */ }
    [Fact]
    public async Task Check_constraint_rejects_invalid_data() { /* CK_Orders_Total */ }
    [Fact]
    public async Task Concurrency_token_detects_conflicts() { /* [ConcurrencyToken] */ }
    [Fact]
    public async Task Seed_data_populated_on_ensure_created() { /* [SeedData] */ }
    [Fact]
    public async Task Bounded_context_scopes_dbsets() { /* multi-DbContext */ }
    [Fact]
    public async Task Shadow_property_timestamps_populated() { /* SaveChanges interceptor */ }
    [Fact]
    public async Task Developer_can_override_configure_property() { /* partial class override */ }
    [Fact]
    public async Task Developer_can_override_configure_relationship() { /* change cascade */ }
    [Fact]
    public async Task Developer_can_add_post_configure_hook() { /* filtered index in PostConfigure */ }
    [Fact]
    public async Task Developer_can_override_on_entities_adding() { /* custom audit in lifecycle hook */ }
    [Fact]
    public async Task Developer_can_override_update_timestamps() { /* custom timestamp logic */ }
    [Fact]
    public async Task Developer_can_skip_configure_by_empty_override() { /* let EF conventions handle it */ }
    [Fact]
    public async Task Base_regeneration_preserves_developer_partial() { /* verify partial class merging */ }
}
```

---

## 12. Phased Implementation

### Phase 1: Foundation — Abstractions + Minimal E2E with Generation Gap
1. Create `[Association]` in `Ddd.Attributes` (prerequisite)
2. Create all **6** csproj projects (including `Abstractions`) + slnx + CPM entries
3. **Abstractions**: Implement `IRepository<T>`, `IAssociationRepository<,,>`, `IUnitOfWork`, `IUnitOfWorkTransaction`, `IEntityListener<T>`
4. Implement `[Table]`, `[Column]`, `[PrimaryKey]`, `[DbContext]` attributes + MetaConcept companions
5. Implement `EntityEmitModel` (public, minimal subset) + `DbContextEmitModel` + `RepositoryEmitModel` + `UnitOfWorkEmitModel`
6. Implement Generation Gap emitters:
   - `EntityConfigurationEmitter.EmitBase/EmitPartialStub/EmitRegistration`
   - `DbContextEmitter.EmitBase/EmitPartialStub`
7. Implement Repository emitters:
   - `RepositoryEmitter.EmitInterface/EmitBase/EmitPartialStub`
8. Implement UnitOfWork emitters:
   - `UnitOfWorkEmitter.EmitInterface/EmitBase/EmitPartialStub`
9. Implement DI registration emitter:
   - `DbContextRegistrationEmitter.Emit` — `AddDbContext` extension (only EF-specific)
   - Generated repos/UoW carry `[Injectable]` — Injectable SG handles DI registration
10. Implement `EntityDslGenerator` (discovery + model extraction + full file emission)
11. Implement `NamingHelper.Pluralize()`
12. Unit tests for all emitter methods (following Injectable pattern)
13. Integration test proving full E2E: POCO → config + repo + UoW → EF Core creates DB
14. Integration test verifying developer can override a virtual method in partial class

### Phase 2: Full Property Configuration
1. Add `[Index]`, `[AlternateKey]`, `[Precision]`, `[DefaultValue]`, `[Conversion]`, `[EnumStorage]`, `[ConcurrencyToken]`, `[ComputedColumn]`, `[Sequence]`, `[ShadowProperty]`
2. Each property gets its own `virtual Configure{PropertyName}(builder)` in the Base class
3. Extend model extraction in generator
4. Unit + integration tests for each property path

### Phase 3: Relationships + Lifecycle + Association Classes
1. Add `[HasOne]`, `[HasMany]`, `[ManyToMany]`, `[OwnedEntity]`, `[ComplexType]`, `[SelfReference]`
2. Add `[AssociationClass(name, A, B)]` — generates composite PK, FK navigations, skip navigations on endpoints
3. Each relationship gets its own `virtual Configure{NavigationName}(builder)` in the Base class
4. Implement convention-based inference (auto-detect navigations from property type)
5. Implement DDD-attribute lifecycle → DeleteBehavior convention
6. Implement bidirectional relationship principal resolution
7. Generate `IAssociationRepository<,,>` implementations for association classes
8. Test cascade/restrict/owned/self-reference/M:M with actual EF Core
9. Test association classes with payload + specialized repository queries
10. Test developer override of relationship via partial class

### Phase 4: Inheritance + Views + Keyless
1. Add `[Inheritance]` with TPH/TPT/TPC + `DerivedTypeModel`
2. Add `[View]`, `[Keyless]`
3. Extend Base emitter for discriminator / ToTable / UseTpcMappingStrategy / ToView / HasNoKey
4. Test inheritance hierarchies, views, keyless entities with EF Core

### Phase 5: Validation + Diagnostics
1. Add `[Validate]`, `[ValidateProperty]` with static method invocation at SG time
2. Implement full diagnostic catalog (EDSL0001–EDSL0014)
3. Add `[NamingConvention]` + `NamingHelper.ToSnakeCase()`/`ToCamelCase()`
4. Negative tests for all diagnostics (CSharpGeneratorDriver + verify diagnostics)

### Phase 6: Lifecycle Hooks + Core Behaviors
1. Add `[EntityListener(typeof(...))]` attribute
2. Implement `IEntityListener<T>` dispatcher in DbContextBase (resolves from DI)
3. Implement `IGlobalEntityListener` dispatcher (global event subscriber pattern)
4. Implement `OnLoadedAsync` via `IMaterializationInterceptor` registration in DbContextBase
5. **Core behaviors**: Implement `ITimestampable`, `ISoftDeletable`, `IBlameable`, `IVersioned`, `ISluggable` in Abstractions
6. SG detects behavior interfaces → auto-configures properties + auto-populates in SaveChanges
7. SG generates `IModelFinalizingConvention` implementations (naming, soft delete filter)
8. Add `[QueryFilter]`, `[CheckConstraint]`, `[SeedData]`
9. Test entity listeners (per-entity + global) with DI
10. Test behaviors (timestamps auto-populated, soft delete filtered, blameable populated)
11. Test `OnLoadedAsync` fires after entity materialization

### Phase 7: Advanced EF Core Features
1. Add `[TemporalTable]`, `[TableSplit]`, `[EntitySplit]`
2. Add `[StoredProcedure]` (InsertUsing/UpdateUsing/DeleteUsing)
3. Add `[Trigger]` (HasTrigger for EF Core 7+ SaveChanges awareness)
4. Add `[AutoInclude]` (Navigation.AutoInclude)
5. Add `[ValueGenerator]`, `[ValueComparer]`, `[Collation]`, `[HiLo]`
6. DbContext-level config: `LazyLoading`, `QueryTracking`, `QuerySplitting`, `ChangeTracking`, `EnableRetryOnFailure`
7. Emit `OnConfiguring` override in DbContextBase with context-level settings
8. Test all advanced features

### Phase 8: Advanced Behaviors (Doctrine Extensions)
1. **`ILoggable`**: SG generates companion `{Entity}AuditLog` entity + config + auto-population in SaveChanges (captures old/new values)
2. **`ITranslatable`**: SG generates companion `{Entity}Translation` entity + config from `[Translatable]` properties
3. **`ISortable`**: SG auto-manages `Position` field on Add/Remove within sort groups
4. **`ITreeNode`**: SG auto-maintains `MaterializedPath` and `Depth` on Add/Move; generates query helpers
5. Test audit trail generation (ILoggable)
6. Test translation CRUD (ITranslatable)
7. Test auto-ordering (ISortable)
8. Test tree operations (ITreeNode — ancestors, descendants, move subtree)

### Phase 9: Multi-Tenancy
**Opt-in**: `[Tenantable]` on entity + `[DbContext(MultiTenancy = "RowLevel")]` on context.

#### `[Tenantable]` behavior attribute
```csharp
[MetaConcept(typeof(TenantableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class TenantableAttribute : Attribute
{
    [MetaProperty("TenantIdName", "string")]
    public string TenantIdName { get; set; } = "TenantId";

    [MetaProperty("TenantIdType", "string")]
    public string TenantIdType { get; set; } = "string";     // "string", "Guid", "int", "long"

    [MetaProperty("MaxLength", "int")]
    public int MaxLength { get; set; } = 64;                  // for string tenant IDs

    [MetaProperty("IndexName", "string")]
    public string? IndexName { get; set; }                    // null = "IX_{Entity}_TenantId"

    [MetaProperty("Required", "bool")]
    public bool Required { get; set; } = true;
}
```

#### `[DbContext]` additions for multi-tenancy strategy
```csharp
// Add to DbContextAttribute:
[MetaProperty("MultiTenancy", "string")]
public string? MultiTenancy { get; set; }            // null (disabled), "RowLevel", "SchemaPerTenant", "DatabasePerTenant"
```

#### `ITenantProvider` (in Abstractions)
```csharp
public interface ITenantProvider
{
    string GetCurrentTenantId();
}
// Developer implements + registers with [Injectable(Scope = Scope.Scoped)]
```

#### What the SG generates per strategy

**RowLevel** (most common):
- `{Entity}.Behaviors.g.cs` → generates `TenantId` property
- `ConfigureBase` → `HasIndex(e => e.TenantId)`, `IsRequired()`
- `ConfigureBase` → `HasQueryFilter(e => e.TenantId == currentTenantId)` (resolved from `ITenantProvider`)
- `DbContextBase` → auto-sets `TenantId` on `EntityState.Added`
- SG diagnostic `EDSL0300`: `[Tenantable]` without `[DbContext(MultiTenancy = "RowLevel")]`

**SchemaPerTenant**:
- `DbContextBase.OnModelCreating` → dynamically sets `builder.HasDefaultSchema(tenantProvider.GetCurrentTenantId())`
- No `TenantId` property generated — isolation is at schema level
- SG diagnostic `EDSL0301`: entity has `[Tenantable]` but `MultiTenancy` is `SchemaPerTenant` — `TenantId` column not needed

**DatabasePerTenant**:
- `DbContextBase.OnConfiguring` → swaps connection string based on `ITenantProvider`
- Generates `ITenantConnectionStringResolver` interface
- No `TenantId` property generated — isolation is at database level

**Implementation steps:**
1. Add `[Tenantable]` attribute + `TenantableConcept`
2. Add `MultiTenancy` to `[DbContext]`
3. Add `ITenantProvider` to Abstractions
4. Add `TenantableBehaviorModel` to emit models
5. Extend `DbContextEmitter` for tenant query filter + auto-set
6. Extend `EntityConfigurationEmitter` for tenant index
7. Test row-level: insert with auto-TenantId, query filter excludes other tenants

### Phase 10: I18N Enhancements
**Opt-in**: `[Translatable]` on entity + `[TranslatableProperty]` on fields (already planned).

Enhancements beyond current plan:

1. **Locale fallback chain**: `[Translatable(FallbackChain = "parent-locale")]`
   - `fr-CA` → `fr` → default (`en`)
   - `parent-locale` = strip region. `exact` = no fallback. `custom` = developer provides `ILocaleFallbackStrategy`

2. **Query extension methods** (generated per translatable entity):
   ```csharp
   // Generated: ProductTranslationExtensions.g.cs
   public static class ProductTranslationExtensions
   {
       public static IQueryable<ProductWithTranslation> WithTranslation(
           this IQueryable<Product> query, string locale) { ... }
   }
   ```

3. **`ITranslationRepository<TEntity, TTranslation>`** in Abstractions:
   ```csharp
   public interface ITranslationRepository<TEntity, TTranslation>
       where TEntity : class where TTranslation : class
   {
       Task<TTranslation?> GetTranslationAsync(object entityKey, string locale, CancellationToken ct = default);
       Task SetTranslationAsync(object entityKey, string locale, TTranslation translation, CancellationToken ct = default);
       Task<IReadOnlyList<TTranslation>> GetAllTranslationsAsync(object entityKey, CancellationToken ct = default);
       Task<IReadOnlyList<string>> GetAvailableLocalesAsync(object entityKey, CancellationToken ct = default);
   }
   ```

4. **Ambient culture integration**: `DbContextBase` resolves `CultureInfo.CurrentUICulture` as default locale for queries

5. Test: CRUD translations, fallback chain, query extension

### Phase 11: Bulk Operations
**Opt-in**: Available on every `IRepository<T>` / `RepositoryBase<T>` — no attribute needed.

Add to `IRepository<T>` and implement in `RepositoryBase<T>`:
```csharp
// In IRepository<T>:
Task BulkInsertAsync(IEnumerable<T> entities, CancellationToken ct = default);
Task BulkUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default);
Task BulkDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default);
Task<int> BulkDeleteWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
Task<int> BulkUpdateWhereAsync(Expression<Func<T, bool>> predicate,
    Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setPropertyCalls,
    CancellationToken ct = default);
```

Uses EF Core 7+ `ExecuteDeleteAsync` / `ExecuteUpdateAsync` for where-based operations.
For entity-based bulk, uses `AddRange` + `SaveChanges` (or EFCore.BulkExtensions if CPM'd).

**Implementation steps:**
1. Add methods to `IRepository<T>`
2. Implement in `RepositoryBase<T>` using EF Core 7+ bulk APIs
3. Test bulk insert, update, delete

### Phase 12: Polymorphic Associations
**Opt-in**: `[PolymorphicOwner]` on entity.

```csharp
[MetaConcept(typeof(PolymorphicOwnerConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class PolymorphicOwnerAttribute : Attribute
{
    [MetaProperty("OwnerTypes", "Type[]", Required = true)]
    public Type[] OwnerTypes { get; }                        // typeof() array — type-safe, refactor-friendly

    [MetaProperty("DiscriminatorName", "string")]
    public string DiscriminatorName { get; set; } = "OwnerType";

    [MetaProperty("OwnerIdName", "string")]
    public string OwnerIdName { get; set; } = "OwnerId";

    [MetaProperty("OwnerIdType", "string")]
    public string OwnerIdType { get; set; } = "int";         // must match PK type of all owner entities

    public PolymorphicOwnerAttribute(params Type[] ownerTypes) { OwnerTypes = ownerTypes; }
}
```

**Usage:**
```csharp
[Entity("Comment")]
[Table("Comments")]
[PolymorphicOwner(typeof(Post), typeof(Photo), typeof(Video))]
public partial class Comment
{
    [PrimaryKey] public int Id { get; set; }
    public string Text { get; set; } = "";
    // SG generates: OwnerId (int), OwnerType (string), navigation helpers
}
```

**What the SG generates:**
- `OwnerId` + `OwnerType` properties in `Comment.Behaviors.g.cs`
- No FK constraint (EF Core can't FK to polymorphic target) — SG diagnostic info
- Typed query methods: `FindByOwnerAsync<TOwner>(int ownerId)` on the repository
- `HasIndex(e => new { e.OwnerType, e.OwnerId })` in config

**Implementation steps:**
1. Add `[PolymorphicOwner]` attribute + concept
2. Add `PolymorphicOwnerBehaviorModel`
3. Generate properties + index + repository query helpers
4. SG diagnostic: warn about no FK constraint (by design)
5. Test polymorphic queries

### Phase 13: Read Replicas / CQRS Split
**Opt-in**: `[DbContext(ReadReplica = "...")]`

```csharp
// Add to DbContextAttribute:
[MetaProperty("ReadReplica", "string")]
public string? ReadReplica { get; set; }             // connection string name for read replica
```

**What the SG generates:**
- `IReadOnlyRepository<T>` routes to the read replica DbContext
- `IRepository<T>` routes to the primary DbContext
- Generated `{Context}ReadOnlyDbContext` — separate context pointing to replica
- `RepositoryBase<T>` constructor accepts both contexts:
  ```csharp
  protected RepositoryBase(DbContext writeContext, DbContext? readContext = null)
  {
      _writeContext = writeContext;
      _readContext = readContext ?? writeContext;
      _readDbSet = _readContext.Set<T>();
      _writeDbSet = _writeContext.Set<T>();
  }
  ```
- All query methods use `_readDbSet`, all command methods use `_writeDbSet`

**Implementation steps:**
1. Add `ReadReplica` to `[DbContext]`
2. Generate read-only DbContext variant
3. Modify `RepositoryBase<T>` for dual-context support
4. Test: writes go to primary, reads go to replica

### Phase 14: Advanced Specifications
**Opt-in**: Available on `ISpecification<T>` — no attribute needed.

Add composable specification infrastructure to Abstractions:

```csharp
// In Abstractions:
public abstract class Specification<T> : ISpecification<T> where T : class
{
    public abstract Expression<Func<T, bool>> Criteria { get; }
    public List<Expression<Func<T, object>>> Includes { get; } = new();
    public Expression<Func<T, object>>? OrderBy { get; protected set; }
    public Expression<Func<T, object>>? OrderByDescending { get; protected set; }
    public int? Take { get; protected set; }
    public int? Skip { get; protected set; }

    public Specification<T> And(Specification<T> other) => new AndSpecification<T>(this, other);
    public Specification<T> Or(Specification<T> other) => new OrSpecification<T>(this, other);
    public Specification<T> Not() => new NotSpecification<T>(this);
}

internal sealed class AndSpecification<T> : Specification<T> where T : class { ... }
internal sealed class OrSpecification<T> : Specification<T> where T : class { ... }
internal sealed class NotSpecification<T> : Specification<T> where T : class { ... }
```

**Usage:**
```csharp
public class ActiveOrderSpec : Specification<Order>
{
    public override Expression<Func<Order, bool>> Criteria
        => o => o.Status != OrderStatus.Cancelled;
}

public class HighValueOrderSpec : Specification<Order>
{
    public override Expression<Func<Order, bool>> Criteria
        => o => o.Total > 1000;
}

// Composable:
var spec = new ActiveOrderSpec().And(new HighValueOrderSpec());
var orders = await repo.FindBySpecAsync(spec);
```

**Implementation steps:**
1. Add `Specification<T>` abstract base to Abstractions
2. Add `AndSpecification<T>`, `OrSpecification<T>`, `NotSpecification<T>`
3. Add spec-based `CountAsync`/`ExistsAsync` overloads to `IReadOnlyRepository<T>`
4. Test composition + query execution

### Phase 15: Temporal Query Extensions
**Opt-in**: `[TemporalTable]` on entity (already planned).

Generate per-entity query extension methods:

```csharp
// Generated: OrderTemporalExtensions.g.cs
public static class OrderTemporalExtensions
{
    public static IQueryable<Order> AsOf(this IQueryable<Order> query, DateTime pointInTime)
        => query.TemporalAsOf(pointInTime);

    public static IQueryable<Order> Between(this IQueryable<Order> query,
        DateTime from, DateTime to)
        => query.TemporalBetween(from, to);

    public static IQueryable<Order> ContainedIn(this IQueryable<Order> query,
        DateTime from, DateTime to)
        => query.TemporalContainedIn(from, to);

    public static IQueryable<Order> All(this IQueryable<Order> query)
        => query.TemporalAll();
}
```

**Implementation steps:**
1. Generate per-entity temporal extension class (only for entities with `[TemporalTable]`)
2. Test temporal queries with SQL Server (or skip in SQLite)

### Phase 16: Domain Events + Outbox Pattern
**Opt-in**: `[OutboxEnabled]` on entity.

```csharp
[MetaConcept(typeof(OutboxEnabledConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class OutboxEnabledAttribute : Attribute
{
    [MetaProperty("OutboxTableName", "string")]
    public string? OutboxTableName { get; set; }             // null = "OutboxMessages"

    [MetaProperty("OutboxSchema", "string")]
    public string? OutboxSchema { get; set; }

    [MetaProperty("RetentionDays", "int")]
    public int RetentionDays { get; set; } = 30;             // auto-purge after N days

    [MetaProperty("PayloadFormat", "string")]
    public string PayloadFormat { get; set; } = "Json";      // "Json" or "Binary"
}
```

**Interfaces in Abstractions:**
```csharp
public interface IDomainEventEmitter
{
    void Emit(object domainEvent);
    IReadOnlyList<object> GetPendingEvents();
    void ClearPendingEvents();
}

public interface IOutboxProcessor
{
    Task ProcessPendingAsync(CancellationToken ct = default);
    Task PurgeProcessedAsync(int retentionDays, CancellationToken ct = default);
}
```

**What the SG generates:**
- `OutboxMessage` entity: `Id` (Guid), `Type` (string), `Payload` (string/JSON), `CreatedAt`, `ProcessedAt?`, `Error?`
- `OutboxMessageConfiguration.g.cs`
- `DbContextBase.SaveChanges` hook: collects domain events from `IDomainEventEmitter`, serializes to `OutboxMessage`, inserts in same transaction
- `IOutboxProcessor` default implementation: polls unprocessed messages, publishes (developer provides `IOutboxMessagePublisher`), marks processed
- Connects to DDD `[DomainEvent]` attributes — events raised via `AddDomainEvent()` are captured

**Usage:**
```csharp
[AggregateRoot("Order")]
[Table("Orders")]
[OutboxEnabled]
public partial class Order
{
    [PrimaryKey] public Guid Id { get; set; }
    public decimal Total { get; set; }

    public void Place()
    {
        // DomainEventEmitter injected via SG-generated partial
        EmitEvent(new OrderPlacedEvent(Id, Total));
    }
}

[DomainEvent("OrderPlaced")]
public record OrderPlacedEvent(Guid OrderId, decimal Total);
```

**Implementation steps:**
1. Add `[OutboxEnabled]` attribute + concept
2. Add `IDomainEventEmitter`, `IOutboxProcessor`, `IOutboxMessagePublisher` to Abstractions
3. Generate `OutboxMessage` entity + config
4. Extend `DbContextBase` SaveChanges to capture + persist events
5. Generate default `OutboxProcessor` implementation
6. Test: domain event → outbox → process

### Phase 17: Event Sourcing (Separate Library)
**Opt-in**: `[EventSourced]` on aggregate root. This is a **separate project** (`Entity.Dsl.EventSourcing`) due to the fundamentally different persistence model.

```csharp
[MetaConcept(typeof(EventSourcedConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class EventSourcedAttribute : Attribute
{
    [MetaProperty("EventStoreTable", "string")]
    public string? EventStoreTable { get; set; }             // null = "{Entity}Events"

    [MetaProperty("SnapshotInterval", "int")]
    public int SnapshotInterval { get; set; } = 0;           // 0 = no snapshots. N = snapshot every N events

    [MetaProperty("SnapshotTable", "string")]
    public string? SnapshotTable { get; set; }               // null = "{Entity}Snapshots"
}
```

**What the SG generates:**
- `{Entity}Event` base entity: `Id`, `AggregateId`, `Version`, `Type`, `Payload` (JSON), `Timestamp`
- `{Entity}Snapshot` entity (if SnapshotInterval > 0): `AggregateId`, `Version`, `State` (JSON)
- Event store repository with `AppendAsync`, `LoadEventsAsync`, `LoadFromSnapshotAsync`
- `Replay()` method on the aggregate that rebuilds state from events
- Concurrency via aggregate version number
- No UPDATE/DELETE on event store (append-only)

**This phase creates:**
- `Entity.Dsl.EventSourcing/` project structure
- `IEventStore<T>` interface
- `EventSourcedAggregateBase<T>` with `Apply()`, `Replay()`, `GetUncommittedEvents()`

### Phase 18: Testing Helpers
**Must-have** — developers testing services that depend on `IOrderRepository` need fakes without EF Core.

Add to Abstractions:
```csharp
/// <summary>In-memory repository for unit testing. No EF Core dependency.</summary>
public class FakeRepository<T> : IRepository<T> where T : class
{
    private readonly List<T> _store = new();
    private readonly Func<T, object[]> _keySelector;

    public FakeRepository(Func<T, object[]> keySelector) { _keySelector = keySelector; }

    public ValueTask<T?> FindByIdAsync(params object[] keyValues)
        => new(_store.FirstOrDefault(e => _keySelector(e).SequenceEqual(keyValues)));
    public Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.AsQueryable().FirstOrDefault(predicate));
    public Task<IReadOnlyList<T>> FindAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(_store.ToList());
    public Task<IReadOnlyList<T>> FindWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(_store.AsQueryable().Where(predicate).ToList());
    public Task<IReadOnlyList<T>> FindBySpecAsync(ISpecification<T> spec, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(_store.AsQueryable().Where(spec.Criteria).ToList());
    public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.AsQueryable().Any(predicate));
    public Task<int> CountAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Count);
    public Task<PagedResult<T>> FindPagedAsync(int pageNumber, int pageSize, ISpecification<T>? spec = null, CancellationToken ct = default)
    {
        var query = spec != null ? _store.AsQueryable().Where(spec.Criteria) : _store.AsQueryable();
        var total = query.Count();
        var items = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<T>(items, total, pageNumber, pageSize));
    }
    public void Add(T entity) => _store.Add(entity);
    public void AddRange(IEnumerable<T> entities) => _store.AddRange(entities);
    public void Update(T entity) { } // no-op for in-memory
    public void Remove(T entity) => _store.Remove(entity);
    public void RemoveRange(IEnumerable<T> entities) { foreach (var e in entities) _store.Remove(e); }
    public void Attach(T entity) { } // no-op

    // Test helpers
    public IReadOnlyList<T> All => _store;
    public void Seed(params T[] entities) => _store.AddRange(entities);
    public void Clear() => _store.Clear();
}

/// <summary>In-memory UnitOfWork for unit testing.</summary>
public class FakeUnitOfWork : IUnitOfWork
{
    private int _saveCount;
    public int SaveCount => _saveCount;
    public bool SaveWasCalled => _saveCount > 0;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) { _saveCount++; return Task.FromResult(1); }
    public int SaveChanges() { _saveCount++; return 1; }
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());
    public void DetachAll() { }
    public bool HasChanges => false;
    public void Dispose() { }
    public ValueTask DisposeAsync() => default;

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => default;
    }
}
```

**Implementation steps:**
1. Add `FakeRepository<T>` to Abstractions
2. Add `FakeUnitOfWork` to Abstractions
3. Unit tests for both fakes
4. Usage example in HOW-TO.md

### Phase 19: Pagination
**Must-have** — every API needs paginated results.

Add to Abstractions:
```csharp
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize) { ... }
}

// Add to IReadOnlyRepository<T>:
Task<PagedResult<T>> FindPagedAsync(
    Expression<Func<T, bool>>? predicate,
    int pageNumber, int pageSize,
    Expression<Func<T, object>>? orderBy = null,
    bool descending = false,
    CancellationToken ct = default);

Task<PagedResult<T>> FindPagedBySpecAsync(
    ISpecification<T> spec,
    int pageNumber, int pageSize,
    CancellationToken ct = default);
```

**Implementation steps:**
1. Add `PagedResult<T>` to Abstractions
2. Add `FindPagedAsync` / `FindPagedBySpecAsync` to `IReadOnlyRepository<T>`
3. Implement in `RepositoryBase<T>`
4. Test pagination with edge cases (empty, last page, beyond range)

### Phase 20: JSON Columns
**Must-have** — storing `List<string>`, `Dictionary<string,object>`, or any serializable type as a JSON column.

```csharp
[MetaConcept(typeof(JsonColumnConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonColumnAttribute : Attribute
{
    [MetaProperty("ColumnName", "string")]
    public string? ColumnName { get; set; }              // null = property name

    [MetaProperty("ColumnType", "string")]
    public string ColumnType { get; set; } = "nvarchar(max)"; // or "jsonb" for PostgreSQL
}
```

**Usage:**
```csharp
[Entity("Order")]
[Table("Orders")]
public partial class Order
{
    [PrimaryKey] public Guid Id { get; set; }

    [JsonColumn]
    public List<string> Tags { get; set; } = new();

    [JsonColumn(ColumnType = "jsonb")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

**What the SG generates:**
```csharp
builder.Property(e => e.Tags)
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)!)
    .HasColumnType("nvarchar(max)")
    .Metadata.SetValueComparer(new ValueComparer<List<string>>(...));
```

**Implementation steps:**
1. Add `[JsonColumn]` attribute + concept
2. Extend PropertyConfigModel with `IsJsonColumn`, `JsonColumnType`
3. Extend emitter for JSON conversion + value comparer
4. Test with List, Dictionary, custom POCO types

### Phase 21: IReadOnlyUnitOfWork
**Should-have** — CQRS read side needs a UoW with only read-only repositories.

```csharp
// In Abstractions:
public interface IReadOnlyUnitOfWork<TContext> where TContext : DbContext
{
    TContext Context { get; }
    // No SaveChanges, no transactions — read-only
}

// SG generates per DbContext:
public interface IReadOnlySalesUnitOfWork : IReadOnlyUnitOfWork<SalesDbContext>
{
    IReadOnlyRepository<Order> Orders { get; }
    IReadOnlyRepository<Customer> Customers { get; }
}
```

**Opt-in**: `[DbContext(GenerateReadOnlyUnitOfWork = true)]`

**Implementation steps:**
1. Add `IReadOnlyUnitOfWork<T>` to Abstractions
2. Add `GenerateReadOnlyUnitOfWork` to `[DbContext]`
3. Extend `UnitOfWorkEmitter` to also emit read-only variant
4. Test CQRS split

### Phase 22: DbContext Pooling
**Should-have** — performance optimization for high-throughput apps.

```csharp
// Add to DbContextAttribute:
[MetaProperty("UsePooling", "bool")]
public bool UsePooling { get; set; }                    // false = AddDbContext, true = AddDbContextPool

[MetaProperty("PoolSize", "int")]
public int PoolSize { get; set; } = 1024;               // default pool size
```

**What the SG generates in Registration:**
```csharp
// When UsePooling = true:
services.AddDbContextPool<SalesDbContext>(configureDbContext, poolSize: 1024);
// When UsePooling = false (default):
services.AddDbContext<SalesDbContext>(configureDbContext);
```

**Implementation steps:**
1. Add `UsePooling`/`PoolSize` to `[DbContext]`
2. Update `DbContextRegistrationEmitter` to emit `AddDbContextPool` when enabled
3. Test both paths

### Phase 23: ERD / Schema Documentation Generation
**Should-have** — auto-generate Mermaid ERD diagrams and schema markdown from the model.

The SG sees the full entity model — it can emit a Mermaid diagram as an additional file:

```csharp
// Add to DbContextAttribute:
[MetaProperty("GenerateErd", "bool")]
public bool GenerateErd { get; set; }                   // true = emit {Context}.erd.md

[MetaProperty("GenerateSchemaDoc", "bool")]
public bool GenerateSchemaDoc { get; set; }             // true = emit {Context}.schema.md
```

**Generated `SalesDbContext.erd.md`:**
```markdown
erDiagram
    Order ||--|{ OrderItem : items
    Order }o--|| Customer : customer
    Order ||--o{ Tag : tags
    Order {
        Guid Id PK
        string OrderNumber UK
        decimal Total
    }
    OrderItem {
        int Id PK
        Guid OrderId FK
        string ProductName
    }
```

**Generated `SalesDbContext.schema.md`:**
Per-entity table documentation with columns, types, indexes, relationships, constraints.

**Implementation steps:**
1. Add `GenerateErd`/`GenerateSchemaDoc` to `[DbContext]`
2. Create `ErdEmitter.Emit(DbContextEmitModel, List<EntityEmitModel>)` in Lib
3. Create `SchemaDocEmitter.Emit(...)` in Lib
4. Emit as additional output files in the SG

### Phase 24: Breaking Schema Change Detection
**Should-have** — warn when DSL changes would break existing database schema.

**Opt-in**: `[DbContext(SchemaGuard = true)]`

```csharp
// Add to DbContextAttribute:
[MetaProperty("SchemaGuard", "bool")]
public bool SchemaGuard { get; set; }                   // true = compare with previous build
```

The SG stores a `{Context}.schema.json` snapshot in the project. On each build, it compares the current model with the snapshot and emits diagnostics for breaking changes:

| ID | Severity | Breaking Change |
|---|---|---|
| `EDSL0400` | Warning | Column removed — `Order.Status` was dropped |
| `EDSL0401` | Warning | Column type changed — `Order.Total` changed from `decimal(18,2)` to `int` |
| `EDSL0402` | Warning | Required column added without default — `Order.NewField` is required but existing rows have no value |
| `EDSL0403` | Warning | FK dropped — relationship `Order→Customer` was removed |
| `EDSL0404` | Info | Table renamed — `Orders` → `SalesOrders` |
| `EDSL0405` | Warning | Primary key changed — `Order.Id` type changed from `int` to `Guid` |

Developer runs `dotnet entity-dsl schema update` to accept the new schema and update the snapshot.

**Implementation steps:**
1. Add `SchemaGuard` to `[DbContext]`
2. Create `SchemaSnapshot` model (JSON-serializable entity/property/relationship summary)
3. Emit snapshot as additional text file
4. On build: load previous snapshot, diff against current model, emit diagnostics
5. Provide CLI tool or MSBuild target to update snapshot

### Phase 25: Environment-Aware Seeding
**Nice-to-have** — seed data for specific environments.

```csharp
[MetaConcept(typeof(SeedDataConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class SeedDataAttribute : Attribute
{
    [MetaProperty("SeedMethod", "string", Required = true)]
    public string SeedMethod { get; }

    [MetaProperty("Environment", "string")]
    public string? Environment { get; set; }             // null = all environments. "Development", "Production", etc.

    public SeedDataAttribute(string seedMethod) { SeedMethod = seedMethod; }
}
```

**Usage:**
```csharp
[Entity("Currency")]
[Table("Currencies")]
[SeedData(nameof(GetReferenceCurrencies))]                      // always seeded
[SeedData(nameof(GetTestCurrencies), Environment = "Development")] // dev only
public partial class Currency
{
    [PrimaryKey] public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public static Currency[] GetReferenceCurrencies() => new[]
    {
        new Currency { Code = "USD", Name = "US Dollar" },
        new Currency { Code = "EUR", Name = "Euro" }
    };

    public static Currency[] GetTestCurrencies() => new[]
    {
        new Currency { Code = "TEST", Name = "Test Currency" }
    };
}
```

**What the SG generates:**
```csharp
// In ConfigureBase:
protected virtual void ConfigureSeedData(EntityTypeBuilder<Currency> builder)
{
    builder.HasData(Currency.GetReferenceCurrencies());
    // Environment-specific seeding handled in DbContextBase via IHostEnvironment
}
```

**Implementation steps:**
1. Update `[SeedData]` with `Environment` property
2. Generate environment-conditional seeding in DbContextBase (resolves `IHostEnvironment` from DI)
3. Test: dev seed included in Development, excluded in Production

### Phase 26: Concurrency Conflict Resolution
**Nice-to-have** — handle `DbUpdateConcurrencyException` with pluggable strategies.

```csharp
// In Abstractions:
public interface IConflictResolver<T> where T : class
{
    /// <summary>
    /// Called when a concurrency conflict is detected.
    /// Returns the resolved entity, or null to reject the save.
    /// </summary>
    Task<T?> ResolveAsync(T clientEntity, T databaseEntity, CancellationToken ct = default);
}

public enum ConflictStrategy
{
    ClientWins,       // overwrite database with client values
    DatabaseWins,     // discard client changes, reload from database
    Merge,            // per-property merge (non-conflicting changes from both sides)
    Custom            // developer provides IConflictResolver<T>
}
```

**Opt-in**: `[Versionable(ConflictStrategy = "ClientWins")]` or register `IConflictResolver<Order>` in DI.

**What the SG generates:**
- `RepositoryBase<T>.SaveWithConflictResolutionAsync()` method that catches `DbUpdateConcurrencyException` and applies the strategy
- For `Custom`: resolves `IConflictResolver<T>` from DI

**Implementation steps:**
1. Add `IConflictResolver<T>` and `ConflictStrategy` to Abstractions
2. Add `ConflictStrategy` to `[Versionable]`
3. Generate conflict resolution in `RepositoryBase<T>` or `UnitOfWorkBase`
4. Test each strategy

### Phase 27: Value Object Collections
**Nice-to-have** — `ICollection<Money>` stored as JSON.

This is handled by combining `[JsonColumn]` (Phase 20) with `[ValueObject]`:

```csharp
[ValueObject("Money")]
public class Money
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
}

[Entity("Invoice")]
[Table("Invoices")]
public partial class Invoice
{
    [PrimaryKey] public int Id { get; set; }

    [JsonColumn]
    public List<Money> LineItems { get; set; } = new();  // stored as JSON array
}
```

No new attribute needed — `[JsonColumn]` on a property whose element type is `[ValueObject]` triggers JSON serialization with proper value comparer.

**Implementation steps:**
1. Already covered by Phase 20 (`[JsonColumn]`) — ensure value comparer handles collection types
2. SG diagnostic: `[JsonColumn]` on `ICollection<T>` where T is `[ValueObject]` — info: stored as JSON

### Phase 28: Health Checks
**Nice-to-have** — auto-register EF Core health check.

```csharp
// Add to DbContextAttribute:
[MetaProperty("HealthCheck", "bool")]
public bool HealthCheck { get; set; }                   // true = register AddDbContextCheck in DI extension

[MetaProperty("HealthCheckName", "string")]
public string? HealthCheckName { get; set; }            // null = "{ContextName}Database"

[MetaProperty("HealthCheckTags", "string")]
public string? HealthCheckTags { get; set; }            // comma-separated: "ready,db"
```

**What the SG generates in Registration:**
```csharp
services.AddHealthChecks()
    .AddDbContextCheck<SalesDbContext>("SalesDatabase", tags: new[] { "ready", "db" });
```

**Implementation steps:**
1. Add health check properties to `[DbContext]`
2. Update `DbContextRegistrationEmitter` to emit `AddHealthChecks().AddDbContextCheck<>()`
3. Add `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` to CPM
4. Test registration

### Phase 29: Observability / Telemetry
**Nice-to-have** — auto-instrument repository methods with OpenTelemetry.

**Opt-in**: `[Traced]` on entity or `[DbContext(Tracing = true)]` for all entities.

```csharp
[MetaConcept(typeof(TracedConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class TracedAttribute : Attribute
{
    [MetaProperty("ActivitySourceName", "string")]
    public string? ActivitySourceName { get; set; }      // null = "Entity.Dsl.{EntityName}"
}
```

**What the SG generates:**
- Wraps each `RepositoryBase<T>` method in `Activity.Start/Stop` spans
- Tags: `db.operation`, `db.entity`, `db.result_count`
- Error recording on exception

**Implementation steps:**
1. Add `[Traced]` attribute + concept
2. Add `Tracing` property to `[DbContext]`
3. Generate `ActivitySource` per entity in RepositoryBase
4. Wrap methods in `using var activity = ...`
5. Add `System.Diagnostics.DiagnosticSource` to CPM

### Phase 30: Full-Text Search
**Nice-to-have** — provider-specific FTS integration.

```csharp
[MetaConcept(typeof(FullTextIndexConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class FullTextIndexAttribute : Attribute
{
    [MetaProperty("Catalog", "string")]
    public string? Catalog { get; set; }                 // SQL Server FT catalog name

    [MetaProperty("Language", "string")]
    public string? Language { get; set; }                 // "English", "French", etc.
}
```

**What the SG generates:**
- Provider-specific configuration (SQL Server: `HasAnnotation("SqlServer:FullTextIndex", ...)`)
- Query extension: `IQueryable<T>.FullTextContains(e => e.Content, "search terms")`

### Phase 31: Spatial Data
**Nice-to-have** — geometry/geography via NetTopologySuite.

```csharp
[MetaConcept(typeof(SpatialConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class SpatialAttribute : Attribute
{
    [MetaProperty("Srid", "int")]
    public int Srid { get; set; } = 4326;                // WGS84

    [MetaProperty("SpatialIndex", "bool")]
    public bool SpatialIndex { get; set; } = true;
}
```

**Usage:**
```csharp
using NetTopologySuite.Geometries;

[Entity("Store")]
[Table("Stores")]
public partial class Store
{
    [PrimaryKey] public int Id { get; set; }
    public string Name { get; set; } = "";

    [Spatial(Srid = 4326, SpatialIndex = true)]
    public Point Location { get; set; } = null!;
}
```

**What the SG generates:**
- `builder.Property(e => e.Location).HasColumnType("geography")`
- Spatial index configuration
- Query extension: `IQueryable<Store>.WithinDistance(point, distanceMeters)`

### Phase 32: Database-First Scaffolding
**Nice-to-have** — reverse-engineer Entity.Dsl attributes from existing database.

Separate CLI tool (not SG): `dotnet entity-dsl scaffold --connection "..." --output ./Models/`

Produces `.cs` files with:
- `[Entity]` / `[AggregateRoot]` (heuristic: table with no FK = aggregate root)
- `[Table("existing_name")]`
- `[PrimaryKey]` with correct `ValueGeneration`
- `[Column]` with existing column names/types
- `[HasOne]`/`[HasMany]` from FK relationships
- `[Index]` from existing indexes

This is a design-time tool, not a runtime feature. Separate project: `Entity.Dsl.Design`.

### Phase 33: Encryption (GDPR/PII)
**Opt-in**: `[Encrypted]` on property.

```csharp
[MetaConcept(typeof(EncryptedConcept))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute : Attribute
{
    [MetaProperty("ConverterType", "Type")]
    public Type? ConverterType { get; set; }             // null = use IEncryptionProvider from DI
    [MetaProperty("Algorithm", "string")]
    public string Algorithm { get; set; } = "AES256";    // "AES256", "AES128"
}
```

```csharp
// In Abstractions (pure interface):
public interface IEncryptionProvider
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
// Developer implements + registers with [Injectable(Scope.Singleton)]
```

**What the SG generates:**
```csharp
builder.Property(e => e.SSN)
    .HasConversion(
        v => encryptionProvider.Encrypt(v),
        v => encryptionProvider.Decrypt(v))
    .HasColumnType("nvarchar(max)");
```

### Phase 34: Caching
**Opt-in**: `[Cacheable]` on entity.

```csharp
[MetaConcept(typeof(CacheableConcept))]
[AttributeUsage(AttributeTargets.Class)]
public sealed class CacheableAttribute : Attribute
{
    [MetaProperty("DurationSeconds", "int")]
    public int DurationSeconds { get; set; } = 300;      // 5 minutes default
    [MetaProperty("Strategy", "string")]
    public string Strategy { get; set; } = "CacheAside"; // "CacheAside", "ReadThrough"
    [MetaProperty("KeyPrefix", "string")]
    public string? KeyPrefix { get; set; }                // null = entity type name
}
```

```csharp
// In Abstractions (pure interface):
public interface ICacheProvider
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
// Developer implements with Redis, MemoryCache, etc. + [Injectable(Scope.Singleton)]
```

**What the SG generates:**
- `RepositoryBase<T>` override: `FindByIdAsync` checks cache first, falls back to DB, caches result
- Cache invalidation on `Update`/`Remove`

### Phase 35: Database Provider Abstraction
**Opt-in**: `[DatabaseProvider("SqlServer")]` on property or class.

```csharp
[MetaConcept(typeof(DatabaseProviderConcept))]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
public sealed class DatabaseProviderAttribute : Attribute
{
    [MetaProperty("Provider", "string", Required = true)]
    public string Provider { get; }                      // "SqlServer", "PostgreSql", "Sqlite", "MySql"
    public DatabaseProviderAttribute(string provider) { Provider = provider; }
}
```

**Usage:**
```csharp
[Entity("Product")]
[Table("Products")]
[DatabaseProvider("SqlServer")]
[TemporalTable]                              // SQL Server only — wrapped in provider check
public partial class Product
{
    [PrimaryKey] public int Id { get; set; }

    [DatabaseProvider("PostgreSql")]
    [Column(TypeName = "jsonb")]             // PostgreSQL only
    public string Metadata { get; set; } = "{}";

    [DatabaseProvider("SqlServer")]
    [Column(TypeName = "nvarchar(max)")]     // SQL Server fallback
    public string Metadata { get; set; } = "{}";
}
```

**What the SG generates:**
```csharp
protected virtual void ConfigureMetadata(EntityTypeBuilder<Product> builder)
{
    if (builder.Metadata.Model.GetDatabaseProvider() == "PostgreSql")
        builder.Property(e => e.Metadata).HasColumnType("jsonb");
    else
        builder.Property(e => e.Metadata).HasColumnType("nvarchar(max)");
}
```

### Phase 36: DbContext Factory Support
**Opt-in**: `[DbContext(UseFactory = true)]`

```csharp
// Add to DbContextAttribute:
[MetaProperty("UseFactory", "bool")]
public bool UseFactory { get; set; }                    // true = AddDbContextFactory instead of AddDbContext
```

**What the SG generates in Registration:**
```csharp
// When UseFactory = true:
services.AddDbContextFactory<SalesDbContext>(configureDbContext);
// When UseFactory = false (default):
services.AddDbContext<SalesDbContext>(configureDbContext);
```

Useful for Blazor Server, background services, and multi-threaded scenarios.

### Phase 37: Multi-DbContext + DSL-to-DSL
1. Implement `[DbContext(BoundedContext = "...")]` scoping
2. Emit models are already public — add XML documentation
3. Add `EntityDslModelBuilder` — programmatic builder for `EntityEmitModel` (fluent API for other generators to construct models without Roslyn)
4. Documentation in `Entity.Dsl/doc/PLAN.md`

---

## Complete Feature Matrix (All Opt-In)

| Feature | Opt-In Mechanism | Phase | Priority |
|---|---|---|---|
| Table/Column/PK/DbContext | `[Table]`, `[Column]`, `[PrimaryKey]`, `[DbContext]` | 1 | Core |
| Property config | `[Index]`, `[Precision]`, `[DefaultValue]`, `[Conversion]`, etc. | 2 | Core |
| Relationships | `[HasOne]`, `[HasMany]`, `[ManyToMany]`, `[OwnedEntity]`, `[SelfReference]` | 3 | Core |
| Association classes | `[AssociationClass(Left, Right)]` | 3 | Core |
| Inheritance | `[Inheritance(TPH/TPT/TPC)]` | 4 | Core |
| Views/Keyless | `[View]`, `[Keyless]` | 4 | Core |
| Validation | `[Validate]`, `[ValidateProperty]` | 5 | Core |
| Diagnostics (110+) | Automatic — runs on every build | 5 | Core |
| Timestampable | `[Timestampable]` | 6 | Core |
| SoftDeletable | `[SoftDeletable]` | 6 | Core |
| Blameable | `[Blameable]` | 6 | Core |
| Versionable | `[Versionable]` | 6 | Core |
| Sluggable | `[Sluggable("Source")]` | 6 | Core |
| Entity Listeners | `[EntityListener(typeof(...))]` | 6 | Core |
| Global Listeners | `IGlobalEntityListener` | 6 | Core |
| EF Core Advanced | `[StoredProcedure]`, `[Trigger]`, `[EntitySplit]`, `[AutoInclude]`, etc. | 7 | Core |
| Loggable (Audit) | `[Loggable]` | 8 | Core |
| Translatable | `[Translatable]` + `[TranslatableProperty]` | 8 | Core |
| Sortable | `[Sortable]` | 8 | Core |
| TreeNode | `[TreeNode]` | 8 | Core |
| Multi-tenancy | `[Tenantable]` + `[DbContext(MultiTenancy="RowLevel")]` | 9 | High |
| I18N enhancements | `[Translatable(FallbackChain="...")]` + query extensions | 10 | High |
| Bulk operations | `IRepository<T>.BulkInsertAsync/BulkDeleteAsync` | 11 | High |
| Polymorphic assoc. | `[PolymorphicOwner(typeof(A), typeof(B), typeof(C))]` | 12 | High |
| Read replicas | `[DbContext(ReadReplica="...")]` | 13 | Medium |
| Advanced specs | `Specification<T>.And/Or/Not` | 14 | Medium |
| Temporal queries | `[TemporalTable]` → typed query extensions | 15 | Medium |
| Domain events + Outbox | `[OutboxEnabled]` | 16 | Medium |
| Event sourcing | `[EventSourced]` (separate project) | 17 | Medium |
| Testing helpers | `FakeRepository<T>`, `FakeUnitOfWork` | 18 | Must-have |
| Pagination | `PagedResult<T>`, `FindPagedAsync` | 19 | Must-have |
| JSON columns | `[JsonColumn]` for arbitrary types | 20 | Must-have |
| Read-only UoW | `[DbContext(GenerateReadOnlyUnitOfWork=true)]` | 21 | Should-have |
| DbContext pooling | `[DbContext(UsePooling=true)]` | 22 | Should-have |
| ERD generation | `[DbContext(GenerateErd=true)]` | 23 | Should-have |
| Schema guard | `[DbContext(SchemaGuard=true)]` → breaking change detection | 24 | Should-have |
| Env-aware seeding | `[SeedData("method", Environment="Development")]` | 25 | Nice-to-have |
| Conflict resolution | `[Versionable(ConflictStrategy="Merge")]` + `IConflictResolver<T>` | 26 | Nice-to-have |
| Value object collections | `[JsonColumn]` + `[ValueObject]` = JSON array | 27 | Nice-to-have |
| Health checks | `[DbContext(HealthCheck=true)]` | 28 | Nice-to-have |
| Observability | `[Traced]` → OpenTelemetry spans | 29 | Nice-to-have |
| Full-text search | `[FullTextIndex]` | 30 | Nice-to-have |
| Spatial data | `[Spatial(Srid=4326)]` | 31 | Nice-to-have |
| DB-first scaffolding | CLI: `dotnet entity-dsl scaffold` | 32 | Nice-to-have |
| Encryption (GDPR) | `[Encrypted]` + `IEncryptionProvider` | 33 | Nice-to-have |
| Caching | `[Cacheable]` + `ICacheProvider` | 34 | Nice-to-have |
| Provider abstraction | `[DatabaseProvider("SqlServer")]` | 35 | Nice-to-have |
| DbContext factory | `[DbContext(UseFactory=true)]` | 36 | Nice-to-have |
| DSL-to-DSL | `EntityDslModelBuilder` | 37 | Final |

---

## Critical Files to Reference

- [BuilderGenerator.cs](Builder/src/FrenchExDev.Net.Builder.SourceGenerator/BuilderGenerator.cs) — SG discovery pattern (ForAttributeWithMetadataName)
- [Builder SG csproj](Builder/src/FrenchExDev.Net.Builder.SourceGenerator/FrenchExDev.Net.Builder.SourceGenerator.csproj) — linked source files pattern
- [BuilderEmitter.cs](Builder/src/FrenchExDev.Net.Builder.SourceGenerator.Lib/BuilderEmitter.cs) — StringBuilder emitter pattern
- [AggregateRootAttribute.cs](Ddd/src/FrenchExDev.Net.Ddd.Attributes/AggregateRootAttribute.cs) — MetaConcept + MetaConstraint pattern
- [CompositionAttribute.cs](Ddd/src/FrenchExDev.Net.Ddd.Attributes/CompositionAttribute.cs) — marker attribute pattern
- [EmitMicrosoftTests.cs](Injectable/test/FrenchExDev.Net.Injectable.SourceGenerator.Lib.Tests/EmitMicrosoftTests.cs) — emitter unit test pattern
- [InjectableEmitter.cs](Injectable/src/FrenchExDev.Net.Injectable.SourceGenerator.Lib/InjectableEmitter.cs) — emitter implementation pattern
- [Directory.Packages.props](Directory.Packages.props) — add EF Core package versions

---

## Verification

1. `dotnet build Entity.Dsl/FrenchExDev.Net.Entity.Dsl.slnx` — all projects compile
2. `dotnet test Entity.Dsl/test/FrenchExDev.Net.Entity.Dsl.Tests/` — emitter + concept + diagnostic tests pass
3. `dotnet test Entity.Dsl/test/FrenchExDev.Net.Entity.Dsl.Integration.Tests/` — SG + EF Core SQLite tests pass
4. Verify generated files in consuming project `obj/`:
   - `*ConfigurationBase.g.cs` + `*Configuration.g.cs` + `*ConfigurationRegistration.g.cs`
   - `I*Repository.g.cs` + `*Repository.g.cs` (inherits `RepositoryBase<T>` or custom intermediate)
   - `I*UnitOfWork.g.cs` + `*UnitOfWorkBase.g.cs` + `*UnitOfWork.g.cs`
   - `*DbContextBase.g.cs` + `*DbContext.g.cs`
   - `{Context}Registration.g.cs` (AddDbContext only)
   - `*.Behaviors.g.cs` (for entities with behavior attributes)
5. Verify diagnostics appear in IDE for invalid attribute combinations
6. Verify DI registration works: `services.AddSalesDbContext(o => o.UseSqlite(...))` + `services.AddMyAppInjectables()` (Injectable SG handles repos/UoW)
7. Verify developer can: override repo methods via partial, create project-level intermediate via `[DbContext(RepositoryBase=...)]`, add custom UoW business methods, implement entity listeners, stack behavior attributes
8. Verify multi-tenancy: `[Tenantable]` generates TenantId, query filter excludes other tenants
9. Verify outbox: `[OutboxEnabled]` captures domain events in same transaction
10. Verify polymorphic: `[PolymorphicOwner]` generates discriminator + typed query helpers
11. Verify testing: `FakeRepository<T>` and `FakeUnitOfWork` work without EF Core
12. Verify pagination: `FindPagedAsync` returns correct `PagedResult<T>` with TotalCount/TotalPages
13. Verify JSON columns: `[JsonColumn]` on `List<string>` serializes to JSON with proper value comparer
14. Verify ERD: `[DbContext(GenerateErd=true)]` emits valid Mermaid diagram
15. Verify health check: `[DbContext(HealthCheck=true)]` registers `AddDbContextCheck`
