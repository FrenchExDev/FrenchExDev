# Skills/Net — Claude Context

Reusable skills (patterns, conventions, architecture docs) for the .NET monorepo.
Two sub-trees: meta-documentation standards and pattern-level programming skills.

## Contents

- [Documentation/](Documentation/) — how the monorepo is organized and documented
  (package layout, README/HOW-TO/CLAUDE.md templates)
- [Programming/](Programming/) — 33 pattern-level skills organized by category
  (foundational, engineering principles, code generation, core libraries, CLI tooling,
  compose/orchestration, apps/verticals)

## Notes for Claude

- Every skill directory has its own `CLAUDE.md` — read that first, dive into
  individual files only when needed.
- Skills describe *patterns and conventions*, not packages. For package-specific
  context, see `Net/FrenchExDev/<Package>/CLAUDE.md`.
- `Programming/RESILIENCE-CONVENTIONS.md` is a standalone file at the
  Programming root, not inside a skill directory.
