# Injectable — Claude Context

Attribute-driven source generators for DI registration. `[Injectable]` on a class or interface declares its lifetime; container-specific generators (Microsoft DI, DryIoc) emit a single `Add{Assembly}Injectables()` extension method per assembly at compile time. Zero runtime overhead.

## Package docs
- [README](README.md)
- [Why](doc/WHY.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Example Generated Code](doc/EXAMPLE-GENERATED-CODE.md)

## Relevant skills
- [INJECTABLE-DI](../../../Skills/Net/Programming/INJECTABLE-DI/PHILOSOPHY.md)
- [SG](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Injectable.slnx`

## Notes for Claude
- **Pre-existing technical debt**: SG assembly loading issue — `SourceGenerator.Lib` DLL is not found when used as a project-referenced analyzer. Do NOT attempt to "fix" this. The workaround is the `<TargetsForTfmSpecificContentInPackage>` packing trick; live with it.
- `Injectable.Attributes` has zero dependencies on any DI container — it's container-agnostic by design. Never make it reference Microsoft.Extensions.DependencyInjection or DryIoc.
- `SourceGenerator.Lib` has NO Roslyn dependency. Both Microsoft and DryIoc generators link its source files (not project reference). This is the pattern that makes adding new containers cheap.
- `[Injectable]` works on BOTH classes AND interfaces. When on an interface, all implementations auto-register with the declared scope — no class attribute needed.
- INJECT004 is an ERROR (not warning) when implementation scope conflicts with interface contract. That's intentional — it's always a bug.
- `[Injectable]` has `AllowMultiple = true` so a class can register under multiple interfaces or keys.
- Generated method name is `Add{SanitizedAssemblyName}Injectables()` — derived from assembly name via PascalCase sanitization.
- 47 tests across Microsoft.Tests, DryIoc.Tests, and SourceGenerator.Lib.Tests.
