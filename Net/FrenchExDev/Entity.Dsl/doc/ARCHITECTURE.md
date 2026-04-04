# Entity.Dsl -- Architecture

## 1. Overview

Entity.Dsl is an attribute-based DSL and Roslyn incremental source generator that eliminates EF Core Fluent API boilerplate. Developers decorate POCO classes with DDD and persistence attributes; the source generator reads both attribute families and emits production-ready `IEntityTypeConfiguration<T>`, DbContext, Repository, UnitOfWork, and entity listener infrastructure. The emit models are a public contract from day one, enabling DSL-to-DSL generation where another generator can produce Entity.Dsl models programmatically.

The design draws directly from the Doctrine ORM / Symfony Diem CMF tradition: attribute-driven behaviors (Timestampable, SoftDeletable, Blameable, etc.), a Generation Gap pattern ensuring generated code is never hand-edited, and per-entity lifecycle listeners dispatched from the DbContext.

---

## 2. Project Structure

```
Entity.Dsl/
  FrenchExDev.Net.Entity.Dsl.slnx
  src/
    FrenchExDev.Net.Entity.Dsl.Attributes/          (netstandard2.0;net10.0)
    FrenchExDev.Net.Entity.Dsl.Abstractions/         (net8.0;net10.0)
    FrenchExDev.Net.Entity.Dsl.SourceGenerator/      (netstandard2.0, IsRoslynComponent)
    FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib/  (netstandard2.0)
  test/
    FrenchExDev.Net.Entity.Dsl.Tests/                (net10.0)
    FrenchExDev.Net.Entity.Dsl.Integration.Tests/    (net10.0)
```

| Project | TFM | Purpose |
|---------|-----|---------|
| **Attributes** | `netstandard2.0;net10.0` | All Entity.Dsl attributes + MetaConcept companions. Persistence-specific (Table, Column, PrimaryKey, etc.) and behavior attributes (Timestampable, SoftDeletable, etc.). No EF Core dependency. |
| **Abstractions** | `net8.0;net10.0` | Runtime interfaces and base classes: `IRepository<T>`, `IReadOnlyRepository<T>`, `IUnitOfWork`, `IEntityListener<T>`, `IGlobalEntityListener`, `IAssociationRepository<,,>`, `ISpecification<T>`, `ICurrentUserProvider`, `SlugHelper`. References EF Core. |
| **SourceGenerator** | `netstandard2.0` | `IIncrementalGenerator` implementation (`EntityDslGenerator`). Discovers attributes via Roslyn, builds emit models, emits diagnostics. Links Lib source files (same pattern as Builder SG). |
| **SourceGenerator.Lib** | `netstandard2.0` | Roslyn-free emitters + public emit models. Pure string emission. No external dependencies. This is the DSL-to-DSL contract surface. |
| **Tests** | `net10.0` | Emitter unit tests (hand-build emit models, call emitters, assert output strings), attribute/concept constraint tests, diagnostic negative tests via `CSharpGeneratorDriver`. |
| **Integration.Tests** | `net10.0` | End-to-end: SG + SQLite in-memory EF Core. Verifies generated config creates valid databases, cascade behaviors, query filters, developer overrides via partial classes. |

---

## 3. Dependency Graph

```
                         External
                        +-----------------------+
                        | Ddd.Attributes        |
                        | (Entity, AggregateRoot|
                        |  Composition, etc.)   |
                        +-----------+-----------+
                                    |
                        +-----------+-----------+
                        | Dsl (MetaConcept,     |
                        |  MetaPropertyAttr)    |
                        +-----------+-----------+
                                    |
                        +-----------+-----------+
                        | Injectable.Attributes |
                        | ([Injectable])        |
                        +-----------+-----------+
                                    |
         +--------------------------+---------------------------+
         |                                                      |
+--------+----------+                              +------------+-------------+
|   Attributes      |                              |   Abstractions           |
| (netstandard2.0;  |                              | (net8.0;net10.0)         |
|  net10.0)         |                              | IRepository, IUnitOfWork |
|                   |                              | IEntityListener, etc.    |
| Depends on:       |                              |                          |
|  - Dsl            |                              | Depends on:              |
|  - Ddd.Attributes |                              |  - EF Core               |
|  - Injectable.Attr|                              +--------------------------+
+-------------------+

+-------------------+        linked source files
| SourceGenerator   +------------------------------+
| (netstandard2.0)  |                              |
| IsRoslynComponent |                    +---------+----------+
|                   |                    | SourceGenerator.Lib |
| Depends on:       |                    | (netstandard2.0)    |
|  - CodeAnalysis   |                    | NO dependencies     |
|  - CodeAnalysis   |                    | Pure string emission|
|    .Analyzers     |                    +---------------------+
+-------------------+

+-------------------+       +---------------------------+
| Tests             |       | Integration.Tests         |
| (net10.0)         |       | (net10.0)                 |
|                   |       |                           |
| Depends on:       |       | Depends on:               |
|  - Attributes     |       |  - All src projects       |
|  - Lib            |       |  - EF Core Sqlite         |
|  - xUnit, Shouldly|       |  - xUnit, Shouldly        |
+-------------------+       +---------------------------+
```

Key rules:
- **Attributes** never references EF Core -- domain layer stays persistence-free.
- **SourceGenerator.Lib** has zero dependencies -- any generator can consume emit models.
- **SourceGenerator** uses Lib via linked source files (not project reference), following the Builder SG pattern.
- CPM (`Directory.Packages.props`) manages all package versions centrally.

---

## 4. Source Generator Pipeline

`EntityDslGenerator.cs` implements `IIncrementalGenerator`. The pipeline has four discovery stages that feed into a unified emission phase.

### 4.1 Discovery Flow

```
 ForAttributeWithMetadataName
           |
           v
+---------------------+   +-------------------------+   +---------------------------+
| Stage 1: Entities   |   | Stage 2: DbContexts    |   | Stage 3: Behaviors        |
| "...EntityAttribute"|   | "...DbContextAttribute" |   | [Timestampable],          |
| "...AggregateRoot   |   | + collected entities    |   | [SoftDeletable],          |
|  Attribute"         |   |                         |   | [Blameable], [Versionable]|
| "...AssociationClass|   |                         |   | [Sluggable], [Loggable],  |
|  Attribute"         |   |                         |   | [Translatable],[Sortable] |
+---------+-----------+   +------------+------------+   | [TreeNode]                |
          |                            |                +-------------+-------------+
          v                            v                              |
  EntityEmitModel            DbContextEmitModel                      v
  RepositoryEmitModel        UnitOfWorkEmitModel            Behavior flags/config
          |                            |                     on EntityEmitModel
          |                            |                              |
          +----------------------------+------------------------------+
                                       |
                          +------------+------------+
                          | Stage 4: Listeners      |
                          | "...EntityListener      |
                          |  Attribute"             |
                          | collected per entity    |
                          +------------+------------+
                                       |
                                       v
                              Emission Phase
                       (emitters produce .g.cs files)
```

### 4.2 Model Extraction (Roslyn to Roslyn-free)

The generator reads from the Roslyn semantic model and populates pure POCO emit models:

1. **Class-level attributes**: `[Table]`, `[View]`, `[Keyless]`, `[Owned]`, `[Inheritance]`, `[ShadowProperty]`, `[QueryFilter]`, `[CheckConstraint]`, `[SeedData]`, `[TemporalTable]`, `[TableSplit]`, `[EntitySplit]`, `[NamingConvention]`, `[StoredProcedure]`, `[Trigger]`, `[Validate]`, `[Comment]`, `[EntityListener]`
2. **Property-level attributes**: `[Column]`, `[PrimaryKey]`, `[Index]`, `[AlternateKey]`, `[Precision]`, `[DefaultValue]`, `[Conversion]`, `[EnumStorage]`, `[ConcurrencyToken]`, `[ComputedColumn]`, `[Sequence]`, `[AutoInclude]`, `[ValueGenerator]`, `[Collation]`, `[HiLo]`, `[ValueComparer]`, `[TranslatableProperty]`, `[ValidateProperty]`, `[Required]`, `[MaxLength]`, `[BackingField]`, `[Comment]`, `[NotMapped]`
3. **Relationship attributes**: `[HasOne]`, `[HasMany]`, `[ManyToMany]`, `[OwnedEntity]`, `[ComplexType]`, `[SelfReference]`
4. **DDD cross-read**: `[Composition]`/`[Aggregation]`/`[Association]` on same property infer `DeleteBehavior`. `[Property(Required, MaxLength)]` provides defaults that Entity.Dsl attributes can override.
5. **Convention inference**: Properties typed as `[Entity]`-decorated types or `ICollection<[Entity]>` auto-infer relationships without explicit attributes.

### 4.3 typeof() Handling

Attributes using `Type` properties (`[AssociationClass]`, `[ValueGenerator]`, `[Conversion]`, `[ValueComparer]`, `[EntityListener]`) are read via `TypedConstant.Value` as `INamedTypeSymbol`. The SG extracts the fully-qualified type name as a string for the emit model. The emitter uses this string in generated code (e.g., `new global::MyApp.MyGenerator()`). The SG never instantiates the actual `Type` at generation time.

---

## 5. Generation Gap Pattern

### 5.1 Why This Pattern

The Generation Gap pattern comes from the Doctrine/Diem CMF tradition. It solves the fundamental tension between code generation and developer customization:

- Generated code must be deterministic and idempotent (same input = same output).
- Developers need to add provider-specific config, filtered indexes, custom query filters, and business logic that no DSL can anticipate.
- Hand-editing generated files means losing changes on regeneration.

The solution: a 3-layer hierarchy where generated code lives in Base (always overwritten) and developer code lives in a partial class that merges with a generated stub (never overwritten).

### 5.2 The 3-Layer Hierarchy

```
+-----------------------------------------------------+
|  Layer 1: {Entity}ConfigurationBase.g.cs             |  <-- SG overwrites every build
|  abstract class, all methods virtual                 |
|  Contains: all Fluent API calls from attributes      |
+-----------------------------------------------------+
|  Layer 2: {Entity}Configuration.g.cs  (partial stub) |  <-- SG overwrites (empty partial)
|  partial class extending Base                        |
+-----------------------------------------------------+
|  Layer 3: {Entity}Configuration.cs    (developer)    |  <-- Developer-owned (optional)
|  partial class, overrides virtual methods            |
|  Merges with Layer 2 at compile time                 |
+-----------------------------------------------------+
     +
  {Entity}ConfigurationRegistration.g.cs               <-- SG overwrites, IEntityTypeConfiguration<T>
  Delegates to Configuration class
```

If the developer never creates Layer 3, the empty partial stub from Layer 2 is used -- pure DSL-driven, zero customization needed.

### 5.3 File Layout Per Entity

```
Generated (in obj/):
  OrderConfigurationBase.g.cs           -- always regenerated, abstract, all virtual methods
  OrderConfiguration.g.cs              -- always regenerated, partial stub (extends Base, empty)
  OrderConfigurationRegistration.g.cs  -- always regenerated, IEntityTypeConfiguration<T>
  IOrderRepository.g.cs               -- always regenerated, typed interface
  OrderRepositoryBase.g.cs            -- always regenerated, abstract base
  OrderRepository.g.cs                -- always regenerated, partial stub + [Injectable]
  Order.Behaviors.g.cs                -- always regenerated (if any behavior attributes)
  Order.AssociationNavigations.g.cs   -- only for [AssociationClass] endpoint entities

Developer (in src/):
  OrderConfiguration.cs               -- optional partial class with overrides
  OrderRepository.cs                  -- optional partial class with domain queries
```

### 5.4 File Layout Per DbContext

```
Generated (in obj/):
  {Context}Base.g.cs                   -- abstract DbContext with hooks + lifecycle dispatch
  {Context}.g.cs                       -- partial stub
  I{Context}UnitOfWork.g.cs            -- UnitOfWork interface with all repo properties
  {Context}UnitOfWorkBase.g.cs         -- abstract, virtual factory methods
  {Context}UnitOfWork.g.cs             -- partial stub + [Injectable]
  {Context}Registration.g.cs           -- AddDbContext extension method
  EntityDslConventions.g.cs            -- custom conventions (naming, soft delete)

Developer (in src/):
  {Context}.cs                         -- optional partial class with overrides
  {Context}UnitOfWork.cs               -- optional partial class with business methods
```

### 5.5 Override Hook Summary

| Hook | Layer | When | Use Case |
|------|-------|------|----------|
| `PreConfigure(builder)` | EntityConfig | Before all config | Global entity config (table comments) |
| `Configure{Property}(builder)` | EntityConfig | Per property | Override column type, provider-specific |
| `Configure{Relationship}(builder)` | EntityConfig | Per relationship | Change cascade, custom FK |
| `ConfigureTable(builder)` | EntityConfig | Table mapping | Change table, add triggers |
| `ConfigureIndexes(builder)` | EntityConfig | Index setup | Add filtered indexes |
| `ConfigureShadowProperties(builder)` | EntityConfig | Shadow props | Add/remove shadow props |
| `ConfigureCheckConstraints(builder)` | EntityConfig | Constraints | Add/modify constraints |
| `PostConfigure(builder)` | EntityConfig | After all config | Catch-all |
| `PreModelCreating(modelBuilder)` | DbContext | Before registration | Global conventions |
| `PostModelCreating(modelBuilder)` | DbContext | After registration | Provider-specific global |
| `RegisterConfigurations(modelBuilder)` | DbContext | Entity registration | Dynamic add/remove |
| `ConfigureSequences(modelBuilder)` | DbContext | Sequence definitions | Override sequences |
| `UpdateTimestamps()` | DbContext | Before SaveChanges | Custom timestamp logic |
| `OnEntitiesAdding(entries)` | DbContext | Before SaveChanges | Pre-insert (Doctrine prePersist) |
| `OnEntitiesModifying(entries)` | DbContext | Before SaveChanges | Pre-update (Doctrine preUpdate) |
| `OnEntitiesDeleting(entries)` | DbContext | Before SaveChanges | Pre-delete (Doctrine preRemove) |

---

## 6. SOLID Extensibility

### Single Responsibility

Each generated file has exactly one job:

| File | Responsibility |
|------|---------------|
| `*ConfigurationBase.g.cs` | EF Core Fluent API for one entity |
| `*ConfigurationRegistration.g.cs` | Wire configuration into `OnModelCreating` |
| `I*Repository.g.cs` | Define typed query contract |
| `*RepositoryBase.g.cs` | Implement CRUD against `DbSet<T>` |
| `*.Behaviors.g.cs` | Declare behavior properties on partial class |
| `*DbContextBase.g.cs` | Orchestrate SaveChanges lifecycle |
| `I*UnitOfWork.g.cs` | Define transaction boundary contract |

### Open/Closed

Base classes are generated and sealed for modification. Extension happens via:
- **Partial classes**: Developer adds a second partial file to override virtual methods.
- **Interfaces**: New capabilities are added by implementing `IEntityListener<T>` or custom `ISpecification<T>`.
- **Virtual methods**: Every property/relationship configuration is a separate virtual method, individually overridable.

The developer can completely replace a single property's config by overriding its method (skip `base` call), extend it (call `base` first, then add), or suppress it (empty override).

### Liskov Substitution

`OrderRepository` is substitutable for `IOrderRepository` everywhere. The UnitOfWork uses virtual factory methods (`CreateOrderRepository()`) so a developer can return a custom implementation:

```csharp
protected override IOrderRepository CreateOrderRepository()
    => new AuditedOrderRepository(Context);
```

### Interface Segregation

- **`IReadOnlyRepository<T>`**: Queries only (FindById, FindWhere, FindBySpec, Count, Exists, Query).
- **`IRepository<T>`**: Extends read-only with commands (Add, Update, Remove, Attach).
- **`IAssociationRepository<TAssoc, TLeft, TRight>`**: Extends `IRepository<T>` with association-specific queries (FindByEndpoints, FindByLeft, FindByRight).
- **`IEntityListener<T>`**: All 7 methods (OnAdding, OnAdded, OnModifying, OnModified, OnRemoving, OnRemoved, OnLoaded) have default implementations (empty). Developers implement only the hooks they care about.

### Dependency Inversion

Services depend on interfaces, never concrete classes:

```
Application layer  -->  IOrderRepository, ISalesUnitOfWork
                        (interfaces from Abstractions / generated)
                                    |
Infrastructure     <--  OrderRepository, SalesUnitOfWork
                        (concrete, registered via [Injectable])
```

The generated `DbContextRegistration` extension is the only place where the concrete `DbContext` class is referenced directly (required by EF Core's `AddDbContext` API).

---

## 7. Emit Models

Emit models live in **SourceGenerator.Lib** with zero Roslyn dependency. They are the public contract surface for DSL-to-DSL generation: any generator that can populate an `EntityEmitModel` can produce EF Core infrastructure.

### Primary Models

| Model | Purpose |
|-------|---------|
| `EntityEmitModel` | Complete entity: table/view mapping, properties, keys, indexes, relationships, owned types, complex types, shadow properties, inheritance, query filters, check constraints, seed data, temporal table, table/entity splitting, behaviors, listeners, triggers, stored procedures |
| `DbContextEmitModel` | DbContext: namespace, class name, bounded context, DbSets, sequences, context-level config (lazy loading, query tracking, retry), entity behavior summaries, global listener flag |
| `RepositoryEmitModel` | Repository: entity class, PK type, composite key info, association class flag, left/right types, typed DbContext |
| `UnitOfWorkEmitModel` | UnitOfWork: DbContext reference, list of repository interfaces + implementations + property names |

### Sub-Models

| Model | Used By | Purpose |
|-------|---------|---------|
| `KeyPropertyModel` | EntityEmitModel | PK property: name, order, value generation, sequence config |
| `PropertyConfigModel` | EntityEmitModel | Property: column name/type/order, required, max length, precision, default value, conversion, concurrency, computed column, enum storage, comment, backing field, value generator, comparer, collation, HiLo, auto-include, translatable, not-mapped |
| `RelationshipModel` | EntityEmitModel | Navigation: kind (HasOne.WithMany, etc.), inverse, FK, PK, delete behavior, self-reference, M:M join config |
| `OwnedEntityModel` | EntityEmitModel | Owned type: navigation, type, collection flag, table name, JSON column |
| `ComplexTypeModel` | EntityEmitModel | Complex property: navigation + type |
| `IndexModel` | EntityEmitModel | Index: name, properties, unique, descending |
| `AlternateKeyModel` | EntityEmitModel | Alternate key: group name, properties |
| `ShadowPropertyModel` | EntityEmitModel | Shadow property: name, CLR type, required, default SQL |
| `InheritanceModel` | EntityEmitModel | Strategy (TPH/TPT/TPC), discriminator column/value, derived types |
| `DerivedTypeModel` | InheritanceModel | Derived type: full name, discriminator value, table name |
| `CheckConstraintModel` | EntityEmitModel | Constraint: name + SQL expression |
| `TemporalTableModel` | EntityEmitModel | History table name + schema |
| `TableSplitModel` | EntityEmitModel | Multiple entities to one table |
| `EntitySplitModel` | EntityEmitModel | One entity across multiple tables |
| `StoredProcedureModel` | EntityEmitModel | Insert/Update/Delete procedure names |
| `AssociationClassModel` | EntityEmitModel | Left/Right types, multiplicities, delete behaviors, FK names, navigation properties |
| `DbSetModel` | DbContextEmitModel | Entity type, property name, keyless flag |
| `EntityBehaviorSummary` | DbContextEmitModel | Per-entity behavior config for DbContextBase `PopulateBehaviors()` |
| `UnitOfWorkRepositoryModel` | UnitOfWorkEmitModel | Interface type, implementation type, property name |

### Behavior Models

| Model | Configures |
|-------|-----------|
| `TimestampableBehaviorModel` | Property names (CreatedAt/UpdatedAt), type, precision, timezone, immutability, child propagation, column names |
| `SoftDeletableBehaviorModel` | Property names (IsDeleted/DeletedAt), cascade to children, allow hard delete, filter enabled, allow restore, column names |
| `BlameableBehaviorModel` | Property names (CreatedBy/UpdatedBy/DeletedBy), track deleted by, identifier type, max length, required, column names |
| `VersionableBehaviorModel` | Property name, strategy (RowVersion/Guid/Timestamp/Increment), type override, column name |
| `SluggableBehaviorModel` | Source properties, separator, output property name, max length, unique + scope, duplicate suffix, regeneration strategy, transliteration, lowercase, column name |
| `LoggableBehaviorModel` | Audit table name/schema, tracked/ignored properties, which operations to log, old/new value capture, value format (Json/Columns), user tracking, retention days |
| `TranslatableBehaviorModel` | Default locale, translation table name/schema, locale column config, fallback strategy, unique per locale, translatable property names |
| `SortableBehaviorModel` | Property name, group-by keys, start value, on-delete/on-insert behavior, column name, index creation |
| `TreeNodeBehaviorModel` | Strategy (MaterializedPath/AdjacencyList/ClosureTable), property names, path config, closure table name, max depth, on-delete behavior, sibling ordering, query extension generation |

---

## 8. Emitters

All emitters are static classes in SourceGenerator.Lib. They accept emit models and return `string` (generated C# source).

### EntityConfigurationEmitter

| Method | Output File | Content |
|--------|-------------|---------|
| `EmitBase(EntityEmitModel)` | `{Entity}ConfigurationBase.g.cs` | Abstract class. Virtual methods: `PreConfigure`, `PostConfigure`, `ConfigureTable`, `ConfigurePrimaryKey`, one `Configure{Property}` per property, one `Configure{Relationship}` per navigation, `ConfigureIndexes`, `ConfigureShadowProperties`, `ConfigureCheckConstraints`, behavior-specific `Configure{Behavior}`. Sealed `Configure()` orchestrator calling all virtuals in order. |
| `EmitPartialStub(EntityEmitModel)` | `{Entity}Configuration.g.cs` | `partial class {Entity}Configuration : {Entity}ConfigurationBase { }` -- empty, developer extends via second partial file. |
| `EmitRegistration(EntityEmitModel)` | `{Entity}ConfigurationRegistration.g.cs` | `IEntityTypeConfiguration<T>` that delegates to `new {Entity}Configuration().Configure(builder)`. |

### DbContextEmitter

| Method | Output File | Content |
|--------|-------------|---------|
| `EmitBase(DbContextEmitModel)` | `{Context}Base.g.cs` | Abstract `DbContext` subclass. DbSet properties, sealed `OnModelCreating` orchestrator, `PreModelCreating`/`PostModelCreating` hooks, `RegisterConfigurations`, `ConfigureSequences`, `UpdateTimestamps`, `PopulateBehaviors`, `OnEntitiesAdding`/`Modifying`/`Deleting`, `SaveChanges`/`SaveChangesAsync` overrides calling `OnBeforeSaveChanges`. Entity listener dispatcher. `OnConfiguring` for context-level settings (lazy loading, query tracking, etc.). |
| `EmitPartialStub(DbContextEmitModel)` | `{Context}.g.cs` | Partial stub extending Base. |

### RepositoryEmitter

| Method | Output File | Content |
|--------|-------------|---------|
| `EmitInterface(RepositoryEmitModel)` | `I{Entity}Repository.g.cs` | Typed interface extending `IRepository<T>` with typed `FindByIdAsync`. For association classes: extends `IAssociationRepository<TAssoc, TLeft, TRight>`. |
| `EmitBase(RepositoryEmitModel)` | `{Entity}RepositoryBase.g.cs` | Abstract class implementing the interface. Takes typed `DbContext` in constructor. All methods virtual. Exposes `Context` and `DbSet` to derived classes. Specification pattern support. |
| `EmitPartialStub(RepositoryEmitModel)` | `{Entity}Repository.g.cs` | Partial stub with `[Injectable(Scope.Scoped, As = typeof(I{Entity}Repository))]`. |

### UnitOfWorkEmitter

| Method | Output File | Content |
|--------|-------------|---------|
| `EmitInterface(UnitOfWorkEmitModel)` | `I{Context}UnitOfWork.g.cs` | Typed interface extending `IUnitOfWork<TContext>` with repository properties. |
| `EmitBase(UnitOfWorkEmitModel)` | `{Context}UnitOfWorkBase.g.cs` | Abstract class. Lazy repository initialization. Virtual `Create{Entity}Repository()` factory methods. `SaveChanges`, `BeginTransactionAsync`, `DetachAll`, `HasChanges`. Transaction wrapper. `IDisposable`/`IAsyncDisposable`. |
| `EmitPartialStub(UnitOfWorkEmitModel)` | `{Context}UnitOfWork.g.cs` | Partial stub with `[Injectable(Scope.Scoped, As = typeof(I{Context}UnitOfWork))]`. |

### DbContextRegistrationEmitter

| Method | Output File | Content |
|--------|-------------|---------|
| `Emit(DbContextEmitModel)` | `{Context}Registration.g.cs` | Extension method `Add{Context}(IServiceCollection, Action<DbContextOptionsBuilder>)` in `Microsoft.Extensions.DependencyInjection` namespace. This is the only EF-specific registration; all repos/UoW use `[Injectable]`. |

### BehaviorEmitter

| Method | Output File | Content |
|--------|-------------|---------|
| `EmitProperties(EntityEmitModel)` | `{Entity}.Behaviors.g.cs` | Partial class with generated CLR properties for all active behaviors (CreatedAt, UpdatedAt, IsDeleted, Slug, RowVersion, Position, etc.). |
| *(companion entities)* | `{Entity}AuditLog.g.cs` | Only for `[Loggable]`. Companion audit log entity + configuration. |
| *(companion entities)* | `{Entity}Translation.g.cs` | Only for `[Translatable]`. Companion translation entity + configuration. |

### NamingHelper

Utility class for name transformations:

| Method | Example |
|--------|---------|
| `ToSnakeCase(string)` | `"OrderNumber"` --> `"order_number"` |
| `ToCamelCase(string)` | `"OrderNumber"` --> `"orderNumber"` |
| `Pluralize(string)` | `"Order"` --> `"Orders"`, `"Category"` --> `"Categories"` |

---

## 9. Attribute Architecture

### 9.1 Design Principle: Complement DDD, Don't Duplicate

DDD attributes (`[Entity]`, `[AggregateRoot]`, `[EntityId]`, `[ValueObject]`, `[Composition]`, `[Aggregation]`, `[Association]`) stay in `Ddd.Attributes`. Entity.Dsl adds **persistence-specific** EF Core semantics. The SG reads both attribute families from the same class.

Why custom attributes instead of EF Core's built-in `[Table]`, `[Column]`:
1. **Domain purity** -- POCOs never reference `Microsoft.EntityFrameworkCore`.
2. **MetaConcept validation** -- compile-time constraint checking via the DSL metamodel.
3. **DSL-to-DSL readiness** -- another generator can produce Entity.Dsl attributes programmatically.
4. **Superset of EF Core annotations** -- covers Fluent-API-only features (HasCheckConstraint, ToView, query filters).

### 9.2 MetaConcept Companion Pattern

Every Entity.Dsl attribute has a companion MetaConcept:

```
[Table]          -->  TableConcept
[PrimaryKey]     -->  PrimaryKeyConcept (MetaInherits EntityIdConcept)
[OwnedEntity]    -->  OwnedEntityConcept (MetaInherits CompositionConcept)
[ComplexType]    -->  ComplexTypeConcept (MetaInherits ValueObjectConcept)
```

`MetaInherits` bridges Entity.Dsl concepts back to DDD concepts. `[PrimaryKey]` IS-A EntityId. `[OwnedEntity]` IS-A Composition. This allows the SG to reason about DDD semantics even when Entity.Dsl-specific attributes are used.

### 9.3 Class-Level Attributes

| Attribute | Key Properties | EF Core Emission |
|-----------|---------------|-----------------|
| `[Table]` | Name?, Schema? | `ToTable("name", "schema")` |
| `[View]` | Name, Schema? | `ToView("name", "schema"); HasNoKey()` |
| `[Keyless]` | -- | `HasNoKey()` |
| `[Inheritance]` | Strategy, DiscriminatorColumn?, DiscriminatorValue? | `HasDiscriminator<T>()` / `ToTable()` / `UseTpcMappingStrategy()` |
| `[DbContext]` | Name?, BoundedContext?, LazyLoading, QueryTracking, etc. | DbSets, OnModelCreating, OnConfiguring |
| `[ShadowProperty]` | Name, ClrType, Required?, DefaultValueSql? | `Property<T>("name")` |
| `[QueryFilter]` | FilterMethod | `HasQueryFilter(Entity.Method())` |
| `[CheckConstraint]` | Name, Sql | `HasCheckConstraint("name", "sql")` |
| `[SeedData]` | SeedMethod | `HasData(Entity.Method())` |
| `[TemporalTable]` | HistoryTable?, HistorySchema? | `ToTable(tb => tb.IsTemporal(...))` |
| `[TableSplit]` | TableName, Schema? | Multiple entities to same table |
| `[EntitySplit]` | TableName, Schema?, Properties | `SplitToTable("name", ...)` |
| `[NamingConvention]` | Strategy (PascalCase/SnakeCase/CamelCase) | `HasColumnName()` on all properties |
| `[Comment]` | Text | `HasComment("...")` |
| `[StoredProcedure]` | InsertProcedure?, UpdateProcedure?, DeleteProcedure? | `InsertUsing/UpdateUsing/DeleteUsingStoredProcedure()` |
| `[Trigger]` | Name | `ToTable(tb => tb.HasTrigger("name"))` |
| `[Owned]` | TableName?, JsonColumn? | Auto-infers OwnsOne/OwnsMany for navigations to this type |
| `[AssociationClass]` | Name, Left, Right, Multiplicities, OnDelete | Composite PK, FK navigations, skip navigations |
| `[Validate]` | Method | SG invokes static method at compile-time |

### 9.4 Property-Level Attributes

| Attribute | Key Properties | EF Core Emission |
|-----------|---------------|-----------------|
| `[Column]` | Name?, TypeName?, Order | `HasColumnName()`, `HasColumnType()`, `HasColumnOrder()` |
| `[PrimaryKey]` | Order, ValueGenerated | `HasKey()`, `ValueGeneratedOnAdd/OnUpdate/Never()` |
| `[AlternateKey]` | GroupName? | `HasAlternateKey()` |
| `[Index]` | Name?, IsUnique, GroupName?, IsDescending? | `HasIndex().IsUnique().HasDatabaseName().IsDescending()` |
| `[Precision]` | P, S | `HasPrecision(p, s)` |
| `[DefaultValue]` | Value?, Sql? | `HasDefaultValue()` / `HasDefaultValueSql()` |
| `[Conversion]` | ConverterType | `HasConversion<T>()` |
| `[EnumStorage]` | AsString | `HasConversion<string>()` or none |
| `[ConcurrencyToken]` | IsRowVersion | `IsConcurrencyToken()` / `IsRowVersion()` |
| `[ComputedColumn]` | Sql, Stored | `HasComputedColumnSql("sql", stored)` |
| `[Sequence]` | Name, Schema?, StartsAt?, IncrementsBy? | `HasSequence().StartsAt().IncrementsBy()` + `UseSequence()` |
| `[Required]` | -- | `.IsRequired()` |
| `[MaxLength]` | Length | `.HasMaxLength(n)` |
| `[BackingField]` | FieldName, AccessMode? | `.HasField("_x").UsePropertyAccessMode(...)` |
| `[Comment]` | Text | `.HasComment("...")` |
| `[AutoInclude]` | -- | `.Navigation(e => e.X).AutoInclude()` |
| `[ValueGenerator]` | GeneratorType | `.HasValueGenerator<T>()` |
| `[Collation]` | Name | `.UseCollation("...")` |
| `[HiLo]` | SequenceName?, Schema? | `.UseHiLo("seq", "schema")` |
| `[ValueComparer]` | ComparerType | `.Metadata.SetValueComparer(new T())` |
| `[NotMapped]` | -- | Property excluded from EF Core mapping |
| `[TranslatableProperty]` | -- | Marks property for translation companion |
| `[ValidateProperty]` | Method | SG invokes static method at compile-time |

### 9.5 Relationship Attributes

| Attribute | Key Properties | EF Core Emission |
|-----------|---------------|-----------------|
| `[HasOne]` | WithMany?, ForeignKey?, PrincipalKey?, OnDelete?, IsRequired? | `HasOne().WithMany().HasForeignKey().OnDelete()` |
| `[HasMany]` | WithOne?, ForeignKey?, OnDelete? | `HasMany().WithOne().HasForeignKey().OnDelete()` |
| `[ManyToMany]` | JoinEntity?, LeftForeignKey?, RightForeignKey?, JoinTable? | `HasMany().WithMany().UsingEntity<T>()` |
| `[OwnedEntity]` | TableName?, JsonColumn? | `OwnsOne()` / `OwnsMany()` + optional `ToJson()` |
| `[ComplexType]` | -- | `ComplexProperty()` |
| `[SelfReference]` | InverseNavigation?, ForeignKey?, OnDelete? | `HasOne(e => e.Parent).WithMany(e => e.Children)` |

### 9.6 Lifecycle Convention Table

When no explicit `OnDelete` is set, the SG infers from DDD attributes:

| DDD Attribute | Default DeleteBehavior | Rationale |
|---------------|----------------------|-----------|
| `[Composition]` | `Cascade` | Parent owns child lifecycle completely |
| `[Aggregation]` | `Restrict` | Shared reference -- prevent accidental orphan |
| `[Association]` | `NoAction` | Independent lifecycle -- no cascade |
| *(none)* | Infer from nullability | Nullable nav --> `ClientSetNull`, non-nullable --> `Cascade` (EF Core default) |

### 9.7 Attribute Conflict Resolution

| Conflict | Resolution |
|----------|-----------|
| `[Column(Name="x")]` + `[NamingConvention(SnakeCase)]` | Explicit `[Column]` wins |
| `[Table]` + `[View]` | SG error `EDSL0006` |
| `[OwnedEntity]` + `[Aggregation]` | SG error `EDSL0013` |
| `[Required]` + DDD `[Property(Required=true)]` | Additive, either one triggers `IsRequired()` |
| `[MaxLength(50)]` + DDD `[Property(MaxLength=100)]` | Entity.Dsl wins (more specific) |
| `[PrimaryKey]` + `[EntityId]` | `[PrimaryKey]` inherits EntityIdConcept, its config is used |
| `[Keyless]` + `[PrimaryKey]` | SG error |
| `[ComputedColumn]` + `[DefaultValue]` | SG error |
| `[HiLo]` + `[Sequence]` | SG error -- mutually exclusive key strategies |
| `[NotMapped]` + `[Column]`/`[Index]` | `[NotMapped]` wins, SG warning |
| `[NotMapped]` + `[PrimaryKey]` | SG error -- PK cannot be unmapped |

---

## 10. Behavior System

Behaviors follow the Doctrine `@Gedmo\Timestampable` approach: opt-in via class-level attributes, the SG generates everything.

### 10.1 How It Works

For each behavior attribute, the SG generates three things:

1. **Properties** -- in `{Entity}.Behaviors.g.cs` (partial class with CLR properties).
2. **Configuration** -- virtual methods in `{Entity}ConfigurationBase.g.cs` (e.g., `ConfigureTimestampable`, `ConfigureSoftDeletable`).
3. **SaveChanges hooks** -- in `DbContextBase.g.cs` via `PopulateBehaviors()`.

### 10.2 Behavior Catalog

| Behavior | Generated Properties | Config | SaveChanges Hook | Extra |
|----------|---------------------|--------|-----------------|-------|
| `[Timestampable]` | `CreatedAt`, `UpdatedAt` | `IsRequired()` | Set on Add/Modify | Configurable type, precision, timezone |
| `[SoftDeletable]` | `IsDeleted`, `DeletedAt` | `HasDefaultValue(false)`, `HasQueryFilter` | Intercept Remove --> soft-delete | Override `Remove()` in RepoBase |
| `[Blameable]` | `CreatedBy`, `UpdatedBy` | -- | Resolve `ICurrentUserProvider` --> set | Optional `DeletedBy` via `TrackDeletedBy` |
| `[Versionable]` | `RowVersion` | `IsRowVersion()` or `IsConcurrencyToken()` | App-managed for Guid/Timestamp/Increment | 4 strategies: RowVersion, Guid, Timestamp, Increment |
| `[Sluggable("Source")]` | `Slug` | `HasMaxLength`, unique index | Compute slug on Add/Modify | Transliteration, dedup suffix, regeneration policy |
| `[Loggable]` | -- | -- | Capture old/new on Modify/Remove | Generates companion `{Entity}AuditLog` entity + config |
| `[Translatable]` | -- | -- | -- | Generates companion `{Entity}Translation` entity |
| `[Sortable]` | `Position` | Optional index | Auto-assign on Add (max+1) | Reorder on Remove, configurable grouping |
| `[TreeNode]` | `ParentId`, `MaterializedPath`, `Depth` | FK to self, index on path | Auto-maintain on Add/Move | 3 strategies, query extension methods |

### 10.3 Behavior vs Shadow Property

Behaviors generate **real CLR properties** on the entity (visible to domain code, queryable with LINQ). Shadow properties are invisible to the domain. Use behaviors when the domain needs to read/query the values; use shadow properties for purely infrastructure concerns (e.g., tenant ID for multi-tenant filtering).

### 10.4 Custom Conventions

The SG generates `IModelFinalizingConvention` implementations for cross-cutting concerns:

- `NamingConventionModelConvention` -- applies `[NamingConvention]` to all properties of decorated entities.
- `SoftDeletableConvention` -- auto-adds `HasQueryFilter` for `ISoftDeletable` entities.

Registered in `DbContextBase.ConfigureConventions()`.

---

## 11. Repository + UnitOfWork

### 11.1 Repository Vertical Slice

For each `[Entity]`/`[AggregateRoot]`/`[AssociationClass]`, the SG generates a complete vertical slice:

```
IOrderRepository         -- typed interface (extends IRepository<Order>)
     |
OrderRepositoryBase      -- abstract, all methods virtual, typed DbContext ctor
     |
OrderRepository          -- partial stub + [Injectable(Scope.Scoped)]
     |
OrderRepository.cs       -- developer partial (optional, domain queries)
```

The base repository takes a **typed DbContext** (e.g., `SalesDbContext`), not a generic `DbContext`. This ensures compile-time safety and scopes the repository to its bounded context.

### 11.2 Association Repository

For `[AssociationClass]` entities, the generated interface extends `IAssociationRepository<TAssoc, TLeft, TRight>` with additional methods:

- `FindByEndpointsAsync(leftKey, rightKey)`
- `FindByLeftAsync(leftKey)` / `FindByRightAsync(rightKey)`
- `FindRightsByLeftAsync(leftKey)` / `FindLeftsByRightAsync(rightKey)`

### 11.3 UnitOfWork

The UnitOfWork coordinates multiple repositories in a single transaction, following the same 3-layer Generation Gap:

```
ISalesUnitOfWork             -- interface with typed repo properties
     |
SalesUnitOfWorkBase          -- abstract, lazy repo init, virtual factories
     |
SalesUnitOfWork              -- partial stub + [Injectable]
     |
SalesUnitOfWork.cs           -- developer partial (optional, business methods)
```

Key design decisions:
- Repositories are lazily initialized via `??=` pattern.
- Factory methods (`CreateOrderRepository()`) are virtual -- developers can inject custom implementations.
- `IUnitOfWork` extends `IAsyncDisposable` and `IDisposable`.
- `IUnitOfWorkTransaction` wraps `IDbContextTransaction` for explicit transaction control.

### 11.4 DI Registration via [Injectable]

Entity.Dsl **reuses** the existing `[Injectable]` attribute from `FrenchExDev.Net.Injectable.Attributes`:

- Generated repositories carry `[Injectable(Scope.Scoped, As = typeof(IOrderRepository))]`.
- Generated UnitOfWork carries `[Injectable(Scope.Scoped, As = typeof(ISalesUnitOfWork))]`.
- The Injectable SG picks these up and generates `services.AddScoped<IOrderRepository, OrderRepository>()`.

Only `AddDbContext` gets a dedicated generated extension method (requires EF Core-specific configuration lambda).

Startup:
```csharp
services.AddSalesDbContext(o => o.UseSqlServer("..."));   // EF-specific
services.AddMyAppInjectables();                            // Injectable SG handles repos + UoW
```

### 11.5 Multi-DbContext / Bounded Context

Each `[DbContext(BoundedContext = "X")]` gets its own complete file set. Entities are routed by `[AggregateRoot(BoundedContext = "X")]`. Entities without a BoundedContext go to any unscoped DbContext.

---

## 12. Entity Listeners

### 12.1 Two Listener Types

| | `IEntityListener<T>` | `IGlobalEntityListener` |
|---|---|---|
| Scope | One entity type | All entities |
| Registration | `[EntityListener(typeof(...))]` on entity class | `[Injectable(As = typeof(IGlobalEntityListener))]` in DI |
| Parameter | Strongly typed `T entity` | Untyped `EntityEntry entry` |
| Performance | Only invoked for matching entities | Invoked for every entity change |
| Use case | Entity-specific audit, validation | Cross-cutting: logging, metrics, security |

### 12.2 IEntityListener<T> Methods

All methods have default empty implementations (ISP):

| Method | Doctrine Equivalent | When |
|--------|-------------------|------|
| `OnAddingAsync` | prePersist | Before INSERT |
| `OnAddedAsync` | postPersist | After INSERT |
| `OnModifyingAsync` | preUpdate | Before UPDATE |
| `OnModifiedAsync` | postUpdate | After UPDATE |
| `OnRemovingAsync` | preRemove | Before DELETE |
| `OnRemovedAsync` | postRemove | After DELETE |
| `OnLoadedAsync` | postLoad | After materialization |

### 12.3 Dispatching in DbContextBase

The generated `DbContextBase` receives an `IServiceProvider?` in its constructor. The `DispatchEntityListenersAsync` method:

1. Iterates changed entities from `ChangeTracker`.
2. For each entity, resolves `IEntityListener<T>` (using `MakeGenericType`) from DI.
3. Invokes the appropriate lifecycle method on all registered listeners.
4. Also invokes `IGlobalEntityListener` for every entity if any are registered.

`OnLoadedAsync` is dispatched via an `IMaterializationInterceptor` registered in `OnModelCreating`.

---

## 13. Diagnostic System

The SG emits compile-time diagnostics (errors, warnings, info) for invalid or suspicious attribute usage. Diagnostics are organized by category with ID ranges:

### 13.1 Category Ranges

| ID Range | Category | Count |
|----------|----------|-------|
| `EDSL0001`--`EDSL0027` | Single-entity attribute validation | 27 |
| `EDSL0100`--`EDSL0109` | Cross-entity relationship validation | 10 |
| `EDSL0110`--`EDSL0117` | Schema integrity | 8 |
| `EDSL0120`--`EDSL0125`, `EDSL0250`--`EDSL0252` | Inheritance validation | 9 |
| `EDSL0130`--`EDSL0137` | Type/value validation | 8 |
| `EDSL0140`--`EDSL0147` | Behavior validation | 8 |
| `EDSL0150`--`EDSL0153` | Ownership validation | 4 |
| `EDSL0160`--`EDSL0163` | DbContext / bounded context validation | 4 |
| `EDSL0170`--`EDSL0172` | Stored procedure / view validation | 3 |
| `EDSL0182`--`EDSL0183` | DDD structural (info/warning) | 2 |
| `EDSL0200`--`EDSL0207`, `EDSL0230`--`EDSL0233` | Cycle detection and graph analysis | 12 |
| `EDSL0210`--`EDSL0214` | DDD structural violations | 5 |
| `EDSL0220`--`EDSL0224` | Owned type graph rules | 5 |
| `EDSL0240`--`EDSL0244` | Association class structural rules | 5 |

### 13.2 Selected Diagnostics by Severity

**Errors (prevent compilation):**

| ID | Condition |
|----|-----------|
| `EDSL0001` | `[PrimaryKey]` on a navigation property |
| `EDSL0002` | `[HasMany]` on a non-collection property |
| `EDSL0005` | `[DbContext]` on a non-partial class |
| `EDSL0006` | `[View]` and `[Table]` on the same class |
| `EDSL0010` | Ambiguous relationship principal |
| `EDSL0013` | `[OwnedEntity]` + `[Aggregation]` on same property |
| `EDSL0015` | `[Keyless]` + `[PrimaryKey]` on same class |
| `EDSL0100` | FK property type mismatch (e.g., `OrderId` is `int` but `Order.Id` is `Guid`) |
| `EDSL0107` | Required relationship cycle (can't INSERT either entity) |
| `EDSL0108` | Composition cycle (infinite cascade delete) |
| `EDSL0200` | Composition cycle (graph analysis) |
| `EDSL0210` | `[Composition]` from entity to another `[AggregateRoot]` |

**Warnings (compile but suspicious):**

| ID | Condition |
|----|-----------|
| `EDSL0007` | Entity has no `[PrimaryKey]` and no `[EntityId]` |
| `EDSL0011` | `[ComplexType]` on nullable property |
| `EDSL0014` | `[Composition]`/`[Aggregation]` without relationship attribute |
| `EDSL0106` | Orphan FK property (e.g., `CustomerId` exists but no relationship references it) |
| `EDSL0113` | Composite key order gap |
| `EDSL0143` | `[SoftDeletable]` child is target of `[Composition]` from non-SoftDeletable parent |
| `EDSL0212` | `[Entity]` unreachable from any `[AggregateRoot]` |
| `EDSL0233` | `[Aggregation]` with explicit `OnDelete="Cascade"` |

**Info:**

| ID | Condition |
|----|-----------|
| `EDSL0162` | Entity matches multiple `[DbContext]` definitions |
| `EDSL0182` | No `[ConcurrencyToken]`/`[Versionable]` on any aggregate root |

### 13.3 Bidirectional Relationship Validation

The SG uses a "principal wins" rule: configuration is emitted only on the principal side. The dependent side's attributes are used for cross-validation (FK name match, etc.). Principal determination:

1. `[HasOne(WithMany=...)]` --> declaring class is dependent, target is principal.
2. `[HasMany(WithOne=...)]` --> declaring class is principal.
3. Both sides have attributes --> side with `HasMany` wins.
4. Ambiguous --> `EDSL0010` error.
