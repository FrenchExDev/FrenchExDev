# Diem — Claude Context

A declaration-first Content Management Framework for .NET 10: developers write attributed C#, Roslyn source generators emit the entire stack (admin CRUD, page widgets, workflows, persistence, routing).

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Plan](doc/PLAN.md)

## Relevant skills
- [DIEM-CMF](../../../Skills/Net/Programming/DIEM-CMF/PHILOSOPHY.md)
- [DDD](../../../Skills/Net/Programming/DDD/PHILOSOPHY.md)
- [DSL Foundations](../../../Skills/Net/Programming/DSL-FOUNDATIONS/PHILOSOPHY.md)
- [Builder Pattern](../../../Skills/Net/Programming/BUILDER-PATTERN/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Diem.slnx` (62 projects)

## Notes for Claude
- Diem is a CMF, not a CMS — it starts empty; never add "default" content types or themes.
- Sub-DSLs (DDD, Content, Admin, Pages, Workflow) are separate projects and compose on a single type — never merge them.
- Generated `*.g.cs` files are regenerated every build. Developer customization lives in Layer 4 partial classes (e.g. `ArticleComponents.cs`), not Layer 3 (`ArticleComponents.g.cs`).
- Every built-in part/block/widget is its own project and uses only the same public attributes a developer would use — there is no internal API.
- `VersionablePart` is *temporal* versioning (history), not draft/publish workflow. Workflow stages live in the Workflow sub-DSL.
- Pages are database records, not source files. Developers register widgets via `[PageWidget]`; editors place them via the admin UI.
- Built on `FrenchExDev.Net.Dsl` (M3 metamodel) and `FrenchExDev.Net.Ddd` — never duplicate metamodel primitives here.
