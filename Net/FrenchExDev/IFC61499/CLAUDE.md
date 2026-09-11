# IFC61499 — Claude Context

**Variant: planned clean-room production implementation** of the IEC 61499 standard, sibling to IEC61499.2 (the master specification) and IEC61499 (the v1 essai).

**Status: stub.** README is empty; doc/ contains placeholder ARCHITECTURE.md / HOW-TO.md / PHILOSOPHY.md / PLAN.md and a `spec/` folder. No source code yet.

## Package docs
- README is currently empty (1 line)
- `doc/ARCHITECTURE.md`, `doc/HOW-TO.md`, `doc/PHILOSOPHY.md`, `doc/PLAN.md` — placeholders
- `doc/spec/` — placeholder

## Relevant skills
- [IEC-61499](../../../Skills/Net/Programming/IEC-61499/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- No solution file yet — package is in stub phase.

## Notes for Claude
- **Stub package.** README is empty; doc/ files are placeholders. Don't rely on package documentation here — read the IEC61499.2 docs for the canonical specification this package will eventually implement.
- The "F" in `IFC61499` is intentional. Three sibling packages exist: `IEC61499` (legacy v1 essai), `IEC61499.2` (master specification), `IFC61499` (this — planned clean-room production implementation). Do not confuse them or "fix" the name.
- When real work starts here, the canonical pattern is in [IEC-61499](../../../Skills/Net/Programming/IEC-61499/PHILOSOPHY.md) and the doc/ folder of `IEC61499.2`.
- Will eventually depend on `FrenchExDev.Net.FiniteStateMachine` for ECC implementation — never duplicate FSM machinery.
