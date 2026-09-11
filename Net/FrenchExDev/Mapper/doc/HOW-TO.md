# Mapper -- Developer Guide (HOW-TO)

## Table of Contents

1. [Defining a Mapping with MapFrom](#1-defining-a-mapping-with-mapfrom)
2. [Defining a Mapping with MapTo](#2-defining-a-mapping-with-mapto)
3. [Property Renaming](#3-property-renaming)
4. [Ignoring Properties](#4-ignoring-properties)
5. [One-Way Mappings](#5-one-way-mappings)
6. [Multiple Mappings on One Type](#6-multiple-mappings-on-one-type)
7. [Manual Mapper Implementation](#7-manual-mapper-implementation)
8. [Using a Generated Mapper](#8-using-a-generated-mapper)
9. [DI Registration](#9-di-registration)
10. [Testing with ShouldMapTo](#10-testing-with-shouldmapto)
11. [Testing Individual Properties](#11-testing-individual-properties)
12. [Running Tests](#12-running-tests)

---

## 1. Defining a Mapping with MapFrom

Place `[MapFrom]` on the **target** type to say "map from Source to me":

```csharp
[MapFrom(typeof(OrderEntity))]
public class OrderDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
}
```

The SG will generate `OrderEntityToOrderDtoMapper : IMapper<OrderEntity, OrderDto>`.

---

## 2. Defining a Mapping with MapTo

Place `[MapTo]` on the **source** type to say "map from me to Target":

```csharp
[MapTo(typeof(OrderDto))]
public class OrderEntity
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
}
```

Both `[MapFrom]` and `[MapTo]` produce the same mapper. Choose whichever reads better for your codebase.

---

## 3. Property Renaming

When source and target properties have different names, use `[MapProperty]` on the target property:

```csharp
[MapFrom(typeof(OrderEntity))]
public class OrderDto
{
    public int Id { get; set; }

    [MapProperty("CustomerName")]  // maps from OrderEntity.CustomerName
    public string Client { get; set; } = "";
}
```

---

## 4. Ignoring Properties

Exclude properties from automatic mapping with `[IgnoreMapping]`:

```csharp
[MapFrom(typeof(OrderEntity))]
public class OrderDto
{
    public int Id { get; set; }

    [IgnoreMapping]
    public string DisplayLabel { get; set; } = "";  // computed, not mapped
}
```

---

## 5. One-Way Mappings

By default, the SG generates both `A→B` and `B→A` mappers. Add `[OneWay]` to suppress the reverse:

```csharp
[MapTo(typeof(OrderDto))]
[OneWay]
public class OrderEntity { ... }
// Generates: OrderEntityToOrderDtoMapper
// Does NOT generate: OrderDtoToOrderEntityMapper
```

---

## 6. Multiple Mappings on One Type

Attributes are `AllowMultiple = true`:

```csharp
[MapFrom(typeof(OrderEntity))]
[MapFrom(typeof(OrderEvent))]
public class OrderDto { ... }
// Generates: OrderEntityToOrderDtoMapper + OrderEventToOrderDtoMapper
```

---

## 7. Manual Mapper Implementation

Without a source generator, implement `IMapper<TSource, TTarget>` manually:

```csharp
public sealed class OrderEntityToOrderDtoMapper : IMapper<OrderEntity, OrderDto>
{
    public OrderDto Map(OrderEntity source) => new()
    {
        Id = source.Id,
        Client = source.CustomerName,
        Total = source.Total
    };
}
```

This is exactly what the SG would generate.

---

## 8. Using a Generated Mapper

```csharp
IMapper<OrderEntity, OrderDto> mapper = new OrderEntityToOrderDtoMapper();
OrderDto dto = mapper.Map(entity);

// Or map a collection
var dtos = entities.Select(mapper.Map).ToList();
```

---

## 9. DI Registration

```csharp
services.AddSingleton<IMapper<OrderEntity, OrderDto>, OrderEntityToOrderDtoMapper>();

// With Injectable SG
[Injectable(Scope = Scope.Singleton, As = typeof(IMapper<OrderEntity, OrderDto>))]
public sealed class OrderEntityToOrderDtoMapper : IMapper<OrderEntity, OrderDto> { ... }
```

---

## 10. Testing with ShouldMapTo

Assert full object equality:

```csharp
var mapper = new OrderEntityToOrderDtoMapper();
var source = new OrderEntity { Id = 1, CustomerName = "Alice", Total = 99.99m };
var expected = new OrderDto { Id = 1, Client = "Alice", Total = 99.99m };

mapper.ShouldMapTo(source, expected);
// Throws MapperAssertionException if not equal
```

Requires `Equals` to be overridden on the target type (or use a record).

---

## 11. Testing Individual Properties

Assert specific properties without full equality:

```csharp
mapper.ShouldMapProperty(source, d => d.Client, "Alice");
mapper.ShouldMapProperty(source, d => d.Total, 99.99m);
mapper.ShouldMapProperty(source, d => d.Id, 1);
```

---

## 12. Running Tests

```bash
dotnet test Mapper/FrenchExDev.Net.Mapper.slnx

# With quality gate
dotnet quality-gate test --config Mapper/quality-gate.yml
```

Test suite: 15 xUnit tests covering attributes (8 tests) and mapper interface + assertions (7 tests).
