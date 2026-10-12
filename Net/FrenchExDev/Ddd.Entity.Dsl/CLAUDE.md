# Ddd.Entity.Dsl — Claude Context

Bridge source generator that maps DDD attributes onto Entity.Dsl attributes. Reads `[AggregateRoot]`, `[Entity]`, `[Composition]`, `[Aggregation]` and emits a partial class with `[MappedEntity]`, `[PrimaryKey]`, `[NavigationProperty]`. The Entity.Dsl SG then picks up the emitted attributes and generates EF Core code. Two-stage generation in one compile.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [ENTITY-DSL](../../../Skills/Net/Programming/ENTITY-DSL/PHILOSOPHY.md)
- [DDD](../../../Skills/Net/Programming/DDD/PHILOSOPHY.md)
- [DSL-FOUNDATIONS](../../../Skills/Net/Programming/DSL-FOUNDATIONS/PHILOSOPHY.md)
- [SG](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Ddd.Entity.Dsl.slnx`

## Notes for Claude
- This is a **bridge** SG, not a monolith. It exists because Ddd and Entity.Dsl are deliberately separate concerns; this SG is the adapter between them. Do not collapse it into Entity.Dsl.
- Two-stage generation: this SG emits `*.DddBridge.g.cs` partials, the Entity.Dsl SG then emits the EF Core config from them. Both stages run in one compile — no separate build step.
- C# partials can't add attributes to existing properties, so the bridge emits **class-level** attributes carrying property names as strings.
- The emitter (`DddEntityDslBridgeEmitter`) lives in `SourceGenerator.Lib` (no Roslyn dep) and is unit-testable. The SG project links it as source files.
- Don't hand-write `[MappedEntity]` / `[NavigationProperty]` declarations on classes the bridge already covers — duplicates will collide.
