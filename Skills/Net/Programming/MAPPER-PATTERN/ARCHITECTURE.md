# MAPPER-PATTERN — Architecture

## Four-Project Decomposition

The same shape used by Builder, Injectable, and other SG-driven libraries:

```
Mapper                    Interface — netstandard2.0 + net10.0 — zero deps
  IMapper<in TSource, out TTarget>

Mapper.Attributes         Attributes — netstandard2.0 + net10.0 — zero deps
  [MapFrom], [MapTo], [MapProperty], [IgnoreMapping], [OneWay]

Mapper.Testing            Test helpers — refs Mapper only
  ShouldMapTo, ShouldMapProperty extensions

Mapper.SourceGenerator    Roslyn — refs Attributes + Roslyn
  reads attributes, emits IMapper<,> implementations
```

The Attributes project is **independent of the Mapper interface project** — it's referenced by the SG, not by application code that wants to use mappers. The application references `Mapper` for the interface and `Mapper.SourceGenerator` as a project analyzer.

## Interface Design

```csharp
public interface IMapper<in TSource, out TTarget>
{
    TTarget Map(TSource source);
}
```

- **Contravariant** on `TSource` (`in`) — a mapper from `Animal → AnimalDto` can be used where `Dog → AnimalDto` is expected.
- **Covariant** on `TTarget` (`out`) — a mapper from `Order → OrderDetailDto` can be used where `Order → OrderDto` is expected (if `OrderDetailDto : OrderDto`).
- **Synchronous** — pure data transformation.
- **Single method** — `Map(TSource) → TTarget`. No `Map(TSource, TTarget)` overwrite variant.

## Attribute Model

### Class-Level (`AllowMultiple = true`)

| Attribute | Placed on | Meaning |
|---|---|---|
| `[MapFrom(typeof(Source))]` | Target class | "Generate a mapper FROM Source TO me" |
| `[MapTo(typeof(Target))]` | Source class | "Generate a mapper FROM me TO Target" |
| `[OneWay]` | Either | "Do NOT generate the reverse mapper" |

`AllowMultiple = true` lets one class participate in multiple mappings:

```csharp
[MapFrom(typeof(OrderEntity))]
[MapFrom(typeof(OrderEvent))]
public class OrderDto { ... }
```

### Property-Level

| Attribute | Meaning |
|---|---|
| `[MapProperty("SourceProp")]` | Map from a differently-named source property |
| `[IgnoreMapping]` | Exclude from automatic mapping |

## Source Generator Pipeline

```
1. Find types decorated with [MapFrom] or [MapTo]
2. For each mapping pair (Source, Target):
   a. Enumerate target properties
   b. For each property:
      - If [IgnoreMapping] → skip
      - If [MapProperty("X")] → map from source.X
      - Else → map from source.{SameName}
   c. If source property not found → diagnostic warning
3. Emit: sealed class {Source}To{Target}Mapper : IMapper<Source, Target>
4. If not [OneWay] → also emit reverse mapper
```

The emitted mapper is plain C# — concrete property assignments inside `Map(TSource)`. No reflection at runtime.

## Generated Code Example

For:

```csharp
[MapFrom(typeof(OrderEntity))]
public class OrderDto
{
    public int Id { get; set; }
    [MapProperty("CustomerName")]
    public string Client { get; set; } = "";
    [IgnoreMapping]
    public string DisplayLabel { get; set; } = "";
}
```

The SG emits:

```csharp
public sealed class OrderEntityToOrderDtoMapper : IMapper<OrderEntity, OrderDto>
{
    public OrderDto Map(OrderEntity source) => new()
    {
        Id = source.Id,
        Client = source.CustomerName,
        // DisplayLabel skipped — [IgnoreMapping]
    };
}
```

## Test Assertions

```csharp
mapper.ShouldMapTo(source, expected);                    // full equality
mapper.ShouldMapProperty(source, t => t.Client, "Alice"); // single property
```

`ShouldMapTo` requires the target type to override `Equals` (or be a record). `ShouldMapProperty` does not — it tests one selected property without requiring full equality on the type. For wide DTOs where you're testing one specific mapping rule, property assertions produce more focused failure messages.

## Dependency Graph

```
Mapper                    (no deps)
Mapper.Attributes         (no deps)
Mapper.Testing            (refs Mapper)
Mapper.Tests              (refs Mapper + Attributes + Testing + xUnit)
Mapper.SourceGenerator    (refs Attributes + Microsoft.CodeAnalysis.CSharp)
```

All three runtime assemblies have zero NuGet dependencies. The SG is the only place Roslyn appears.

## Multi-Targeting

- `Mapper`: `netstandard2.0 + net10.0` so it works in .NET Framework, .NET 6+, .NET 10.
- `Mapper.Attributes`: same — referenced by both runtime code and the SG.
- `Mapper.SourceGenerator`: `netstandard2.0` (Roslyn requirement).

## Anchor Package

[`Net/FrenchExDev/Mapper/`](../../../Net/FrenchExDev/Mapper/) — interface, attributes, testing, and tests. Same 4-project layout used by Builder/Injectable.
