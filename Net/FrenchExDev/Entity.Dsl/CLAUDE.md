# Entity.Dsl — Claude Context

Attribute-driven Entity Framework Core configuration generator. Reads `[MappedEntity]`, `[PrimaryKey]`, `[NavigationProperty]`, `[Timestampable]`, `[SoftDeletable]`, `[DbContext]` and emits `IEntityTypeConfiguration<T>`, repositories, UoW, and behavior partials. Domain layer stays EF-Core-free.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)

## Relevant skills
- [ENTITY-DSL](../../../Skills/Net/Programming/ENTITY-DSL/PHILOSOPHY.md)
- [DSL-FOUNDATIONS](../../../Skills/Net/Programming/DSL-FOUNDATIONS/PHILOSOPHY.md)
- [SG](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Entity.Dsl.slnx`

## Notes for Claude
- Domain assembly **never** references `Microsoft.EntityFrameworkCore` — only `Entity.Dsl.Attributes`. If you find yourself wanting to import EF Core into a model project, you're in the wrong project.
- C# partials cannot add attributes to **existing** properties. Class-level `[PrimaryKey("Id")]` and `[NavigationProperty("Name", ...)]` carry property names as strings; the SG resolves them at emission time.
- Generation Gap pattern is mandatory: never edit `*.g.cs` files. Override the developer-owned partial stub instead, calling `base.Configure(builder)` first.
- Lifecycle convention: `[Composition]` → Cascade, `[Aggregation]` → Restrict, `[Association]` → NoAction. Don't override globally — change the attribute or the specific `OnDelete`.
- Generated repositories/UoW use `[Injectable]` from the Injectable package; only `AddXxxDbContext` is a dedicated extension.
- Behavior properties (CreatedAt, DeletedAt, etc.) live in `*.Behaviors.g.cs` partials. Never hand-write them.
- Entity classes must be `partial`. The build will fail if they aren't.
