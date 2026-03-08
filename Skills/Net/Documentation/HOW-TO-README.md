# FrenchExDev .NET HOW-TO: How to Create a Package README

## Audience
This guide is for internal maintainers creating or updating a package `README.md` under `Net/FrenchExDev/<Package>/`.

## Scope
This document defines:
- Where a package README lives.
- How to create a package README file.
- How to write useful README content that complements package `doc/` files.
- A reusable README template and review checklist.

## Non-Scope
This document does not:
- Redefine package architecture rules from `Skills/Net/Documentation/ARCHITECTURE.md`.
- Replace package `doc/HOW-TO.md` for task-level procedures.
- Replace package `doc/ARCHITECTURE.md` for deep design rationale.

## What a Package README Is (and Is Not)
### A package README is:
- The first entry point for maintainers and contributors.
- A concise orientation to package purpose, boundaries, and common actions.
- A navigation hub to deeper docs in `doc/`.

### A package README is not:
- A full architecture specification.
- A full operational runbook.
- A long API reference dump.

### Mandatory intent rule
After reading a package README, a maintainer should know:
- What the package does.
- Where code and docs are.
- How to perform the most common next action.

## Relationship to ARCHITECTURE.md, HOW-TO.md, and INDEX.md
For each package:
- `README.md` (package root) is the quick entry point.
- `doc/ARCHITECTURE.md` explains design and boundaries.
- `doc/HOW-TO.md` explains task procedures.
- `doc/INDEX.md` is the documentation table of contents.

### Mandatory link expectations
- `README.md` must link to:
- `./doc/ARCHITECTURE.md`
- `./doc/HOW-TO.md`
- `./doc/INDEX.md`
- `doc/INDEX.md` should link back to package `../README.md` when appropriate.

## When to Create or Update a Package README
Create or refresh `README.md` when:
- A new package is created.
- Package purpose or scope changes.
- Primary setup/build/test commands change.
- Documentation links move or become stale.
- Onboarding friction appears for maintainers.

## Step-by-Step: Create a New Package README
Example package: `Parser`

### 1. Ensure package root exists
Package root path:
- `Net/FrenchExDev/Parser/`

### 2. Create the README file
From repository root:

```powershell
Set-Content "Net/FrenchExDev/Parser/README.md" "# FrenchExDev.Net.Parser"
```

### 3. Add package purpose
Under title, add a one- to two-line description:
- What the package provides.
- Who should use it (internal maintainers/contributors).

### 4. Add quick navigation links
Add links to package docs:

```markdown
- [Architecture](./doc/ARCHITECTURE.md)
- [How-To](./doc/HOW-TO.md)
- [Documentation Index](./doc/INDEX.md)
```

### 5. Add a minimal quick start
Add at least one copy-paste command for a common maintainer action, such as:
- Run package tests.
- Build package solution.
- Add package reference (if applicable).

### 6. Validate terminology and paths
Ensure names and paths align with architecture convention:
- `FrenchExDev.Net.<Package>`
- `FrenchExDev.Net.<Package>.Testing`
- `FrenchExDev.Net.<Package>.Tests`
- `doc/`, `src/`, `test/`, package `.slnx`

## Step-by-Step: Write Useful README Content
Use this structure for each README section.

### 1. Start with identity and purpose
- Package title.
- Short summary paragraph.

### 2. Provide fast navigation
- Keep a small "Documentation" section near the top.
- Link to architecture, HOW-TO, and index docs.

### 3. Provide practical first actions
- Include a "Quick Start" section.
- Use exact commands and paths.
- Prefer commands that work from repository root.

### 4. Keep advanced detail out of README
- Link to `doc/HOW-TO.md` for procedures.
- Link to `doc/ARCHITECTURE.md` for rationale.

### 5. Add verification cues
For each command block, state expected success outcome:
- Build succeeds.
- Tests pass.
- Reference appears in project file.

### 6. Add update discipline
Whenever setup or structure changes:
- Update README links and commands.
- Re-check against package `doc/` docs for consistency.

## Full Copy-Ready Template
Use this as a baseline for `Net/FrenchExDev/<Package>/README.md`.

```markdown
# FrenchExDev.Net.<Package>

Short description: what this package is responsible for in one to two sentences.

## Documentation
- [Architecture](./doc/ARCHITECTURE.md)
- [How-To](./doc/HOW-TO.md)
- [Documentation Index](./doc/INDEX.md)

## Package Layout
- `src/FrenchExDev.Net.<Package>`: runtime code
- `src/FrenchExDev.Net.<Package>.Testing`: reusable test helpers
- `test/FrenchExDev.Net.<Package>.Tests`: executable tests

## Quick Start
### Build package
```powershell
dotnet build Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx
```
Expected result:
- Build succeeds with no critical errors.

### Run package tests
```powershell
dotnet test Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx
```
Expected result:
- Tests execute and pass.

## Common Tasks
- For step-by-step maintainer tasks, see [How-To](./doc/HOW-TO.md).
- For design and constraints, see [Architecture](./doc/ARCHITECTURE.md).

## Compatibility
- Target framework baseline: `net10.0`

## Notes for Maintainers
- Keep README concise and task-oriented.
- Update links and commands whenever package layout or workflow changes.
```

## Guardrails and Anti-Patterns
### Guardrails
- Keep README concise and discoverable.
- Place README at package root: `Net/FrenchExDev/<Package>/README.md`.
- Use repository-relative links and commands.
- Keep README and `doc/` files consistent.

### Anti-Patterns to avoid
- README with no links to `doc/` files.
- Long architecture prose copied from `ARCHITECTURE.md`.
- Procedure-heavy content copied from `HOW-TO.md`.
- Stale commands or incorrect paths.
- Missing expected outcomes for command blocks.

## Review Checklist
Use this checklist before merging README changes.

- [ ] README exists at `Net/FrenchExDev/<Package>/README.md`.
- [ ] Title matches package name (`FrenchExDev.Net.<Package>`).
- [ ] README links to `./doc/ARCHITECTURE.md`, `./doc/HOW-TO.md`, and `./doc/INDEX.md`.
- [ ] Quick Start commands are copy-paste ready.
- [ ] Each command block has expected outcome text.
- [ ] Paths and naming match `Skills/Net/Documentation/ARCHITECTURE.md`.
- [ ] README does not duplicate full architecture or HOW-TO content.

## Validation Scenarios
### 1. Greenfield package bootstrap (`Parser`)
Validation:
- Create `README.md` from template.
- Verify links to package docs resolve.

Expected result:
- New package has an immediately useful entry-point README.

### 2. Existing package update
Validation:
- Update README commands after workflow change.
- Re-run commands from repo root.

Expected result:
- Commands succeed and README remains accurate.

### 3. Onboarding trial
Validation:
- Ask a maintainer unfamiliar with package to follow README Quick Start.

Expected result:
- Maintainer can build/test package without additional context.

### 4. Drift detection
Validation:
- Intentionally break one link or path and run checklist.

Expected result:
- Checklist catches the issue before merge.

## Assumptions and Defaults
- Package README path is `Net/FrenchExDev/<Package>/README.md`.
- Language standard is English.
- Primary audience is internal maintainers.
- README is concise and navigational, not exhaustive.
- `Skills/Net/Documentation/ARCHITECTURE.md` is the structural source of truth.
- `Skills/Net/Documentation/HOW-TO.md` is the procedure-writing source of truth.
