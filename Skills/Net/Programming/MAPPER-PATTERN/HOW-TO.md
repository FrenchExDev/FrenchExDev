# MAPPER-PATTERN — How-To

## 1. Defining a Mapping With `[MapFrom]`

Place `[MapFrom]` on the **target** type — common in DTO projects that don't reference domain entities directly:

```csharp
[MapFrom(typeof(OrderEntity))]
public class OrderDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
}
```

The SG generates `OrderEntityToOrderDtoMapper : IMapper<OrderEntity, OrderDto>`.

## 2. Defining a Mapping With `[MapTo]`

Place `[MapTo]` on the **source** type — common in domain projects:

```csharp
[MapTo(typeof(OrderDto))]
public class OrderEntity
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
}
```

Both produce the same mapper. Choose whichever reads better for the codebase organization.

## 3. Property Renaming

```csharp
[MapFrom(typeof(OrderEntity))]
public class OrderDto
{
    public int Id { get; set; }

    [MapProperty("CustomerName")]
    public string Client { get; set; } = "";
}
```

## 4. Ignoring Properties

```csharp
[MapFrom(typeof(OrderEntity))]
public class OrderDto
{
    public int Id { get; set; }

    [IgnoreMapping]
    public string DisplayLabel { get; set; } = "";   // computed, not mapped
}
```

## 5. One-Way Mappings

By default the SG generates `A → B` and `B → A`. Suppress the reverse when it would be unsafe:

```csharp
[MapTo(typeof(OrderDto))]
[OneWay]
public class OrderEntity { ... }
```

## 6. Multiple Mappings On One Type

```csharp
[MapFrom(typeof(OrderEntity))]
[MapFrom(typeof(OrderEvent))]
public class OrderDto { ... }
```

Generates `OrderEntityToOrderDtoMapper` AND `OrderEventToOrderDtoMapper`.

## 7. Manual Implementation (No SG)

The interface is usable on its own. Without an SG, implement it directly:

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

This is exactly what the SG would emit.

## 8. Using a Mapper

```csharp
IMapper<OrderEntity, OrderDto> mapper = new OrderEntityToOrderDtoMapper();
OrderDto dto = mapper.Map(entity);

// Map a collection — synchronous, composable
var dtos = entities.Select(mapper.Map).ToList();
```

## 9. DI Registration

```csharp
services.AddSingleton<IMapper<OrderEntity, OrderDto>, OrderEntityToOrderDtoMapper>();
```

Or with `[Injectable]`:

```csharp
[Injectable(Scope = Scope.Singleton, As = typeof(IMapper<OrderEntity, OrderDto>))]
public sealed class OrderEntityToOrderDtoMapper : IMapper<OrderEntity, OrderDto> { ... }
```

Mappers are stateless — always singleton.

## 10. Testing — Full Equality

```csharp
var mapper = new OrderEntityToOrderDtoMapper();
var source = new OrderEntity { Id = 1, CustomerName = "Alice", Total = 99.99m };
var expected = new OrderDto { Id = 1, Client = "Alice", Total = 99.99m };

mapper.ShouldMapTo(source, expected);
```

Requires `Equals` overridden on the target — easiest if the DTO is a `record`.

## 11. Testing — Single Property

For wide DTOs without overridden equality:

```csharp
mapper.ShouldMapProperty(source, d => d.Client, "Alice");
mapper.ShouldMapProperty(source, d => d.Total, 99.99m);
```

This is more focused than full equality and produces better failure messages.

## What NOT To Do

- **Don't make `Map` async.** Mapping is pure data transformation. Async work belongs in services, not mappers.
- **Don't load related entities inside `Map`.** Mappers receive what they need; queries fetch.
- **Don't put validation in mappers.** Validation belongs in `Result<T>`-returning validators.
- **Don't use a single global `IMapper` with `Map<T>(object)`.** That defeats the typing.
- **Don't add runtime configuration.** All mapping decisions are compile-time via attributes.

## Anchor Package

[`Net/FrenchExDev/Mapper/`](../../../Net/FrenchExDev/Mapper/) — full implementation.
