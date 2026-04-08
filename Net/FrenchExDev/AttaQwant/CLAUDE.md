# AttaQwant — Claude Context

Sparsely documented package — README, ARCHITECTURE, and HOW-TO are currently empty. Source consists of a stub `Program.cs` plus an `.Design` companion project, suggesting an early-stage source-generator-based wrapper or DSL project (pattern matches other `.Design`-paired packages in the monorepo).

## Package docs
- [README](README.md) — empty
- [Architecture](doc/ARCHITECTURE.md) — empty
- [How-To](doc/HOW-TO.md) — empty

## Relevant skills
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)
- [DSL Foundations](../../../Skills/Net/Programming/DSL-FOUNDATIONS/PHILOSOPHY.md)
- [Binary Wrapper](../../../Skills/Net/Programming/BINARY-WRAPPER/PHILOSOPHY.md)

## Solution
- `FrenchExDev.Net.AttaQwant.slnx`

## Notes for Claude
- This package is a stub — confirm intent with the user before adding substantial code.
- The presence of `FrenchExDev.Net.AttaQwant.Design/` mirrors the `BinaryWrapper`/`Vagrant`/`Podman` design-project pattern: a Design project drives scrape/codegen for a runtime sibling.
- Do NOT run the Design project's downloads/scrapers — those are user-driven (see user memory `feedback_design_user_runs.md`).
- Empty README/doc means do not infer architecture from the name; ask first.
