# Mapper

Source-generation-ready object mapping contracts and attributes for .NET. Defines `IMapper<TSource, TTarget>` (a covariant, contravariant single-method interface), five mapping attributes (`[MapFrom]`, `[MapTo]`, `[MapProperty]`, `[IgnoreMapping]`, `[OneWay]`) for source generator consumption, and test assertions (`ShouldMapTo`, `ShouldMapProperty`). No runtime reflection -- the attributes are designed to be read by an incremental source generator that emits concrete mappers at compile time.

## Quick Start

```csharp
// 1. Declare the mapping with attributes (SG reads these)
[MapFrom(typeof(OrderEntity))]
public sealed class OrderDto
{
    public int Id { get; set; }

    [MapProperty("CustomerName")]   // source property has a different name
    public string Client { get; set; } = "";

    [IgnoreMapping]                 // not mapped
    public string DisplayLabel { get; set; } = "";
}

// 2. SG generates: OrderEntityToOrderDtoMapper : IMapper<OrderEntity, OrderDto>

// 3. Use the generated mapper
IMapper<OrderEntity, OrderDto> mapper = new OrderEntityToOrderDtoMapper();
OrderDto dto = mapper.Map(entity);

// 4. Test
mapper.ShouldMapTo(entity, expectedDto);
mapper.ShouldMapProperty(entity, d => d.Client, "Alice");
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Mapper` | netstandard2.0; net10.0 | `IMapper<TSource, TTarget>` interface (single `Map` method) |
| `Mapper.Attributes` | netstandard2.0; net10.0 | `[MapFrom]`, `[MapTo]`, `[MapProperty]`, `[IgnoreMapping]`, `[OneWay]` |
| `Mapper.Testing` | netstandard2.0; net10.0 | `ShouldMapTo`, `ShouldMapProperty` assertion extensions |
| `Mapper.Tests` | net10.0 | 15 xUnit tests |

## Attributes

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[MapFrom(typeof(Source))]` | class/struct | Generate a mapper from Source to this type |
| `[MapTo(typeof(Target))]` | class/struct | Generate a mapper from this type to Target |
| `[MapProperty("SourceProp")]` | property | Map from a differently-named source property |
| `[IgnoreMapping]` | property | Exclude from automatic mapping |
| `[OneWay]` | class/struct | Suppress reverse mapper generation |

## Key Design Decisions

- **Contracts + attributes, no runtime** -- `IMapper<TSource, TTarget>` and the attributes define the contract; a source generator (separate project) emits concrete mappers
- **Covariant + contravariant** -- `IMapper<in TSource, out TTarget>` enables polymorphic usage
- **AllowMultiple on class attributes** -- a type can map to/from multiple types (`[MapFrom(typeof(A))][MapFrom(typeof(B))]`)
- **netstandard2.0** -- attributes and interface work with .NET Framework, .NET 6+, .NET 10
- **No dependency on Roslyn** -- the attributes project has zero dependencies; the SG references it

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project structure, type design, attribute model, SG integration pattern
- [HOW-TO.md](doc/HOW-TO.md) -- defining mappings, property renaming, ignoring, one-way, manual mappers, testing
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why source generation over reflection, why typed interface, why separate attributes

## Building

```bash
dotnet build Mapper/FrenchExDev.Net.Mapper.slnx
dotnet test Mapper/FrenchExDev.Net.Mapper.slnx
```
