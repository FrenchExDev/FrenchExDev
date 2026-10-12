# Entity.Dsl -- Developer Guide (HOW-TO)

This guide covers every feature of Entity.Dsl with practical, compilable examples. Entity.Dsl is an attribute-based DSL that decorates POCO classes to define EF Core entities, relationships, and lifecycle semantics. A source generator reads the attributes and emits production-ready EF Core configuration, repositories, and unit-of-work classes.

**Design principle**: You describe *what* your domain model is; the generator produces *how* EF Core configures it.

---

## Table of Contents

1. [Getting Started](#1-getting-started)
2. [Defining Entities](#2-defining-entities)
3. [Primary Keys](#3-primary-keys)
4. [Property Configuration](#4-property-configuration)
5. [Relationships](#5-relationships)
6. [Association Classes](#6-association-classes)
7. [Lifecycle Convention](#7-lifecycle-convention)
8. [Inheritance](#8-inheritance)
9. [Behaviors (Doctrine-Style)](#9-behaviors-doctrine-style)
10. [Overriding Generated Code](#10-overriding-generated-code)
11. [Entity Listeners](#11-entity-listeners)
12. [DbContext Configuration](#12-dbcontext-configuration)
13. [Views and Keyless Entities](#13-views-and-keyless-entities)
14. [Advanced Features](#14-advanced-features)
15. [Validation](#15-validation)
16. [Multi-DbContext](#16-multi-dbcontext)
17. [DI Registration](#17-di-registration)
18. [DSL-to-DSL](#18-dsl-to-dsl)

---

## 1. Getting Started

### NuGet packages

Add the following package references to your project. Entity.Dsl uses Central Package Management, so versions go in `Directory.Packages.props`.

```xml
<!-- In your .csproj -->
<ItemGroup>
  <!-- Runtime abstractions (IRepository, IUnitOfWork, IEntityListener, etc.) -->
  <PackageReference Include="FrenchExDev.Net.Entity.Dsl.Abstractions" />

  <!-- Attribute definitions ([Table], [PrimaryKey], [Column], etc.) -->
  <PackageReference Include="FrenchExDev.Net.Entity.Dsl.Attributes" />

  <!-- Source generator (analyzer) -->
  <PackageReference Include="FrenchExDev.Net.Entity.Dsl.SourceGenerator"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />

  <!-- DDD attributes ([Entity], [AggregateRoot], [Composition], etc.) -->
  <PackageReference Include="FrenchExDev.Net.Ddd.Attributes" />

  <!-- Injectable for auto DI registration -->
  <PackageReference Include="FrenchExDev.Net.Injectable.Attributes" />
</ItemGroup>
```

### Minimal Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register the generated DbContext (extension method emitted by SG)
services.AddAppDbContext(o => o.UseSqlite("Data Source=app.db"));

// Register all [Injectable]-decorated services (repos, UoW, listeners)
services.AddMyAppInjectables();

var provider = services.BuildServiceProvider();

await using var uow = provider.GetRequiredService<IAppDbContextUnitOfWork>();
uow.Orders.Add(new Order { OrderNumber = "ORD-001", Total = 99.99m });
await uow.SaveChangesAsync();
```

### First entity and DbContext

```csharp
using FrenchExDev.Net.Ddd.Attributes;
using FrenchExDev.Net.Entity.Dsl.Attributes;

// 1. Define the entity
[AggregateRoot("Order")]
[Table("Orders")]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string OrderNumber { get; set; } = "";

    public decimal Total { get; set; }
}

// 2. Define the DbContext
[DbContext]
public partial class AppDbContext : DbContext { }
```

The source generator emits the following files:

- `OrderConfigurationBase.g.cs` -- abstract base with virtual Configure* methods
- `OrderConfiguration.g.cs` -- partial stub (you extend this)
- `OrderConfigurationRegistration.g.cs` -- `IEntityTypeConfiguration<Order>` delegate
- `IOrderRepository.g.cs` -- typed repository interface
- `OrderRepositoryBase.g.cs` -- base implementation
- `OrderRepository.g.cs` -- partial stub with `[Injectable]`
- `AppDbContextBase.g.cs` -- abstract DbContext with DbSets, hooks
- `AppDbContext.g.cs` -- partial stub
- `IAppDbContextUnitOfWork.g.cs` -- UoW interface
- `AppDbContextUnitOfWorkBase.g.cs` -- UoW base
- `AppDbContextUnitOfWork.g.cs` -- UoW partial stub with `[Injectable]`
- `AppDbContextRegistration.g.cs` -- `AddAppDbContext` extension method

---

## 2. Defining Entities

Entities are defined using DDD attributes from `FrenchExDev.Net.Ddd.Attributes` combined with persistence-specific attributes from `FrenchExDev.Net.Entity.Dsl.Attributes`.

### With `[Entity]`

```csharp
[Entity("OrderItem")]
[Table("OrderItems")]
public partial class OrderItem
{
    [PrimaryKey]
    public int Id { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
```

### With `[AggregateRoot]`

Aggregate roots are entities that form the root of a DDD aggregate. They get their own repository and participate in bounded context scoping.

```csharp
[AggregateRoot("Customer")]
[Table("Customers")]
public partial class Customer
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";
}
```

### With table name override

By default, the table name is inferred from the class name. Use `[Table]` to override.

```csharp
// Default table name: uses class name
[Entity("Product")]
public partial class Product { /* ... */ }

// Explicit table name
[Entity("Product")]
[Table("Products")]
public partial class Product { /* ... */ }

// With schema
[Entity("Product")]
[Table("Products", Schema = "catalog")]
public partial class Product { /* ... */ }
```

**Generated output** (with schema):

```csharp
// In ProductConfigurationBase.g.cs
protected virtual void ConfigureTable(
    EntityTypeBuilder<Product> builder)
{
    builder.ToTable("Products", "catalog");
}
```

**Note**: `[Table]` requires `[Entity]` or `[AggregateRoot]` on the same class. The SG enforces this via a `MetaConstraint` and emits a compiler error if violated.

---

## 3. Primary Keys

### Single primary key

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }
}
```

**Generated output:**

```csharp
protected virtual void ConfigurePrimaryKey(EntityTypeBuilder<Order> builder)
{
    builder.HasKey(e => e.Id);
    builder.Property(e => e.Id).ValueGeneratedOnAdd();
}
```

### Composite primary key

Use the `Order` property (not to be confused with column order) to control the key column ordering.

```csharp
[Entity("Enrollment")]
[Table("Enrollments")]
public partial class Enrollment
{
    [PrimaryKey(Order = 0)]
    public int StudentId { get; set; }

    [PrimaryKey(Order = 1)]
    public int CourseId { get; set; }

    public string? Grade { get; set; }
}
```

**Generated output:**

```csharp
protected virtual void ConfigurePrimaryKey(EntityTypeBuilder<Enrollment> builder)
{
    builder.HasKey(e => new { e.StudentId, e.CourseId });
}
```

### ValueGeneration strategies

The `ValueGenerated` property controls how the database generates the key value.

```csharp
// Auto-generate on insert (default)
[PrimaryKey(ValueGenerated = ValueGeneration.OnAdd)]
public Guid Id { get; set; }

// Never auto-generate (caller must supply the value)
[PrimaryKey(ValueGenerated = ValueGeneration.None)]
public string Code { get; set; } = "";

// Database generates on update (rare)
[PrimaryKey(ValueGenerated = ValueGeneration.OnUpdate)]
public int Version { get; set; }

// Database generates on both add and update
[PrimaryKey(ValueGenerated = ValueGeneration.OnAddOrUpdate)]
public long RowVersion { get; set; }
```

**Generated output for each strategy:**

| Strategy | EF Core Call |
|---|---|
| `OnAdd` | `builder.Property(e => e.Id).ValueGeneratedOnAdd();` |
| `None` | `builder.Property(e => e.Id).ValueGeneratedNever();` |
| `OnUpdate` | `builder.Property(e => e.Id).ValueGeneratedOnUpdate();` |
| `OnAddOrUpdate` | `builder.Property(e => e.Id).ValueGeneratedOnAddOrUpdate();` |

---

## 4. Property Configuration

All property attributes are standalone -- they do not depend on DDD `[Property]`.

### `[Column]` -- name, type, order

```csharp
[AggregateRoot("Product")]
[Table("Products")]
public partial class Product
{
    [PrimaryKey]
    public int Id { get; set; }

    [Column(Name = "product_name", TypeName = "varchar(200)", Order = 1)]
    public string Name { get; set; } = "";

    [Column(Name = "unit_price", Order = 2)]
    public decimal Price { get; set; }
}
```

**Generated output:**

```csharp
protected virtual void ConfigureName(EntityTypeBuilder<Product> builder)
{
    builder.Property(e => e.Name)
        .HasColumnName("product_name")
        .HasColumnType("varchar(200)")
        .HasColumnOrder(1);
}

protected virtual void ConfigurePrice(EntityTypeBuilder<Product> builder)
{
    builder.Property(e => e.Price)
        .HasColumnName("unit_price")
        .HasColumnOrder(2);
}
```

### `[Required]`

```csharp
[Required]
public string Name { get; set; } = "";
```

**Generated:** `.IsRequired()`

### `[MaxLength]`

```csharp
[MaxLength(100)]
public string Description { get; set; } = "";
```

**Generated:** `.HasMaxLength(100)`

### `[Precision]`

```csharp
[Precision(18, 2)]
public decimal Price { get; set; }
```

**Generated:** `.HasPrecision(18, 2)`

### `[DefaultValue]`

Use either `Value` (CLR default) or `Sql` (database SQL expression), but not both.

```csharp
// CLR default value
[DefaultValue(Value = "0")]
public decimal Balance { get; set; }

// SQL expression default
[DefaultValue(Sql = "GETUTCDATE()")]
public DateTimeOffset CreatedAt { get; set; }
```

**Generated:**

```csharp
builder.Property(e => e.Balance).HasDefaultValue(0m);
builder.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
```

**Conflict**: Setting both `Value` and `Sql` triggers diagnostic `EDSL0004`.

### `[ComputedColumn]`

```csharp
[ComputedColumn("[Quantity] * [UnitPrice]", Stored = true)]
public decimal LineTotal { get; set; }
```

**Generated:**

```csharp
builder.Property(e => e.LineTotal)
    .HasComputedColumnSql("[Quantity] * [UnitPrice]", stored: true);
```

### `[EnumStorage]`

```csharp
public enum OrderStatus { Pending, Confirmed, Shipped, Cancelled }

[EnumStorage(AsString = true)]    // store as "Pending", "Confirmed", etc.
public OrderStatus Status { get; set; }

[EnumStorage(AsString = false)]   // store as 0, 1, 2, 3
public OrderStatus StatusInt { get; set; }
```

**Generated (AsString = true):**

```csharp
builder.Property(e => e.Status).HasConversion<string>();
```

### `[NotMapped]`

Excludes a property from EF Core mapping entirely.

```csharp
[NotMapped]
public string FullName => $"{FirstName} {LastName}";
```

**Generated:** The property is skipped -- no `Configure{PropertyName}` method is emitted.

**Conflict**: `[NotMapped]` combined with `[PrimaryKey]` triggers diagnostic `EDSL0027`.

### `[Comment]`

Adds database documentation to a table or column.

```csharp
// Class-level: table comment
[Entity("Order")]
[Table("Orders")]
[Comment("Contains all customer orders")]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }

    // Property-level: column comment
    [Comment("ISO 4217 currency code")]
    public string Currency { get; set; } = "USD";
}
```

**Generated:**

```csharp
builder.HasComment("Contains all customer orders");
builder.Property(e => e.Currency).HasComment("ISO 4217 currency code");
```

### `[BackingField]`

For DDD-style encapsulated properties with private setters.

```csharp
[BackingField("_email", AccessMode = "Field")]
public string Email { get; private set; } = "";
```

**Generated:**

```csharp
builder.Property(e => e.Email)
    .HasField("_email")
    .UsePropertyAccessMode(PropertyAccessMode.Field);
```

Access modes: `"Field"`, `"Property"`, `"PreferField"`, `"PreferFieldDuringConstruction"`.

---

## 5. Relationships

### Convention-based inference

Entity.Dsl can infer relationships from property types without explicit attributes:

| Property Type | Inference |
|---|---|
| `T` where T has `[Entity]` | `HasOne<T>` (reference navigation) |
| `ICollection<T>` / `List<T>` where T has `[Entity]` | `HasMany<T>` (collection navigation) |
| `T?` where T has `[Entity]` | `HasOne<T>` optional (`IsRequired = false`) |

Attributes are for overrides when the convention does not give you what you want.

### One-to-Many with `[Composition]`

Composition means the parent **owns** the child lifecycle. Default delete behavior: `Cascade`.

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Composition]
    [HasMany(WithOne = "Order", ForeignKey = "OrderId")]
    public List<OrderItem> Items { get; set; } = new();
}

[Entity("OrderItem")]
[Table("OrderItems")]
public partial class OrderItem
{
    [PrimaryKey]
    public int Id { get; set; }

    public Guid OrderId { get; set; }

    [HasOne(WithMany = "Items")]
    public Order Order { get; set; } = null!;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
```

**Generated output:**

```csharp
// In OrderConfigurationBase.g.cs
protected virtual void ConfigureItems(EntityTypeBuilder<Order> builder)
{
    builder.HasMany(e => e.Items)
        .WithOne(e => e.Order)
        .HasForeignKey(e => e.OrderId)
        .OnDelete(DeleteBehavior.Cascade);
}
```

### One-to-Many with `[Aggregation]`

Aggregation means the child has a **shared reference** to the parent. Default delete behavior: `Restrict`.

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    [Aggregation]
    [HasOne(WithMany = "Orders", ForeignKey = "CustomerId")]
    public Customer Customer { get; set; } = null!;
}
```

**Generated output:**

```csharp
protected virtual void ConfigureCustomer(EntityTypeBuilder<Order> builder)
{
    builder.HasOne(e => e.Customer)
        .WithMany(e => e.Orders)
        .HasForeignKey(e => e.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);
}
```

### One-to-One

```csharp
[AggregateRoot("User")]
[Table("Users")]
public partial class User
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Composition]
    [HasOne(ForeignKey = "UserId")]
    public UserProfile? Profile { get; set; }
}

[Entity("UserProfile")]
[Table("UserProfiles")]
public partial class UserProfile
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [HasOne]
    public User User { get; set; } = null!;
}
```

**Generated output:**

```csharp
protected virtual void ConfigureProfile(EntityTypeBuilder<User> builder)
{
    builder.HasOne(e => e.Profile)
        .WithOne(e => e.User)
        .HasForeignKey<UserProfile>(e => e.UserId)
        .OnDelete(DeleteBehavior.Cascade);
}
```

### Many-to-Many (without join entity)

```csharp
[AggregateRoot("Article")]
[Table("Articles")]
public partial class Article
{
    [PrimaryKey]
    public int Id { get; set; }

    [ManyToMany(JoinTable = "ArticleTags")]
    public List<Tag> Tags { get; set; } = new();
}

[Entity("Tag")]
[Table("Tags")]
public partial class Tag
{
    [PrimaryKey]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = "";
}
```

**Generated output:**

```csharp
protected virtual void ConfigureTags(EntityTypeBuilder<Article> builder)
{
    builder.HasMany(e => e.Tags)
        .WithMany()
        .UsingEntity(j => j.ToTable("ArticleTags"));
}
```

### Many-to-Many (with join entity)

When the join table has payload, use `JoinEntity`:

```csharp
[ManyToMany(JoinEntity = typeof(OrderTag))]
public List<Tag> Tags { get; set; } = new();
```

**Generated output:**

```csharp
builder.HasMany(e => e.Tags)
    .WithMany()
    .UsingEntity<global::MyApp.Domain.OrderTag>();
```

### Self-referencing with `[SelfReference]`

For tree/hierarchy structures.

```csharp
[Entity("Category")]
[Table("Categories")]
public partial class Category
{
    [PrimaryKey]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";

    public int? ParentId { get; set; }

    [SelfReference(InverseNavigation = "Children", ForeignKey = "ParentId", OnDelete = "Restrict")]
    public Category? Parent { get; set; }

    public List<Category> Children { get; set; } = new();
}
```

**Generated output:**

```csharp
protected virtual void ConfigureParent(EntityTypeBuilder<Category> builder)
{
    builder.HasOne(e => e.Parent)
        .WithMany(e => e.Children)
        .HasForeignKey(e => e.ParentId)
        .OnDelete(DeleteBehavior.Restrict);
}
```

### Owned types with `[OwnedEntity]` and `[Owned]`

#### Property-level: `[OwnedEntity]`

Explicit opt-in for a specific navigation.

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [OwnedEntity]
    public Address ShippingAddress { get; set; } = null!;

    [OwnedEntity(TableName = "BillingAddresses")]
    public Address BillingAddress { get; set; } = null!;

    [OwnedEntity(JsonColumn = "metadata")]
    public OrderMetadata Metadata { get; set; } = null!;
}
```

**Generated output:**

```csharp
builder.OwnsOne(e => e.ShippingAddress);                          // same table, flattened
builder.OwnsOne(e => e.BillingAddress, b => b.ToTable("BillingAddresses")); // separate table
builder.OwnsOne(e => e.Metadata, b => b.ToJson("metadata"));     // JSON column
```

#### Class-level: `[Owned]`

When a type is *always* owned, apply `[Owned]` to the class itself. Any navigation to this type auto-generates `OwnsOne`/`OwnsMany`.

```csharp
[Owned]
public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string ZipCode { get; set; } = "";
}
```

No need to put `[OwnedEntity]` on each navigation -- the SG infers it.

### Complex types with `[ComplexType]` / `[ValueObject]`

For value objects with no identity, no nullability, and no navigations.

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
    [PrimaryKey]
    public int Id { get; set; }

    [ComplexType]
    public Money Total { get; set; } = new();
}
```

**Generated output:**

```csharp
builder.ComplexProperty(e => e.Total);
```

**`[OwnedEntity]` vs `[ComplexType]` summary:**

| | OwnedEntity | ComplexType |
|---|---|---|
| Has identity | Yes (via owner's key) | No |
| Can be null | Yes | No |
| Can have navigations | Yes | No |
| Table mapping | Same table or separate | Same table only |
| DDD mapping | Composition | ValueObject |

---

## 6. Association Classes

An association class materializes a many-to-many relationship and carries payload. It is a first-class DSL concept.

### Full example: Student / Course / Enrollment

```csharp
[AssociationClass("Enrollment", typeof(Student), typeof(Course),
    OnDeleteLeft = "Cascade", OnDeleteRight = "Cascade")]
[Table("Enrollments")]
public partial class Enrollment
{
    // FK properties (auto-detected by convention: {EndpointName}Id)
    public int StudentId { get; set; }
    public int CourseId { get; set; }

    // Navigations (auto-generated if missing)
    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;

    // Payload -- this is what makes it an association CLASS
    public string? Grade { get; set; }

    [DefaultValue(Sql = "GETUTCDATE()")]
    public DateTimeOffset EnrolledAt { get; set; }
}

[AggregateRoot("Student")]
[Table("Students")]
public partial class Student
{
    [PrimaryKey]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";
    // SG generates: public ICollection<Course> Courses { get; set; }     (skip nav)
    // SG generates: public ICollection<Enrollment> Enrollments { get; set; } (direct nav)
}

[AggregateRoot("Course")]
[Table("Courses")]
public partial class Course
{
    [PrimaryKey]
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = "";
    // SG generates: public ICollection<Student> Students { get; set; }   (skip nav)
    // SG generates: public ICollection<Enrollment> Enrollments { get; set; } (direct nav)
}
```

### Generated output

**`Student.AssociationNavigations.g.cs`** (auto-generated partial):

```csharp
// <auto-generated/>
public partial class Student
{
    public ICollection<global::MyApp.Domain.Course> Courses { get; set; }
        = new List<global::MyApp.Domain.Course>();
    public ICollection<global::MyApp.Domain.Enrollment> Enrollments { get; set; }
        = new List<global::MyApp.Domain.Enrollment>();
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

**`StudentConfigurationBase.g.cs`** (skip navigation):

```csharp
protected virtual void ConfigureCourses(EntityTypeBuilder<Student> builder)
{
    builder.HasMany(e => e.Courses)
        .WithMany(e => e.Students)
        .UsingEntity<global::MyApp.Domain.Enrollment>();
}
```

### What `[AssociationClass]` auto-generates

1. **Composite PK** from `{Left}Id` + `{Right}Id` (unless developer declares explicit `[PrimaryKey]`)
2. **FK navigation properties** to both endpoints (if not already declared)
3. **Skip navigations** on both endpoint entities (injected via partial classes)
4. **`IEntityTypeConfiguration<T>`** with full relationship wiring
5. **`IAssociationRepository<TAssoc, TLeft, TRight>`** with specialized queries

### Specialized repository

Association classes get `IAssociationRepository<,,>` instead of plain `IRepository<T>`:

```csharp
// Generated interface extends IAssociationRepository
public interface IEnrollmentRepository
    : IAssociationRepository<Enrollment, Student, Course> { }

// Usage
var enrollment = await uow.Enrollments.FindByEndpointsAsync(studentId: 1, courseId: 42);
var courses = await uow.Enrollments.FindRightsByLeftAsync(leftKey: 1);  // courses for student 1
var students = await uow.Enrollments.FindLeftsByRightAsync(rightKey: 42); // students in course 42
```

---

## 7. Lifecycle Convention

When no explicit `OnDelete` is set on a relationship attribute, the SG infers the delete behavior from DDD attributes on the same property.

| DDD Attribute | Default DeleteBehavior | Rationale |
|---|---|---|
| `[Composition]` | `Cascade` | Parent owns child lifecycle completely |
| `[Aggregation]` | `Restrict` | Shared reference -- prevent accidental orphan |
| `[Association]` | `NoAction` | Independent lifecycle -- no cascade |
| *(none)* | Infer from nullability | Nullable nav -> `ClientSetNull`, non-nullable -> `Cascade` (EF Core default) |

### Overriding with explicit OnDelete

You can override the convention by specifying `OnDelete` directly on the relationship attribute:

```csharp
// Composition normally cascades, but here we restrict instead
[Composition]
[HasMany(WithOne = "Order", ForeignKey = "OrderId", OnDelete = "Restrict")]
public List<OrderItem> Items { get; set; } = new();

// Aggregation normally restricts, but here we set null instead
[Aggregation]
[HasOne(WithMany = "Orders", ForeignKey = "CustomerId", OnDelete = "SetNull")]
public Customer? Customer { get; set; }
```

Valid `OnDelete` values: `"Cascade"`, `"Restrict"`, `"SetNull"`, `"NoAction"`, `"ClientCascade"`, `"ClientSetNull"`.

---

## 8. Inheritance

### TPH (Table Per Hierarchy)

All types in the hierarchy are stored in a single table with a discriminator column.

```csharp
[Entity("Payment")]
[Table("Payments")]
[Inheritance(Strategy = InheritanceStrategy.TPH, DiscriminatorColumn = "PaymentType")]
public class Payment
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Required]
    [Precision(18, 2)]
    public decimal Amount { get; set; }
}

[Entity("CreditCardPayment")]
[Inheritance(Strategy = InheritanceStrategy.TPH, DiscriminatorValue = "CreditCard")]
public class CreditCardPayment : Payment
{
    [MaxLength(4)]
    public string CardLastFour { get; set; } = "";
}

[Entity("BankTransferPayment")]
[Inheritance(Strategy = InheritanceStrategy.TPH, DiscriminatorValue = "BankTransfer")]
public class BankTransferPayment : Payment
{
    [MaxLength(34)]
    public string IBAN { get; set; } = "";
}
```

**Generated output on base type configuration:**

```csharp
builder.HasDiscriminator<string>("PaymentType")
    .HasValue<global::MyApp.Domain.Payment>("Payment")
    .HasValue<global::MyApp.Domain.CreditCardPayment>("CreditCard")
    .HasValue<global::MyApp.Domain.BankTransferPayment>("BankTransfer");
```

### TPT (Table Per Type)

Each type in the hierarchy gets its own table.

```csharp
[Entity("Vehicle")]
[Table("Vehicles")]
[Inheritance(Strategy = InheritanceStrategy.TPT)]
public class Vehicle
{
    [PrimaryKey]
    public int Id { get; set; }

    [Required]
    public string Make { get; set; } = "";
}

[Entity("Car")]
[Table("Cars")]
[Inheritance(Strategy = InheritanceStrategy.TPT)]
public class Car : Vehicle
{
    public int Doors { get; set; }
}

[Entity("Truck")]
[Table("Trucks")]
[Inheritance(Strategy = InheritanceStrategy.TPT)]
public class Truck : Vehicle
{
    public double PayloadTons { get; set; }
}
```

**Generated output:**

```csharp
// On base config
builder.UseTptMappingStrategy();

// On Car config
builder.ToTable("Cars");

// On Truck config
builder.ToTable("Trucks");
```

### TPC (Table Per Concrete type)

Each concrete type gets its own table. The base class must be abstract.

```csharp
[Entity("Shape")]
[Inheritance(Strategy = InheritanceStrategy.TPC)]
public abstract class Shape
{
    [PrimaryKey]
    public int Id { get; set; }
    public string Color { get; set; } = "";
}

[Entity("Circle")]
[Table("Circles")]
[Inheritance(Strategy = InheritanceStrategy.TPC)]
public class Circle : Shape
{
    public double Radius { get; set; }
}

[Entity("Rectangle")]
[Table("Rectangles")]
[Inheritance(Strategy = InheritanceStrategy.TPC)]
public class Rectangle : Shape
{
    public double Width { get; set; }
    public double Height { get; set; }
}
```

**Generated output:**

```csharp
builder.UseTpcMappingStrategy();
```

---

## 9. Behaviors (Doctrine-Style)

Behaviors are opt-in via class-level attributes. The SG generates properties via partial classes, EF Core configuration, and SaveChanges hooks -- zero boilerplate for the developer.

### `[Timestampable]`

Automatically tracks creation and modification timestamps.

```csharp
[AggregateRoot("Article")]
[Table("Articles")]
[Timestampable]
public partial class Article
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Required]
    public string Title { get; set; } = "";
}
```

**Generated partial class** (`Article.Behaviors.g.cs`):

```csharp
public partial class Article
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

**Generated configuration:**

```csharp
protected virtual void ConfigureTimestampable(EntityTypeBuilder<Article> builder)
{
    builder.Property(e => e.CreatedAt).IsRequired();
    builder.Property(e => e.UpdatedAt).IsRequired();
}
```

**Generated SaveChanges hook** (in DbContextBase):

```csharp
if (entry.State == EntityState.Added)
    entry.Property("CreatedAt").CurrentValue = now;
entry.Property("UpdatedAt").CurrentValue = now;
```

**Configuration options:**

```csharp
[Timestampable(
    CreatedAtName = "DateCreated",        // custom property name (default: "CreatedAt")
    UpdatedAtName = "DateModified",       // custom property name (default: "UpdatedAt")
    Type = "DateTime",                    // "DateTimeOffset" (default), "DateTime", "long" (unix ticks)
    Precision = 3,                        // fractional seconds: 0-7 (default: 7 = 100ns)
    TimeZone = "Utc",                     // "Utc" (default), "Local", "Unspecified"
    CreatedAtImmutable = true,            // never overwrite after first set (default: true)
    UpdateOnChildChange = false,          // update parent when [Composition] children change (default: false)
    CreatedAtColumnName = "created_at",   // explicit column name (default: null = use NamingConvention)
    UpdatedAtColumnName = "updated_at"
)]
```

### `[SoftDeletable]`

Intercepts `Remove()` calls and sets a flag instead of deleting. Adds a global query filter.

```csharp
[AggregateRoot("Product")]
[Table("Products")]
[SoftDeletable]
public partial class Product
{
    [PrimaryKey]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";
}
```

**Generated partial class:**

```csharp
public partial class Product
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

**Generated configuration:**

```csharp
protected virtual void ConfigureSoftDeletable(EntityTypeBuilder<Product> builder)
{
    builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
    builder.Property(e => e.DeletedAt);
    builder.HasQueryFilter(e => !e.IsDeleted);
}
```

**Generated SaveChanges hook:**

```csharp
if (entry.State == EntityState.Deleted
    && entry.Metadata.FindProperty("IsDeleted") != null)
{
    entry.State = EntityState.Modified;
    entry.Property("IsDeleted").CurrentValue = true;
    entry.Property("DeletedAt").CurrentValue = now;
}
```

**Configuration options:**

```csharp
[SoftDeletable(
    IsDeletedName = "Archived",           // custom property name (default: "IsDeleted")
    DeletedAtName = "ArchivedAt",         // custom property name (default: "DeletedAt")
    CascadeToChildren = true,             // soft-delete [Composition] children too (default: true)
    AllowHardDelete = false,              // generates HardDelete() on repository (default: false)
    FilterEnabled = true,                 // auto-add HasQueryFilter (default: true)
    AllowRestore = true                   // generates Restore() method (default: true)
)]
```

### `[Blameable]`

Tracks which user created/modified an entity. Requires an `ICurrentUserProvider` implementation.

```csharp
[AggregateRoot("Document")]
[Table("Documents")]
[Blameable]
public partial class Document
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Required]
    public string Title { get; set; } = "";
}
```

**Generated partial class:**

```csharp
public partial class Document
{
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
```

**ICurrentUserProvider setup:**

```csharp
using FrenchExDev.Net.Entity.Dsl.Abstractions;
using FrenchExDev.Net.Injectable.Attributes;

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

**Configuration options:**

```csharp
[Blameable(
    CreatedByName = "Author",             // default: "CreatedBy"
    UpdatedByName = "Editor",             // default: "UpdatedBy"
    TrackDeletedBy = true,                // generates DeletedBy property (default: false)
    DeletedByName = "DeletedBy",          // default: "DeletedBy"
    IdentifierType = "Guid",              // "string" (default), "Guid", "int", "long"
    MaxLength = 256,                      // for string identifiers (default: 256)
    Required = false                      // false = nullable (anonymous actions allowed)
)]
```

### `[Versionable]`

Adds optimistic concurrency control.

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
[Versionable]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }
}
```

**Generated partial class:**

```csharp
public partial class Order
{
    public byte[] RowVersion { get; set; } = null!;
}
```

**Generated configuration:**

```csharp
protected virtual void ConfigureVersionable(EntityTypeBuilder<Order> builder)
{
    builder.Property(e => e.RowVersion).IsRowVersion();
}
```

**Four strategies:**

| Strategy | CLR Type | EF Core Config | Managed By |
|---|---|---|---|
| `RowVersion` (default) | `byte[]` | `IsRowVersion()` | Database |
| `Guid` | `Guid` | `IsConcurrencyToken()` | App (`Guid.NewGuid()` on save) |
| `Timestamp` | `DateTimeOffset` | `IsConcurrencyToken()` | App (`UtcNow` on save) |
| `Increment` | `long` | `IsConcurrencyToken()` | App (+1 on save) |

```csharp
[Versionable(Strategy = "Guid", PropertyName = "ConcurrencyStamp")]
```

### `[Sluggable]`

Auto-generates URL-friendly slugs from a source property.

```csharp
[AggregateRoot("BlogPost")]
[Table("BlogPosts")]
[Sluggable(nameof(Title))]
public partial class BlogPost
{
    [PrimaryKey]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";
    // SG generates: public string Slug { get; set; } = "";
}
```

**Generated partial class:**

```csharp
public partial class BlogPost
{
    public string Slug { get; set; } = "";
}
```

**Generated configuration:**

```csharp
protected virtual void ConfigureSluggable(EntityTypeBuilder<BlogPost> builder)
{
    builder.Property(e => e.Slug).IsRequired().HasMaxLength(256);
    builder.HasIndex(e => e.Slug).IsUnique();
}
```

**Configuration options:**

```csharp
[Sluggable("Title",
    Separator = "-",                      // word separator (default: "-")
    PropertyName = "UrlSlug",             // output property name (default: "Slug")
    MaxLength = 128,                      // max slug length (default: 256)
    Unique = true,                        // unique index on slug (default: true)
    UniqueScope = "TenantId",             // scope uniqueness per tenant (default: null = global)
    DuplicateSuffix = "-{n}",             // collision suffix pattern (default: "-{n}")
    Regenerate = "OnCreate",              // "OnCreate" = never changes, "Always" = re-slug on source change
    Transliterate = true,                 // e->e, u->u, n->n (default: true)
    Lowercase = true                      // force lowercase (default: true)
)]
```

Multiple source properties (concatenated):

```csharp
[Sluggable("FirstName,LastName")]
```

### `[Loggable]`

Creates an audit trail by generating a companion `{Entity}AuditLog` entity.

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
[Loggable]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Required]
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
}
```

**Generated companion entity** (`OrderAuditLog.g.cs`):

```csharp
public class OrderAuditLog
{
    public long Id { get; set; }
    public Guid EntityId { get; set; }           // FK to Order
    public string Operation { get; set; } = "";   // "Insert", "Update", "Delete"
    public string? OldValues { get; set; }        // JSON
    public string? NewValues { get; set; }        // JSON
    public string? ChangedProperties { get; set; }
    public string? UserId { get; set; }           // from ICurrentUserProvider
    public DateTimeOffset Timestamp { get; set; }
}
```

**Configuration options:**

```csharp
[Loggable(
    AuditTableName = "OrderHistory",      // default: "{Entity}AuditLogs"
    AuditSchema = "audit",                // default: same schema as entity
    TrackProperties = "Status,Total",     // null = all (default). Comma-separated
    IgnoreProperties = "RowVersion",      // comma-separated exclusions
    LogInsert = true,                     // track inserts (default: true)
    LogUpdate = true,                     // track updates (default: true)
    LogDelete = true,                     // track deletes (default: true)
    CaptureOldValues = true,              // capture before-values (default: true)
    CaptureNewValues = true,              // capture after-values (default: true)
    ValueFormat = "Json",                 // "Json" (default) or "Columns"
    TrackUser = true,                     // require ICurrentUserProvider (default: true)
    RetentionDays = 90                    // 0 = unlimited (default). >0 = generates purge helper
)]
```

### `[Translatable]`

Multi-language support. Generates a companion `{Entity}Translation` entity.

```csharp
[AggregateRoot("Article")]
[Table("Articles")]
[Translatable(DefaultLocale = "en")]
public partial class Article
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [TranslatableProperty]
    public string Title { get; set; } = "";

    [TranslatableProperty]
    public string Content { get; set; } = "";

    public string Author { get; set; } = "";  // not translatable
}
```

**Generated companion entity** (`ArticleTranslation.g.cs`):

```csharp
public class ArticleTranslation
{
    public int Id { get; set; }
    public Guid ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public string Locale { get; set; } = "";   // "en", "fr", "de"

    // One property per [TranslatableProperty]:
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}
```

**Configuration options:**

```csharp
[Translatable(
    DefaultLocale = "en",                   // fallback locale (default: "en")
    TranslationTableName = "ArticleI18n",   // default: "{Entity}Translations"
    TranslationSchema = "i18n",             // default: same schema as entity
    LocaleColumnName = "Locale",            // default: "Locale"
    LocaleMaxLength = 10,                   // default: 10 (handles "zh-Hans")
    Fallback = "DefaultLocale",             // "DefaultLocale", "Null", "Throw"
    UniquePerLocale = true                  // unique index on (EntityId, Locale) (default: true)
)]
```

### `[Sortable]`

Automatic ordering with position management.

```csharp
[Entity("TodoItem")]
[Table("TodoItems")]
[Sortable(GroupBy = "ListId")]
public partial class TodoItem
{
    [PrimaryKey]
    public int Id { get; set; }

    public int ListId { get; set; }

    [Required]
    public string Title { get; set; } = "";
    // SG generates: public int Position { get; set; }
}
```

**Generated partial class:**

```csharp
public partial class TodoItem
{
    public int Position { get; set; }
}
```

**Configuration options:**

```csharp
[Sortable(
    PropertyName = "SortOrder",           // default: "Position"
    GroupBy = "ParentId,TenantId",        // position resets per group (comma-separated)
    StartAt = 1,                          // 0-based or 1-based (default: 0)
    OnDelete = "Reorder",                 // "Reorder" (default) or "LeaveGap"
    OnInsert = "AppendLast",              // "AppendLast" (default) or "PrependFirst"
    CreateIndex = true                    // index on (GroupBy, Position) (default: true)
)]
```

### `[TreeNode]`

Hierarchical tree structure with three strategy options.

```csharp
[Entity("Department")]
[Table("Departments")]
[TreeNode(Strategy = "MaterializedPath")]
public partial class Department
{
    [PrimaryKey]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";
    // SG generates: public int? ParentId, Parent, Children, MaterializedPath, Depth
}
```

**Generated partial class (MaterializedPath):**

```csharp
public partial class Department
{
    public int? ParentId { get; set; }
    public Department? Parent { get; set; }
    public List<Department> Children { get; set; } = new();
    public string MaterializedPath { get; set; } = "";
    public int Depth { get; set; }
}
```

**Three strategies compared:**

| | AdjacencyList | MaterializedPath | ClosureTable |
|---|---|---|---|
| Generated properties | `ParentId`, `Parent`, `Children` | + `MaterializedPath`, `Depth` | + separate closure table |
| Read ancestors | N+1 queries (recursive) | Single `LIKE` query | Single JOIN |
| Read descendants | Recursive CTE | Single `LIKE` prefix query | Single JOIN |
| Move subtree | Update 1 row | Update all descendant paths | Delete + re-insert |
| Best for | Shallow trees | Read-heavy, moderate depth | Deep trees, frequent subtree queries |

**Configuration options:**

```csharp
[TreeNode(
    Strategy = "MaterializedPath",        // "MaterializedPath" (default), "AdjacencyList", "ClosureTable"
    ParentIdName = "ParentId",            // default: "ParentId"
    ParentNavigationName = "Parent",      // default: "Parent"
    ChildrenNavigationName = "Children",  // default: "Children"
    PathName = "TreePath",                // default: "MaterializedPath"
    DepthName = "Level",                  // default: "Depth"
    PathSeparator = "/",                  // default: "/"
    PathMaxLength = 1024,                 // default: 1024
    ClosureTableName = "DeptClosure",     // default: "{Entity}TreeClosure"
    MaxDepth = 10,                        // 0 = unlimited (default)
    OnDelete = "Restrict",                // "Restrict" (default), "Cascade", "SetNull"
    OrderChildrenBy = "Name",             // property name for sibling ordering
    GenerateQueryExtensions = true        // generates IsAncestorOf, GetDescendants, etc. (default: true)
)]
```

---

## 10. Overriding Generated Code

Entity.Dsl uses the **Generation Gap** pattern. Generated code is split into three layers:

1. **Base** (`*Base.g.cs`) -- always regenerated, abstract, all methods virtual
2. **Partial stub** (`*.g.cs`) -- always regenerated, empty partial class extending Base
3. **Developer file** (`*.cs`) -- your code, a second partial that merges with the stub

You never edit generated files. You create your own partial class and override specific virtual methods.

### Override a `Configure*` method on ConfigurationBase

```csharp
// File: OrderConfiguration.cs (developer-written)
namespace MyApp.Domain.Configuration;

public partial class OrderConfiguration
{
    // Override the generated Total configuration
    protected override void ConfigureTotal(
        EntityTypeBuilder<Order> builder)
    {
        base.ConfigureTotal(builder);  // keep defaults

        // Add provider-specific config
        builder.Property(e => e.Total)
            .HasComment("Order total in base currency");
    }
}
```

### Completely replace a configuration

```csharp
public partial class OrderConfiguration
{
    // Don't call base -- completely replace
    protected override void ConfigureItems(
        EntityTypeBuilder<Order> builder)
    {
        builder.HasMany(e => e.Items)
            .WithOne(e => e.Order)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.SetNull);  // different from DSL's Cascade
    }
}
```

### Skip a generated property configuration

```csharp
public partial class OrderConfiguration
{
    protected override void ConfigureTotal(
        EntityTypeBuilder<Order> builder)
    {
        // Intentionally empty -- let EF Core conventions handle it
    }
}
```

### Add a custom query to a Repository

```csharp
// File: OrderRepository.cs (developer-written)
namespace MyApp.Domain.Repositories;

public partial class OrderRepository
{
    public Task<IReadOnlyList<Order>> FindByCustomerAsync(
        Guid customerId, CancellationToken ct = default)
        => FindWhereAsync(o => o.CustomerId == customerId, ct);

    public async Task<IReadOnlyList<Order>> FindActiveAsync(CancellationToken ct = default)
        => await DbSet.Where(o => o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
}
```

### Override UnitOfWork factory methods

```csharp
// File: SalesUnitOfWork.cs (developer-written)
namespace MyApp.Infrastructure;

public partial class SalesUnitOfWork
{
    // Inject a custom repository implementation
    protected override IOrderRepository CreateOrdersRepository()
        => new AuditedOrderRepository(Context);

    // Add cross-repository business logic
    public async Task<Order> PlaceOrderAsync(Order order, CancellationToken ct = default)
    {
        Orders.Add(order);
        var customer = await Customers.FindByIdAsync(order.CustomerId);
        customer!.OrderCount++;
        Customers.Update(customer);
        await SaveChangesAsync(ct);
        return order;
    }
}
```

### Add PostConfigure hooks

```csharp
public partial class OrderConfiguration
{
    protected override void PostConfigure(
        EntityTypeBuilder<Order> builder)
    {
        // Filtered index -- not expressible via DSL attributes
        builder.HasIndex(e => e.Status)
            .HasFilter("\"Status\" != 'Cancelled'")
            .HasDatabaseName("IX_Orders_ActiveStatus");
    }
}
```

### Override SaveChanges behavior

```csharp
// File: AppDbContext.cs (developer-written)
public partial class AppDbContext
{
    protected override void OnEntitiesAdding(
        IEnumerable<EntityEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Entity is IAuditable auditable)
            {
                auditable.AuditTrail = "Created";
            }
        }
    }

    protected override void OnEntitiesDeleting(
        IEnumerable<EntityEntry> entries)
    {
        // Custom logic before any deletion
        foreach (var entry in entries)
        {
            LogDeletion(entry);
        }
    }
}
```

### Override hook summary

| Hook | Layer | When |
|---|---|---|
| `PreConfigure(builder)` | EntityConfig | Before all configuration |
| `Configure{Property}(builder)` | EntityConfig | Per property |
| `Configure{Relationship}(builder)` | EntityConfig | Per relationship |
| `ConfigureTable(builder)` | EntityConfig | Table mapping |
| `ConfigureIndexes(builder)` | EntityConfig | Index setup |
| `ConfigureShadowProperties(builder)` | EntityConfig | Shadow properties |
| `ConfigureCheckConstraints(builder)` | EntityConfig | Check constraints |
| `PostConfigure(builder)` | EntityConfig | After all configuration |
| `PreModelCreating(modelBuilder)` | DbContext | Before entity registration |
| `PostModelCreating(modelBuilder)` | DbContext | After entity registration |
| `RegisterConfigurations(modelBuilder)` | DbContext | Entity registration |
| `OnEntitiesAdding(entries)` | DbContext | Before SaveChanges (added) |
| `OnEntitiesModifying(entries)` | DbContext | Before SaveChanges (modified) |
| `OnEntitiesDeleting(entries)` | DbContext | Before SaveChanges (deleted) |

---

## 11. Entity Listeners

Entity listeners are per-entity lifecycle hooks following the Doctrine `EntityListeners` pattern. They are resolved from DI and dispatched by the generated DbContext.

### Declaring listeners on an entity

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
[EntityListener(typeof(OrderAuditListener))]
[EntityListener(typeof(OrderNotificationListener))]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Required]
    public string OrderNumber { get; set; } = "";
}
```

### Implementing a listener

All methods have default implementations (empty). Implement only the hooks you need.

```csharp
using FrenchExDev.Net.Entity.Dsl.Abstractions;
using FrenchExDev.Net.Injectable.Attributes;

[Injectable(Scope = Scope.Scoped, As = typeof(IEntityListener<Order>))]
public class OrderAuditListener : IEntityListener<Order>
{
    private readonly IAuditService _audit;

    public OrderAuditListener(IAuditService audit) => _audit = audit;

    public async Task OnAddedAsync(Order entity, CancellationToken ct)
        => await _audit.LogAsync($"Order {entity.Id} created", ct);

    public async Task OnModifyingAsync(Order entity, CancellationToken ct)
        => await _audit.LogAsync($"Order {entity.Id} updating", ct);

    public async Task OnRemovedAsync(Order entity, CancellationToken ct)
        => await _audit.LogAsync($"Order {entity.Id} deleted", ct);
}

[Injectable(Scope = Scope.Scoped, As = typeof(IEntityListener<Order>))]
public class OrderNotificationListener : IEntityListener<Order>
{
    private readonly INotificationService _notifications;

    public OrderNotificationListener(INotificationService notifications)
        => _notifications = notifications;

    public async Task OnAddedAsync(Order entity, CancellationToken ct)
        => await _notifications.SendAsync($"New order {entity.OrderNumber}", ct);
}
```

### Global entity listener

For cross-cutting concerns that apply to all entities.

```csharp
[Injectable(Scope = Scope.Scoped, As = typeof(IGlobalEntityListener))]
public class MetricsListener : IGlobalEntityListener
{
    private readonly IMetrics _metrics;

    public MetricsListener(IMetrics metrics) => _metrics = metrics;

    public Task OnAddedAsync(EntityEntry entry, CancellationToken ct)
    {
        _metrics.Increment($"entity.{entry.Metadata.ClrType.Name}.created");
        return Task.CompletedTask;
    }
}
```

### Available lifecycle hooks

| Method | Doctrine Equivalent | When |
|---|---|---|
| `OnAddingAsync` | `prePersist` | Before insert |
| `OnAddedAsync` | `postPersist` | After insert |
| `OnModifyingAsync` | `preUpdate` | Before update |
| `OnModifiedAsync` | `postUpdate` | After update |
| `OnRemovingAsync` | `preRemove` | Before delete |
| `OnRemovedAsync` | `postRemove` | After delete |
| `OnLoadedAsync` | `postLoad` | After materialization |

### Per-entity vs global

| | `IEntityListener<T>` | `IGlobalEntityListener` |
|---|---|---|
| Scope | One entity type | All entities |
| Registration | `[EntityListener(typeof(...))]` on entity | `[Injectable(As = typeof(IGlobalEntityListener))]` |
| Typing | Strongly typed `T entity` | Untyped `EntityEntry entry` |
| Use case | Entity-specific audit | Cross-cutting: logging, metrics |

---

## 12. DbContext Configuration

The `[DbContext]` attribute supports several context-level configuration properties.

```csharp
[DbContext(
    BoundedContext = "Sales",                     // scope to Sales entities only
    LazyLoading = false,                          // enable lazy loading proxies (default: false)
    QueryTracking = "NoTracking",                 // "TrackAll" (default), "NoTracking", "NoTrackingWithIdentityResolution"
    QuerySplitting = "SplitQuery",                // "SingleQuery" (default), "SplitQuery"
    ChangeTracking = "Snapshot",                  // "Snapshot" (default), "ChangingAndChangedNotifications", "ChangedNotifications"
    EnableRetryOnFailure = true,                  // enable transient failure retry (default: false)
    MaxRetryCount = 3                             // max retry count (default: 6)
)]
public partial class SalesDbContext : DbContext { }
```

**Generated output** (in `SalesDbContextBase.g.cs`):

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    base.OnConfiguring(optionsBuilder);
    optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    optionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
}

// Resiliency is provider-specific, emitted as virtual hook:
protected virtual void ConfigureResiliency(DbContextOptionsBuilder optionsBuilder) { }
```

The developer overrides `ConfigureResiliency` in their partial class:

```csharp
public partial class SalesDbContext
{
    protected override void ConfigureResiliency(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableRetryOnFailure(maxRetryCount: 3);
    }
}
```

---

## 13. Views and Keyless Entities

### `[View]` -- map to a database view

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

**Generated output:**

```csharp
builder.ToView("vw_ActiveOrders", "reports");
builder.HasNoKey();
```

Views are keyless by default. They do not get a `DbSet` property, and no repository is generated.

### `[Keyless]` -- explicit keyless entity

For raw SQL result types or custom projections.

```csharp
[Entity("OrderStatistics")]
[Keyless]
public class OrderStatistics
{
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
}
```

**Generated output:**

```csharp
builder.HasNoKey();
```

**Conflict**: `[Keyless]` combined with `[PrimaryKey]` triggers diagnostic `EDSL0015`.

---

## 14. Advanced Features

### `[StoredProcedure]`

Map CUD operations to stored procedures.

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
[StoredProcedure(
    InsertProcedure = "sp_InsertOrder",
    UpdateProcedure = "sp_UpdateOrder",
    DeleteProcedure = "sp_DeleteOrder")]
public partial class Order { /* ... */ }
```

**Generated output:**

```csharp
builder.InsertUsingStoredProcedure("sp_InsertOrder", sp => { sp.HasParameter(e => e.Id); /* ... */ });
builder.UpdateUsingStoredProcedure("sp_UpdateOrder", sp => { /* ... */ });
builder.DeleteUsingStoredProcedure("sp_DeleteOrder", sp => { /* ... */ });
```

### `[Trigger]`

Declare database triggers. Required for correct SaveChanges behavior in EF Core 7+ (OUTPUT clause vs SELECT after INSERT).

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
[Trigger("trg_Orders_Audit")]
[Trigger("trg_Orders_UpdateTimestamp")]
public partial class Order { /* ... */ }
```

**Generated output:**

```csharp
builder.ToTable(tb =>
{
    tb.HasTrigger("trg_Orders_Audit");
    tb.HasTrigger("trg_Orders_UpdateTimestamp");
});
```

### `[EntitySplit]`

Split one entity across multiple tables (for performance with wide entities).

```csharp
[AggregateRoot("Customer")]
[Table("Customers")]
[EntitySplit("CustomerContacts", "Email,Phone,Address")]
[EntitySplit("CustomerPreferences", "Theme,Language,Timezone")]
public partial class Customer
{
    [PrimaryKey]
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Theme { get; set; } = "";
    public string Language { get; set; } = "";
    public string Timezone { get; set; } = "";
}
```

**Generated output:**

```csharp
builder.SplitToTable("CustomerContacts", tb =>
{
    tb.Property(e => e.Email);
    tb.Property(e => e.Phone);
    tb.Property(e => e.Address);
});
builder.SplitToTable("CustomerPreferences", tb =>
{
    tb.Property(e => e.Theme);
    tb.Property(e => e.Language);
    tb.Property(e => e.Timezone);
});
```

### `[TableSplit]`

Map multiple entities to the same table (opposite of EntitySplit).

```csharp
[Entity("CustomerBasic")]
[TableSplit("Customers")]
public partial class CustomerBasic
{
    [PrimaryKey]
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

[Entity("CustomerContact")]
[TableSplit("Customers")]
public partial class CustomerContact
{
    [PrimaryKey]
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
}
```

### `[TemporalTable]`

SQL Server temporal table support for tracking historical data.

```csharp
[AggregateRoot("Product")]
[Table("Products")]
[TemporalTable(HistoryTable = "ProductsHistory", HistorySchema = "history")]
public partial class Product { /* ... */ }
```

**Generated output:**

```csharp
builder.ToTable(tb => tb.IsTemporal(t =>
{
    t.UseHistoryTable("ProductsHistory", "history");
}));
```

### `[AutoInclude]`

Always eagerly load a navigation property.

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [AutoInclude]
    [OwnedEntity]
    public Address ShippingAddress { get; set; } = null!;
}
```

**Generated output:**

```csharp
builder.Navigation(e => e.ShippingAddress).AutoInclude();
```

### `[ValueGenerator]`

Custom value generation for properties.

```csharp
[ValueGenerator(typeof(OrderCodeGenerator))]
public string Code { get; set; } = "";
```

**Generated output:**

```csharp
builder.Property(e => e.Code).HasValueGenerator<OrderCodeGenerator>();
```

### `[ValueComparer]`

Custom change tracking comparer for complex value types.

```csharp
[ValueComparer(typeof(JsonListComparer<string>))]
public List<string> Tags { get; set; } = new();
```

**Generated output:**

```csharp
builder.Property(e => e.Tags).Metadata.SetValueComparer(new JsonListComparer<string>());
```

### `[Collation]`

Text sorting and comparison rules.

```csharp
[Collation("SQL_Latin1_General_CP1_CI_AS")]
public string Name { get; set; } = "";
```

**Generated output:**

```csharp
builder.Property(e => e.Name).UseCollation("SQL_Latin1_General_CP1_CI_AS");
```

### `[HiLo]`

Batch-friendly key generation using Hi-Lo sequences.

```csharp
[PrimaryKey]
[HiLo(SequenceName = "OrderIds", Schema = "sales")]
public int Id { get; set; }
```

**Generated output:**

```csharp
builder.Property(e => e.Id).UseHiLo("OrderIds", "sales");
```

### `[Sequence]`

Database sequence for key generation.

```csharp
[PrimaryKey]
[Sequence("OrderSequence", Schema = "sales", StartsAt = 1000, IncrementsBy = 10)]
public long Id { get; set; }
```

**Generated output:**

```csharp
modelBuilder.HasSequence<long>("OrderSequence", "sales")
    .StartsAt(1000)
    .IncrementsBy(10);

builder.Property(e => e.Id).UseSequence("OrderSequence", "sales");
```

### `[QueryFilter]`

Global query filter using a static method.

```csharp
[AggregateRoot("Product")]
[Table("Products")]
[QueryFilter(nameof(NotDeleted))]
public partial class Product
{
    [PrimaryKey]
    public int Id { get; set; }
    public bool IsDeleted { get; set; }

    public static Expression<Func<Product, bool>> NotDeleted() => p => !p.IsDeleted;
}
```

**Generated output:**

```csharp
builder.HasQueryFilter(global::MyApp.Domain.Product.NotDeleted());
```

### `[CheckConstraint]`

Database-level check constraint.

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
[CheckConstraint("CK_Orders_Total", "\"Total\" >= 0")]
[CheckConstraint("CK_Orders_Quantity", "\"Quantity\" > 0")]
public partial class Order { /* ... */ }
```

**Generated output:**

```csharp
builder.HasCheckConstraint("CK_Orders_Total", "\"Total\" >= 0");
builder.HasCheckConstraint("CK_Orders_Quantity", "\"Quantity\" > 0");
```

### `[SeedData]`

Initial/reference data using a static method.

```csharp
[Entity("Currency")]
[Table("Currencies")]
[SeedData(nameof(GetSeedData))]
public class Currency
{
    [PrimaryKey(ValueGenerated = ValueGeneration.None)]
    public string Code { get; set; } = "";

    [Required]
    public string Name { get; set; } = "";

    public static Currency[] GetSeedData() =>
    [
        new() { Code = "USD", Name = "US Dollar" },
        new() { Code = "EUR", Name = "Euro" },
        new() { Code = "GBP", Name = "British Pound" },
    ];
}
```

**Generated output:**

```csharp
builder.HasData(global::MyApp.Domain.Currency.GetSeedData());
```

### `[NamingConvention]`

Apply a naming strategy to all column names.

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
[NamingConvention(Strategy = NamingStrategy.SnakeCase)]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = "";
    public decimal TotalAmount { get; set; }
}
```

**Generated output:**

```csharp
builder.Property(e => e.OrderNumber).HasColumnName("order_number");
builder.Property(e => e.TotalAmount).HasColumnName("total_amount");
```

Explicit `[Column(Name = "...")]` overrides the convention for that property.

Available strategies: `PascalCase`, `SnakeCase`, `CamelCase`.

---

## 15. Validation

Entity.Dsl supports two levels of validation expressed via `nameof()` pointing to static methods.

### `[Validate]` -- entity-level compile-time validation

```csharp
[AggregateRoot("Order")]
[Table("Orders")]
[Validate(nameof(ValidateOrder))]
public partial class Order
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public decimal Total { get; set; }
    public int ItemCount { get; set; }

    public static ConstraintResult ValidateOrder(ConceptValidationContext ctx)
    {
        // Verify structural constraints at compile time
        var hasTotal = ctx.Properties.Any(p => p.Name == "Total");
        var hasItems = ctx.Properties.Any(p => p.Name == "ItemCount");

        if (!hasTotal || !hasItems)
            return ConstraintResult.Failed("Order must have Total and ItemCount properties");

        return ConstraintResult.Satisfied();
    }
}
```

The SG calls this method at generation time. If `ConstraintResult.Failed(...)`, the SG emits a compiler error with the message.

### `[ValidateProperty]` -- property-level compile-time validation

```csharp
[AggregateRoot("Product")]
[Table("Products")]
public partial class Product
{
    [PrimaryKey]
    public int Id { get; set; }

    [ValidateProperty(nameof(ValidatePrice))]
    public decimal Price { get; set; }

    public static ConstraintResult ValidatePrice(ConceptValidationContext ctx)
    {
        // Validate structural rules about this property
        var prop = ctx.CurrentProperty;
        if (prop.TypeName != "decimal")
            return ConstraintResult.Failed("Price must be a decimal");
        return ConstraintResult.Satisfied();
    }
}
```

**Method signature contract:**

```csharp
// Entity-level
public static ConstraintResult ValidateOrder(ConceptValidationContext ctx) { ... }

// Property-level
public static ConstraintResult ValidatePrice(ConceptValidationContext ctx) { ... }
```

**Note**: These are **compile-time** structural checks on the DSL model itself. They are distinct from DDD `[Invariant]` methods, which are **runtime** domain rules.

---

## 16. Multi-DbContext

### Bounded context scoping

Use `[DbContext(BoundedContext = "...")]` to scope which entities each DbContext includes.

```csharp
// Entities declare their bounded context
[AggregateRoot("Order", BoundedContext = "Sales")]
[Table("Orders")]
public partial class Order { /* ... */ }

[AggregateRoot("Product", BoundedContext = "Inventory")]
[Table("Products")]
public partial class Product { /* ... */ }

// DbContexts scope to their bounded context
[DbContext(BoundedContext = "Sales")]
public partial class SalesDbContext : DbContext { }

[DbContext(BoundedContext = "Inventory")]
public partial class InventoryDbContext : DbContext { }
```

### Per-context file generation

Each `[DbContext]` gets its own complete set of generated files:

```
Per Sales:
  SalesDbContextBase.g.cs
  SalesDbContext.g.cs
  ISalesDbContextUnitOfWork.g.cs
  SalesDbContextUnitOfWorkBase.g.cs
  SalesDbContextUnitOfWork.g.cs
  SalesDbContextRegistration.g.cs

Per Inventory:
  InventoryDbContextBase.g.cs
  InventoryDbContext.g.cs
  IInventoryDbContextUnitOfWork.g.cs
  InventoryDbContextUnitOfWorkBase.g.cs
  InventoryDbContextUnitOfWork.g.cs
  InventoryDbContextRegistration.g.cs
```

### Startup registration

```csharp
// One call per context
services.AddSalesDbContext(o => o.UseSqlServer("..."));
services.AddInventoryDbContext(o => o.UseSqlite("..."));

// Injectable SG registers all repos + UoWs
services.AddMyAppInjectables();
```

### No BoundedContext

If `BoundedContext` is null on `[DbContext]`, all entities in the assembly are included (default for simple projects).

```csharp
// All entities included
[DbContext]
public partial class AppDbContext : DbContext { }
```

---

## 17. DI Registration

Entity.Dsl reuses the `[Injectable]` attribute from `FrenchExDev.Net.Injectable` for all standard service registrations. Only `AddDbContext` (which requires a configuration lambda) gets a dedicated generated extension method.

### Generated repository with `[Injectable]`

```csharp
// Generated: OrderRepository.g.cs
[Injectable(Scope = Scope.Scoped, As = typeof(IOrderRepository))]
public partial class OrderRepository : OrderRepositoryBase
{
    public OrderRepository(SalesDbContext context) : base(context) { }
}
```

The Injectable SG picks this up and generates:

```csharp
services.AddScoped<IOrderRepository, OrderRepository>();
```

### Generated UnitOfWork with `[Injectable]`

```csharp
// Generated: SalesDbContextUnitOfWork.g.cs
[Injectable(Scope = Scope.Scoped, As = typeof(ISalesDbContextUnitOfWork))]
public partial class SalesDbContextUnitOfWork : SalesDbContextUnitOfWorkBase
{
    public SalesDbContextUnitOfWork(SalesDbContext context) : base(context) { }
}
```

### DbContext registration (dedicated extension)

```csharp
// Generated: SalesDbContextRegistration.g.cs
namespace Microsoft.Extensions.DependencyInjection;

public static class SalesDbContextRegistration
{
    public static IServiceCollection AddSalesDbContext(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<SalesDbContext>(configureDbContext);
        return services;
    }
}
```

### Entity listeners (developer-written with `[Injectable]`)

Listeners are developer-written, so you add `[Injectable]` yourself:

```csharp
[Injectable(Scope = Scope.Scoped, As = typeof(IEntityListener<Order>))]
public class OrderAuditListener : IEntityListener<Order> { /* ... */ }
```

### Startup

```csharp
services.AddSalesDbContext(o => o.UseSqlite("..."));   // DbContext (EF-specific)
services.AddMyAppInjectables();                         // Everything else (Injectable SG)
```

---

## 18. DSL-to-DSL

The emit models in `FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib` are a **public contract** from day one. Another source generator (or code generator) can construct `EntityEmitModel` instances programmatically and call the emitters to produce EF Core code without going through attributes at all.

### Building an emit model programmatically

```csharp
using FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

// Build the entity model
var entityModel = new EntityEmitModel
{
    Namespace = "MyApp.Domain",
    ClassName = "Product",
    ClassFullName = "global::MyApp.Domain.Product",
    TableName = "Products",
    Schema = "catalog",
    PrimaryKeyProperties =
    {
        new KeyPropertyModel
        {
            PropertyName = "Id",
            ValueGenerated = "OnAdd",
            Order = 0
        }
    },
    Properties =
    {
        new PropertyConfigModel
        {
            PropertyName = "Name",
            IsRequired = true,
            MaxLength = 200,
            ColumnName = "product_name"
        },
        new PropertyConfigModel
        {
            PropertyName = "Price",
            IsRequired = true,
            ColumnType = "decimal(18,2)"
        }
    }
};

// Emit configuration files
string configBase = EntityConfigurationEmitter.EmitBase(entityModel);
string configStub = EntityConfigurationEmitter.EmitPartialStub(entityModel);
string configReg = EntityConfigurationEmitter.EmitRegistration(entityModel);

// Build the DbContext model
var dbContextModel = new DbContextEmitModel
{
    Namespace = "MyApp.Infrastructure",
    ClassName = "CatalogDbContext",
    DbSets =
    {
        new DbSetModel
        {
            EntityTypeFull = "global::MyApp.Domain.Product",
            PropertyName = "Products"
        }
    }
};

// Emit DbContext files
string ctxBase = DbContextEmitter.EmitBase(dbContextModel);
string ctxStub = DbContextEmitter.EmitPartialStub(dbContextModel);
string ctxReg = DbContextRegistrationEmitter.Emit(dbContextModel);

// Build repository model
var repoModel = new RepositoryEmitModel
{
    Namespace = "MyApp.Domain",
    EntityClassName = "Product",
    EntityClassFull = "global::MyApp.Domain.Product",
    PrimaryKeyTypeFull = "global::System.Guid",
    DbContextTypeFull = "global::MyApp.Infrastructure.CatalogDbContext"
};

// Emit repository files
string repoInterface = RepositoryEmitter.EmitInterface(repoModel);
string repoBase = RepositoryEmitter.EmitBase(repoModel);
string repoStub = RepositoryEmitter.EmitPartialStub(repoModel);

// Build UnitOfWork model
var uowModel = new UnitOfWorkEmitModel
{
    Namespace = "MyApp.Infrastructure",
    DbContextClassName = "CatalogDbContext",
    DbContextClassFull = "global::MyApp.Infrastructure.CatalogDbContext",
    Repositories =
    {
        new UnitOfWorkRepositoryModel
        {
            InterfaceTypeFull = "global::MyApp.Domain.Repositories.IProductRepository",
            ImplementationTypeFull = "global::MyApp.Domain.Repositories.ProductRepository",
            PropertyName = "Products"
        }
    }
};

// Emit UnitOfWork files
string uowInterface = UnitOfWorkEmitter.EmitInterface(uowModel);
string uowBase = UnitOfWorkEmitter.EmitBase(uowModel);
string uowStub = UnitOfWorkEmitter.EmitPartialStub(uowModel);
```

### Use case: generating Entity.Dsl from another DSL

If you have a higher-level DSL (such as a YAML schema, a JSON schema, or a Diem CMF content model), you can:

1. Parse your DSL input into your own model
2. Map your model to `EntityEmitModel`, `DbContextEmitModel`, etc.
3. Call the emitters to produce fully-wired EF Core code

This keeps the Entity.Dsl emitters as a reusable code generation engine independent of how models are discovered.

### Available emit models (all in `SourceGenerator.Lib`)

| Model | Purpose |
|---|---|
| `EntityEmitModel` | Entity configuration (table, keys, properties, relationships, behaviors) |
| `DbContextEmitModel` | DbContext (DbSets, hooks, context-level config) |
| `DbSetModel` | Single DbSet entry in the context |
| `RepositoryEmitModel` | Repository (entity type, key type, context type) |
| `UnitOfWorkEmitModel` | UnitOfWork (context type, repository list) |
| `KeyPropertyModel` | Primary key property (name, order, value generation) |
| `PropertyConfigModel` | Property configuration (column, type, required, max length, etc.) |
| `RelationshipModel` | Relationship (kind, FK, delete behavior, etc.) |
| `OwnedEntityModel` | Owned type navigation |
| `ComplexTypeModel` | Complex type navigation |
| `AssociationClassModel` | Association class endpoints |
| `InheritanceModel` | Inheritance strategy and derived types |
| `IndexModel` | Index definition |
| `AlternateKeyModel` | Alternate key definition |
| `ShadowPropertyModel` | Shadow property |
| `CheckConstraintModel` | Check constraint |
| `TemporalTableModel` | Temporal table config |
| `EntitySplitModel` | Entity split table mapping |
| `StoredProcedureModel` | Stored procedure mapping |
| `TimestampableBehaviorModel` | Timestampable behavior config |
| `SoftDeletableBehaviorModel` | Soft-deletable behavior config |
| `BlameableBehaviorModel` | Blameable behavior config |
| `VersionableBehaviorModel` | Versionable behavior config |
| `SluggableBehaviorModel` | Sluggable behavior config |
| `LoggableBehaviorModel` | Loggable behavior config |
| `TranslatableBehaviorModel` | Translatable behavior config |
| `SortableBehaviorModel` | Sortable behavior config |
| `TreeNodeBehaviorModel` | TreeNode behavior config |
