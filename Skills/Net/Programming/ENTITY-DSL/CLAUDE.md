# ENTITY-DSL — Claude Context

Generate EF Core persistence code from attribute-decorated POCOs. Domain layer has zero EF Core reference. Bridge SG maps DDD attributes onto Entity DSL attributes in a 2-stage generation within one compile.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [DDD](../DDD/) — source attributes
- [BUILDER-PATTERN](../BUILDER-PATTERN/) — construction
- [DESIGN-PHASED-PROJECT](../DESIGN-PHASED-PROJECT/) — phased generation

## Related packages
- [`FrenchExDev.Net.Entity.Dsl.Attributes`](../../../../Net/FrenchExDev/Entity/)
- [`FrenchExDev.Net.Entity.Dsl.Abstractions`](../../../../Net/FrenchExDev/Entity/)
- [`FrenchExDev.Net.Entity.Dsl.SourceGenerator`](../../../../Net/FrenchExDev/Entity/)
- [`FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib`](../../../../Net/FrenchExDev/Entity/)

## Notes for Claude
- Every entity must be `partial` — generated behavior partials require it
- Never reference EF Core from domain assembly — whole point of the pattern
- Never hand-write `IEntityTypeConfiguration<T>` for entities SG covers
- Call `base.Configure(builder)` in overrides — ensures generated config runs first
- Composite primary keys use class-level `[PrimaryKey("Prop1", "Prop2", ...)]`
- Bridge SG is 2-stage: DDD SG -> bridge SG -> Entity DSL SG, all in one compile
- DDD attributes drive persistence defaults: `[Composition]` -> Cascade, `[Aggregation]` -> Restrict, `[Association]` -> NoAction
