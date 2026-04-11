# Skills/Net/Programming — Claude Context

33 reusable pattern-level skills used across the .NET monorepo. Each skill directory
contains 4 prescriptive Markdown files (PHILOSOPHY, ARCHITECTURE, HOW-TO, REQUIREMENTS)
and its own CLAUDE.md summary.

## Index

See [INDEX.md](INDEX.md) for the full categorized listing.

**Categories:** Foundational (3) · Engineering Principles (7) · Code Generation (7) ·
Core Libraries (10) · CLI Tooling (3) · Compose/Orchestration (3) · Apps/Verticals (1)

## Root files

- [INDEX.md](INDEX.md) — master index of all 33 skills by category
- [RESILIENCE-CONVENTIONS.md](RESILIENCE-CONVENTIONS.md) — Polly.Core resilience
  pipeline conventions (retry/circuit-breaker/timeout, named pipelines, Result integration)

## Notes for Claude

- Read a skill's `CLAUDE.md` first — it summarizes all 4 files. Only dive into
  individual files when you need full detail.
- Exception: `SYSTEM.COMMANDLINE/` has 3 non-standard files (WHAT.md, HOW.md, RULES.md).
- Cross-cutting patterns that recur across skills:
  - `Result<T>` for failure values (never exceptions for domain failures)
  - Attribute-driven source generation (4-project pattern)
  - Hand-written fakes (never mocking frameworks)
  - `[SinceVersion]`/`[UntilVersion]` for multi-version awareness
  - `BuilderEmitter` shared across 4+ generators
