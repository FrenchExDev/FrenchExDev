# How To — FrenchExDev.Net.Ddd

Step-by-step guides for modeling domains using the DDD DSL.

---

## 1. Define an Aggregate Root

An aggregate root is the entry point to a cluster of entities and value objects. It has an identity, owns its children, and enforces invariants.

```csharp
using FrenchExDev.Net.Ddd.Attributes;

[AggregateRoot("Order", BoundedContext = "Ordering")]
public partial class Order
{
    [EntityId]
    public partial OrderId Id { get; }

    [Property("OrderDate", Required = true)]
    public partial DateTime OrderDate { get; }

    [Property("Status", Required = true)]
    public partial OrderStatus Status { get; }
}
```

Key rules:
- The class must be `partial` (the SG adds the other half)
- Must have exactly one `[EntityId]` property (enforced by DDD001 diagnostic)
- `BoundedContext` is optional but recommended for organizing large domains
- The `Name` parameter should match the class name

### Strongly-Typed IDs

Define an ID type for each aggregate:

```csharp
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
}
```

---

## 2. Add Entities Within an Aggregate

Entities have identity and are owned by their aggregate root via `[Composition]`:

```csharp
[Entity("OrderLine")]
public partial class OrderLine
{
    [EntityId]
    public partial OrderLineId Id { get; }

    [Property("ProductName", Required = true, MaxLength = 200)]
    public partial string ProductName { get; }

    [Property("Quantity", Required = true)]
    public partial int Quantity { get; }

    [Composition]
    public partial Money UnitPrice { get; }

    [Composition]
    public partial Money LineTotal { get; }
}
```

Connect it to the aggregate root:

```csharp
[AggregateRoot("Order")]
public partial class Order
{
    // ... Id, properties ...

    [Composition]  // Order OWNS its lines (cascade delete)
    public partial IReadOnlyList<OrderLine> Lines { get; }
}
```

---

## 3. Define Value Objects

Value objects have no identity. They're immutable and compared by value. Use `[ValueComponent]` for their fields:

```csharp
[ValueObject("Money")]
public partial class Money
{
    [ValueComponent("Amount", "decimal", Required = true)]
    public partial decimal Amount { get; }

    [ValueComponent("Currency", "string", Required = true)]
    public partial string Currency { get; }
}

[ValueObject("ShippingAddress")]
public partial class ShippingAddress
{
    [ValueComponent("Street", "string", Required = true)]
    public partial string Street { get; }

    [ValueComponent("City", "string", Required = true)]
    public partial string City { get; }

    [ValueComponent("PostalCode", "string", Required = true)]
    public partial string PostalCode { get; }

    [ValueComponent("Country", "string", Required = true)]
    public partial string Country { get; }
}
```

Value objects are embedded in their parent via `[Composition]`:

```csharp
[AggregateRoot("Order")]
public partial class Order
{
    [Composition]
    public partial ShippingAddress ShippingAddress { get; }  // OwnsOne in EF Core
}
```

---

## 4. Write Invariants

Invariants are private methods that return `Result`. They enforce domain rules. The source generator discovers them and wires them into `EnsureInvariants()`.

```csharp
using FrenchExDev.Net.Result;

[AggregateRoot("Order")]
public partial class Order
{
    [Composition]
    public partial IReadOnlyList<OrderLine> Lines { get; }

    [Invariant("Order must have at least one line item")]
    private Result HasLines()
        => Lines.Count > 0
            ? Result.Success()
            : Result.Failure("Order must have at least one line item");

    [Invariant("Order total must be positive")]
    private Result TotalIsPositive()
    {
        var total = Lines.Sum(l => l.LineTotal.Amount);
        return total > 0
            ? Result.Success()
            : Result.Failure($"Order total ({total}) is not positive");
    }

    [Invariant("Shipped orders must have a shipping address")]
    private Result ShippedOrdersHaveAddress()
    {
        if (Status != OrderStatus.Shipped)
            return Result.Success(); // not applicable

        return ShippingAddress is not null
            ? Result.Success()
            : Result.Failure("Shipped order must have a shipping address");
    }
}
```

The generator emits:

```csharp
// Generated: Order.Invariants.g.cs
public partial class Order
{
    public Result EnsureInvariants()
    {
        var _results = new List<Result>();
        _results.Add(HasLines());
        _results.Add(TotalIsPositive());
        _results.Add(ShippedOrdersHaveAddress());
        foreach (var _r in _results)
        {
            if (_r.IsFailure) return _r;
        }
        return Result.Success();
    }
}
```

Rules for invariant methods:
- Must be `private`
- Must return `FrenchExDev.Net.Result.Result`
- Must have `[Invariant("description")]`
- Can access any property or method on the class
- Should be pure (no side effects)

---

## 5. Use Relationship Attributes

### Composition (ownership, cascade delete)

```csharp
// Single entity (1:1)
[Composition]
public partial PaymentDetails Payment { get; }

// Collection of entities (1:N)
[Composition]
public partial IReadOnlyList<OrderLine> Lines { get; }

// Value object (embedded columns)
[Composition]
public partial ShippingAddress ShippingAddress { get; }
```

### Association (cross-aggregate reference)

```csharp
// Reference to another aggregate (no cascade, eventual consistency)
[Association]
public partial CustomerId CustomerId { get; }
```

### Aggregation (weak reference)

```csharp
// Nullable FK, no cascade
[Aggregation]
public partial CategoryId? PreferredCategory { get; }
```

---

## 6. Define CQRS Commands

```csharp
[Command("PlaceOrder", AggregateRoot = "Order")]
public partial record PlaceOrderCommand
{
    [Property("CustomerId", Required = true)]
    public partial CustomerId CustomerId { get; }

    [Property("Lines", Required = true)]
    public partial IReadOnlyList<OrderLineDto> Lines { get; }

    [Property("ShippingAddress", Required = true)]
    public partial ShippingAddress ShippingAddress { get; }
}
```

Implement the handler:

```csharp
public class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand, Result<OrderId>>
{
    private readonly IOrderRepository _repository;

    public PlaceOrderHandler(IOrderRepository repository) => _repository = repository;

    public async Task<Result<OrderId>> HandleAsync(
        PlaceOrderCommand command, CancellationToken ct = default)
    {
        var builder = new OrderBuilder()
            .WithOrderDate(DateTime.UtcNow)
            .WithCustomer(command.CustomerId)
            .WithShippingAddress(command.ShippingAddress);

        foreach (var line in command.Lines)
            builder.AddLine(line.ToOrderLine());

        var buildResult = builder.Build(); // calls EnsureInvariants()
        if (!buildResult.IsSuccess)
            return Result<OrderId>.Failure(buildResult.ValidationResult);

        await _repository.AddAsync(buildResult.Value, ct);
        await _repository.SaveChangesAsync(ct);

        return Result<OrderId>.Success(buildResult.Value.Id);
    }
}
```

---

## 7. Define Domain Events

```csharp
[DomainEvent("OrderPlaced", SourceAggregate = "Order")]
public partial record OrderPlacedEvent : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    [Property("OrderId", Required = true)]
    public partial OrderId OrderId { get; }

    [Property("CustomerId", Required = true)]
    public partial CustomerId CustomerId { get; }
}
```

---

## 8. Declare Bounded Contexts

At the assembly level, declare what bounded contexts exist:

```csharp
// In AssemblyInfo.cs or any file
[assembly: BoundedContext("Ordering", Description = "Order processing and fulfillment")]
[assembly: BoundedContext("Catalog", Description = "Product catalog and pricing")]
[assembly: BoundedContext("Identity", Description = "User authentication and authorization")]
```

Then reference the context in aggregate roots:

```csharp
[AggregateRoot("Order", BoundedContext = "Ordering")]
public partial class Order { /* ... */ }

[AggregateRoot("Product", BoundedContext = "Catalog")]
public partial class Product { /* ... */ }
```

---

## 9. Test Invariants

Invariants are regular private methods. Test them via `EnsureInvariants()` on the generated class:

```csharp
[Fact]
public void Order_with_no_lines_fails_invariant()
{
    var order = new OrderBuilder()
        .WithOrderDate(DateTime.UtcNow)
        .WithStatus(OrderStatus.Draft)
        .Build();

    // Builder calls EnsureInvariants() internally
    Assert.False(order.IsSuccess);
}

[Fact]
public void Order_with_lines_passes_invariant()
{
    var order = new OrderBuilder()
        .WithOrderDate(DateTime.UtcNow)
        .AddLine(new OrderLineBuilder()
            .WithProductName("Laptop")
            .WithQuantity(1)
            .WithUnitPrice(new Money(999.99m, "USD"))
            .WithLineTotal(new Money(999.99m, "USD"))
            .Build().Value)
        .Build();

    Assert.True(order.IsSuccess);
    Assert.Equal(1, order.Value.Lines.Count);
}
```

### Test constraint methods directly

```csharp
[Fact]
public void MustHaveId_satisfied_when_EntityId_present()
{
    var ctx = new ConceptValidationContext
    {
        ConceptName = "AggregateRoot",
        TypeName = "Order",
        Properties = new List<ConceptPropertyInfo>
        {
            new ConceptPropertyInfo
            {
                Name = "Id",
                TypeName = "OrderId",
                AttributeNames = new List<string> { "EntityId" }
            }
        }
    };

    var result = AggregateRootAttribute.MustHaveIdConstraint(ctx);
    Assert.True(result.IsSatisfied);
}
```

---

## 10. Test Companion Concepts

```csharp
[Fact]
public void AggregateRoot_can_contain_entity()
{
    var agg = new AggregateRootConcept();
    var entity = new EntityConcept();
    Assert.True(agg.CanContain(entity));
}

[Fact]
public void AggregateRoot_cannot_contain_command()
{
    var agg = new AggregateRootConcept();
    var cmd = new CommandConcept();
    Assert.False(agg.CanContain(cmd));
}

[Fact]
public void AggregateRoot_is_supertype_of_Entity()
{
    var agg = new AggregateRootConcept();
    Assert.Contains(typeof(EntityConcept), agg.SuperTypes);
}
```

---

## 11. Verify Attributes Have MetaConcept

Every DDD attribute should be `[MetaConcept]`-decorated. Verify in tests:

```csharp
[Theory]
[InlineData(typeof(AggregateRootAttribute))]
[InlineData(typeof(EntityAttribute))]
[InlineData(typeof(ValueObjectAttribute))]
[InlineData(typeof(CompositionAttribute))]
[InlineData(typeof(InvariantAttribute))]
[InlineData(typeof(CommandAttribute))]
[InlineData(typeof(DomainEventAttribute))]
public void Attribute_has_MetaConcept(Type attributeType)
{
    var attr = Attribute.GetCustomAttribute(attributeType, typeof(MetaConceptAttribute));
    Assert.NotNull(attr);
}
```

---

## 12. Complete Example: E-Commerce Order Aggregate

Here's a full aggregate showing every feature:

```csharp
// OrderId.cs
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
}

public readonly record struct OrderLineId(Guid Value)
{
    public static OrderLineId New() => new(Guid.NewGuid());
}

public enum OrderStatus { Draft, Placed, Paid, Shipped, Delivered, Cancelled }
public enum PaymentMethod { CreditCard, BankTransfer, PayPal }

// Money.cs
[ValueObject("Money")]
public partial class Money
{
    [ValueComponent("Amount", "decimal", Required = true)]
    public partial decimal Amount { get; }

    [ValueComponent("Currency", "string", Required = true)]
    public partial string Currency { get; }
}

// ShippingAddress.cs
[ValueObject("ShippingAddress")]
public partial class ShippingAddress
{
    [ValueComponent("Street", "string", Required = true)]
    public partial string Street { get; }

    [ValueComponent("City", "string", Required = true)]
    public partial string City { get; }

    [ValueComponent("PostalCode", "string", Required = true)]
    public partial string PostalCode { get; }

    [ValueComponent("Country", "string", Required = true)]
    public partial string Country { get; }
}

// OrderLine.cs
[Entity("OrderLine")]
public partial class OrderLine
{
    [EntityId]
    public partial OrderLineId Id { get; }

    [Property("ProductName", Required = true, MaxLength = 200)]
    public partial string ProductName { get; }

    [Property("Quantity", Required = true)]
    public partial int Quantity { get; }

    [Composition]
    public partial Money UnitPrice { get; }

    [Composition]
    public partial Money LineTotal { get; }
}

// Order.cs
[AggregateRoot("Order", BoundedContext = "Ordering")]
public partial class Order
{
    [EntityId]
    public partial OrderId Id { get; }

    [Property("OrderDate", Required = true)]
    public partial DateTime OrderDate { get; }

    [Property("Status", Required = true)]
    public partial OrderStatus Status { get; }

    [Composition]
    public partial IReadOnlyList<OrderLine> Lines { get; }

    [Composition]
    public partial ShippingAddress ShippingAddress { get; }

    [Association]
    public partial CustomerId CustomerId { get; }

    [Invariant("Order must have at least one line item")]
    private Result HasLines()
        => Lines.Count > 0
            ? Result.Success()
            : Result.Failure("Order must have at least one line item");

    [Invariant("Order total must be positive")]
    private Result TotalIsPositive()
    {
        var total = Lines.Sum(l => l.LineTotal.Amount);
        return total > 0
            ? Result.Success()
            : Result.Failure($"Order total ({total}) must be positive");
    }

    [Invariant("Cancelled orders cannot be modified")]
    private Result CancelledOrdersImmutable()
        => Result.Success(); // enforced by command handler guards
}

// PlaceOrderCommand.cs
[Command("PlaceOrder", AggregateRoot = "Order")]
public partial record PlaceOrderCommand
{
    [Property("CustomerId", Required = true)]
    public partial CustomerId CustomerId { get; }

    [Property("Lines", Required = true)]
    public partial IReadOnlyList<OrderLineDto> Lines { get; }
}

// OrderPlacedEvent.cs
[DomainEvent("OrderPlaced", SourceAggregate = "Order")]
public partial record OrderPlacedEvent : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required OrderId OrderId { get; init; }
    public required CustomerId CustomerId { get; init; }
}
```

From these ~120 lines, the compiler generates:
- Entity implementations with backing fields
- `EnsureInvariants()` calling all 3 invariant methods
- `OrderBuilder` with fluent `With*()` methods
- `IEntityTypeConfiguration<Order>` for EF Core
- `IOrderRepository` interface + `OrderRepository` implementation
- `PlaceOrderCommandHandler`

---

## 13. Project Setup

### Reference the DDD DSL in your domain project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <!-- Runtime types (IDomainEvent, ICommandHandler, etc.) -->
    <ProjectReference Include="...\FrenchExDev.Net.Ddd\FrenchExDev.Net.Ddd.csproj" />
    <!-- DSL attributes ([AggregateRoot], [Entity], etc.) -->
    <ProjectReference Include="...\FrenchExDev.Net.Ddd.Attributes\FrenchExDev.Net.Ddd.Attributes.csproj" />
    <!-- Source generator (emits EnsureInvariants, builders, etc.) -->
    <ProjectReference Include="...\FrenchExDev.Net.Ddd.SourceGenerator\FrenchExDev.Net.Ddd.SourceGenerator.csproj"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <ProjectReference Include="...\FrenchExDev.Net.Ddd.SourceGenerator.Lib\FrenchExDev.Net.Ddd.SourceGenerator.Lib.csproj"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

### Reference test helpers in your test project

```xml
<ItemGroup>
  <ProjectReference Include="...\FrenchExDev.Net.Ddd.Testing\FrenchExDev.Net.Ddd.Testing.csproj" />
</ItemGroup>
```
