# Ddd.Entity.Dsl

Bridge source generator that maps DDD attributes (`[AggregateRoot]`, `[Entity]`) to Entity.Dsl attributes (`[MappedEntity]`). When a class is decorated with `[AggregateRoot]` or `[Entity]` from the Ddd project, this SG emits a partial class with `[MappedEntity]`, which the Entity.Dsl source generator then picks up to generate EF Core persistence code. The bridge eliminates manual duplication -- DDD users get Entity.Dsl code generation for free.

## Quick Start

```csharp
// 1. Developer writes DDD domain model
[AggregateRoot]
public partial class Order
{
    [EntityId]
    public Guid Id { get; set; }

    [Composition]
    public List<OrderLine> Lines { get; set; } = [];
}

// 2. Ddd.Entity.Dsl SG generates:
//    Order.DddBridge.g.cs:
//      [MappedEntity]
//      public partial class Order { }

// 3. Entity.Dsl SG sees [MappedEntity] and generates EF Core DbContext, config, etc.
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Ddd.Entity.Dsl.SourceGenerator` | netstandard2.0 | Incremental SG: reads `[Entity]`/`[AggregateRoot]`, emits `[MappedEntity]` partial classes |
| `Ddd.Entity.Dsl.SourceGenerator.Lib` | netstandard2.0 | `DddEntityDslBridgeEmitter` + `DddBridgeModel` (no Roslyn dependency, testable) |
| `Ddd.Entity.Dsl.Tests` | net10.0 | 3 xUnit + Shouldly tests for the emitter |

## DDD → Entity.Dsl Mapping

| DDD Attribute | Entity.Dsl Attribute | Planned Mapping |
|---------------|---------------------|-----------------|
| `[AggregateRoot]` | `[MappedEntity]` | Implemented |
| `[Entity]` | `[MappedEntity]` | Implemented |
| `[EntityId]` | `[PrimaryKey]` | Planned (property-level) |
| `[Composition]` | `OnDelete = Cascade` | Planned |
| `[Aggregation]` | `OnDelete = Restrict` | Planned |
| `[Association]` | `OnDelete = NoAction` | Planned |

## Key Design Decisions

- **Bridge SG, not a monolith** -- Ddd and Entity.Dsl are separate concerns; this SG is the adapter between them
- **Lib + SG split** -- the emitter logic lives in a testable Lib (no Roslyn dependency); the SG links it as source files
- **Partial classes** -- the bridge emits a partial class with attributes, so the developer's class and the generated attributes coexist
- **Two-stage generation** -- Ddd.Entity.Dsl emits `[MappedEntity]` → Entity.Dsl reads `[MappedEntity]` and emits EF Core code

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- two-stage SG pipeline, project structure, bridge model, emitter design
- [HOW-TO.md](doc/HOW-TO.md) -- using the bridge, adding the SG, understanding generated output
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why a bridge SG, why not merge Ddd and Entity.Dsl, why partial classes

## Building

```bash
dotnet build Ddd.Entity.Dsl/FrenchExDev.Net.Ddd.Entity.Dsl.slnx
dotnet test Ddd.Entity.Dsl/FrenchExDev.Net.Ddd.Entity.Dsl.slnx
```
