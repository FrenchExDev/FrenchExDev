# IEC61499.2 — Claude Context

**Master architectural specification** for an industrial-grade IEC 61499 distributed control platform: source-generated, AOT-compiled function blocks in C#/.NET with a 12-extension VSCode IDE covering design, deployment, security, runtime monitoring, debugging, and simulation.

**Status: design / specification phase.** Comprehensive planning documents exist; no source code yet.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [PLAN.md](doc/PLAN.md) and `PLAN-ext-01..12-*.md` (per-extension specs)

## Relevant skills
- [IEC-61499](../../../Skills/Net/Programming/IEC-61499/PHILOSOPHY.md)
- [FINITE-STATE-MACHINE](../../../Skills/Net/Programming/FINITE-STATE-MACHINE/PHILOSOPHY.md)
- [SG](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- No solution file yet — package is in specification phase.

## Notes for Claude
- **Design phase only.** No source code exists yet. Treat the doc/ folder as the source of truth; don't search for `.cs` files.
- The canonical name is `IEC61499.2` — the trailing `.2` matters. Sibling packages: `IEC61499` (legacy v1 essai) and `IFC61499` (planned clean-room production implementation). Do not confuse the three.
- Every ECC will delegate to `FrenchExDev.Net.FiniteStateMachine` — duplicating FSM machinery in this package is forbidden.
- The 13 docs in `doc/` are large. Read selectively: PHILOSOPHY/ARCHITECTURE for context, PLAN for project decomposition, PLAN-ext-NN for individual VSCode extensions.
- Ports must be `static readonly` fields (not properties, not instance fields) so the future SG can discover them at compile time.
- Function block classes will be `partial`.
