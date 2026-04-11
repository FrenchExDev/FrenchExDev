# Skills/Net/Documentation — Claude Context

Meta-docs that define how every .NET package in the monorepo is organized and
documented. These are governance standards, not code patterns.

## Files

- [INDEX.md](INDEX.md) — entry point linking all documentation files
- [ARCHITECTURE.md](ARCHITECTURE.md) — canonical package layout: 3-folder (doc/src/test),
  3 projects (Runtime/Testing/Tests), naming, target frameworks, solution wiring
- [HOW-TO.md](HOW-TO.md) — how to write a package `doc/HOW-TO.md` (task-oriented,
  copy-paste commands, verification + troubleshooting per task)
- [HOW-TO-README.md](HOW-TO-README.md) — how to write a package `README.md`
  (concise hub, quick start, links to deeper docs)
- [CLAUDE-MD-TEMPLATE.md](CLAUDE-MD-TEMPLATE.md) — how to write a package `CLAUDE.md`
  (~20-40 lines, pointers not content, "Notes for Claude" = only original section)

## Notes for Claude

- These files prescribe standards for package docs under `Net/FrenchExDev/`.
  For pattern-level skills, see [../Programming/](../Programming/).
- ARCHITECTURE.md here defines the *directory structure rules*; each skill's
  ARCHITECTURE.md describes that *pattern's internal structure*.
- CLAUDE-MD-TEMPLATE.md applies to packages, not skill directories.
