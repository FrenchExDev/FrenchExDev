# HttpClient — Claude Context

Sparsely documented package — README and `doc/` are empty. Source contains `FrenchExDev.Net.HttpClient` (a single `Code.cs`) plus a `.Testing` sibling. Inferred purpose from layout: a thin wrapper / abstraction over `System.Net.Http.HttpClient` with a paired testing helper assembly.

## Package docs
- [README](README.md) — empty
- `doc/` — empty

## Relevant skills
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Hand-Written Fakes](../../../Skills/Net/Programming/HAND-WRITTEN-FAKES/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- No `.slnx` at the package root; reference the runtime + testing csproj directly.

## Notes for Claude
- This package is a stub — `Code.cs` is essentially empty. Confirm intent with the user before scaffolding a design.
- The `.Testing` sibling assembly mirrors the monorepo convention: production interface in the runtime project, hand-written fakes / harnesses in `*.Testing`.
- Do not pull in `Moq` / `NSubstitute` here — the convention is hand-written fakes (see HAND-WRITTEN-FAKES skill).
- Empty README/doc means do not invent architectural claims; ask first.
