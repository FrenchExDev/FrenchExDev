# DIEM-CMF — Claude Context

Declaration-first Content Management Framework: define the domain once via composed sub-DSLs (DDD, Content, Admin, Pages, Workflow), generate everything else. 4-layer customization: ComponentBase (framework) then SubDslBase (virtual hooks) then `.g.cs` (generated) then partial (developer). Three audiences: developers (IDE), editors (browser), DevOps (`dotnet publish`).

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [DDD](../DDD/) — aggregate/invariant modeling
- [DSL-FOUNDATIONS](../DSL-FOUNDATIONS/) — M3 metamodel
- [COMPOSE-BUNDLE](../COMPOSE-BUNDLE/) — schema-driven generation
- [BUILDER-PATTERN](../BUILDER-PATTERN/) — construction

## Related packages
- [`FrenchExDev.Net.Diem.Core`](../../../../Net/FrenchExDev/Diem/)
- [`FrenchExDev.Net.Diem.Content`](../../../../Net/FrenchExDev/Diem/)
- [`FrenchExDev.Net.Diem.Admin`](../../../../Net/FrenchExDev/Diem/)
- [`FrenchExDev.Net.Diem.Pages`](../../../../Net/FrenchExDev/Diem/)
- [`FrenchExDev.Net.Diem.Workflow`](../../../../Net/FrenchExDev/Diem/)

## Notes for Claude
- Never edit `*.g.cs` files — use Layer 4 partial classes to override virtual hooks
- Every built-in (parts, blocks, widgets) is its own project — no internal framework magic
- Metamodel is self-describing — no central registry file; `[MetaConcept]` annotations auto-discovered
- Parts are horizontal (cross-cutting: SEO, timestamps, soft-delete via `[HasPart]`)
- Blocks are vertical (structured content: Hero, RichText via `[StreamField]`)
- Pages are records (editors create in DB); widgets are code (developers declare in C#)
- Type-safe end-to-end: DSL then DB column then API DTO then admin form (compile-time checked)
