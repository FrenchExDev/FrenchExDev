# FrenchExDev .NET HOW-TO: How to Create a HOW-TO

## Audience
This guide is for internal maintainers who create and maintain package documentation under `Net/FrenchExDev/<Package>/doc/`.

## Scope
This document defines:
- How to create a package `doc/HOW-TO.md` file.
- How to write high-quality operational/task-oriented HOW-TO content.
- The required structure and quality checks for package HOW-TO documents.

## Non-Scope
This document does not:
- Redefine package architecture rules already standardized in `Skills/Net/Documentation/ARCHITECTURE.md`.
- Serve as a language/API reference style guide.
- Define migration procedures for existing legacy documentation.

## What a HOW-TO Is (and Is Not)
### A HOW-TO is:
- Task and outcome driven.
- Focused on repeatable procedures maintainers can execute.
- Explicit about prerequisites, steps, and expected results.
- Actionable through concrete commands, paths, and examples.

### A HOW-TO is not:
- A deep design rationale document (use `ARCHITECTURE.md` for that).
- A pure API reference listing members/types without procedures.
- A generic narrative with no verification steps.
- A dumping ground for unstructured notes.

### Mandatory intent rule
Every major section in a package HOW-TO must answer:  
`What specific task can a maintainer complete after reading this?`

## Relationship to ARCHITECTURE.md and INDEX.md
Within each package `doc/` directory:

- `ARCHITECTURE.md` explains the why:
- Design boundaries.
- Structural choices.
- Conceptual constraints.

- `HOW-TO.md` explains the how:
- Operational and development tasks.
- Step-by-step procedures.
- Verification and troubleshooting.

- `INDEX.md` is the entry point:
- Links to `ARCHITECTURE.md` and `HOW-TO.md`.
- Links to key anchors inside `HOW-TO.md`.

### Mandatory cross-link expectations
- Package `HOW-TO.md` must link to the sibling `ARCHITECTURE.md`.
- Package `INDEX.md` must link to package `HOW-TO.md`.
- If `HOW-TO.md` references architecture constraints, it must link to the relevant architecture section or file.

## When to Create or Update a HOW-TO
Create or update a package HOW-TO when:
- A new package is created.
- A new repeated maintainer task appears.
- Build/test/reference workflows change.
- Solution structure changes (`.slnx`, project paths, folder conventions).
- Existing instructions become stale, ambiguous, or fail validation.

## Step-by-Step: Create a New Package HOW-TO
Example package: `Parser`

### 1. Ensure package docs directory exists
From repository root:

```powershell
New-Item -ItemType Directory -Force -Path "Net/FrenchExDev/Parser/doc" | Out-Null
```

### 2. Create the HOW-TO file
```powershell
Set-Content "Net/FrenchExDev/Parser/doc/HOW-TO.md" "# FrenchExDev.Net.Parser HOW-TO"
```

### 3. Add title and purpose
At top of `doc/HOW-TO.md`, include:
- Package name in title.
- One short purpose statement explaining which tasks this HOW-TO covers.

### 4. Add initial task list and anchors
Create a top section listing initial tasks with anchor links, for example:
- Create package skeleton.
- Add/update project references.
- Build and run tests.
- Validate solution wiring.

### 5. Link from `doc/INDEX.md`
Ensure package `doc/INDEX.md` references the package HOW-TO:

```markdown
- [HOW-TO](./HOW-TO.md)
```

### 6. Validate naming and terminology consistency
Ensure names and paths in package HOW-TO align with architecture convention:
- `FrenchExDev.Net.<Package>`
- `FrenchExDev.Net.<Package>.Testing`
- `FrenchExDev.Net.<Package>.Tests`
- `doc/`, `src/`, `test/`, package `.slnx`

## Step-by-Step: Write Useful HOW-TO Content
Each task section in a package HOW-TO must follow this structure.

### 1. Task title with clear outcome
Use action-oriented titles, e.g.:
- `## Add a Cross-Package Runtime Reference`
- `## Run Package Tests from Package Solution`

### 2. Preconditions / prerequisites
State required context:
- Working directory.
- Required tools/SDK version if relevant.
- Required existing files/projects.

### 3. Exact steps
- Use numbered steps.
- Include copy-paste commands.
- Include exact paths.
- Keep commands minimal and ordered.

### 4. Verification step
Add an `Expected result` subsection for each task:
- What should exist/change.
- What command output state indicates success.

### 5. Failure and recovery notes
Add a `Troubleshooting` subsection:
- Common error.
- Likely cause.
- Corrective action.

### 6. Related links
End each task with `Related` links:
- Relevant architecture document/section.
- Adjacent tasks in the same HOW-TO.
- Package `INDEX.md` if navigation is needed.

## Full Copy-Ready Template
Use this template for package `Net/FrenchExDev/<Package>/doc/HOW-TO.md`.

```markdown
# FrenchExDev.Net.<Package> HOW-TO

Short purpose: operational and development tasks for maintainers of `FrenchExDev.Net.<Package>`.

## Related Documentation
- [Architecture](./ARCHITECTURE.md)
- [Documentation Index](./INDEX.md)

## Tasks
- [Create or verify package skeleton](#create-or-verify-package-skeleton)
- [Add package projects to package solution](#add-package-projects-to-package-solution)
- [Add package projects to aggregate solution](#add-package-projects-to-aggregate-solution)
- [Add or update project references](#add-or-update-project-references)
- [Build and run package tests](#build-and-run-package-tests)
- [Validate package documentation links](#validate-package-documentation-links)

## Create or verify package skeleton
### Goal
Ensure required package structure exists and follows naming conventions.

### Prerequisites
- Repository root is current directory.
- Package name is selected (`<Package>`).

### Steps
1. Create required directories:
```powershell
New-Item -ItemType Directory -Force -Path `
  "Net/FrenchExDev/<Package>/doc", `
  "Net/FrenchExDev/<Package>/src", `
  "Net/FrenchExDev/<Package>/test" | Out-Null
```
2. Confirm required doc placeholders:
```powershell
Set-Content "Net/FrenchExDev/<Package>/doc/ARCHITECTURE.md" "# <Package> Architecture"
Set-Content "Net/FrenchExDev/<Package>/doc/HOW-TO.md" "# FrenchExDev.Net.<Package> HOW-TO"
Set-Content "Net/FrenchExDev/<Package>/doc/INDEX.md" "# <Package> Documentation Index"
```

### Verify
Expected result:
- `doc/`, `src/`, `test/` exist under `Net/FrenchExDev/<Package>`.
- `ARCHITECTURE.md`, `HOW-TO.md`, `INDEX.md` exist in `doc/`.

### Troubleshooting
- Error: path not found.
- Cause: command run from wrong directory.
- Fix: run from repository root and retry.

### Related
- [Architecture](./ARCHITECTURE.md)

## Add package projects to package solution
### Goal
Ensure package `.slnx` contains package-local `src` and `test` projects.

### Prerequisites
- `FrenchExDev.Net.<Package>.slnx` exists in package root.
- Project files exist for runtime, testing helper, and tests.

### Steps
1. Add runtime project:
```powershell
dotnet sln Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx add `
  Net/FrenchExDev/<Package>/src/FrenchExDev.Net.<Package>/FrenchExDev.Net.<Package>.csproj `
  --solution-folder src
```
2. Add testing helper project:
```powershell
dotnet sln Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx add `
  Net/FrenchExDev/<Package>/src/FrenchExDev.Net.<Package>.Testing/FrenchExDev.Net.<Package>.Testing.csproj `
  --solution-folder src
```
3. Add tests project:
```powershell
dotnet sln Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx add `
  Net/FrenchExDev/<Package>/test/FrenchExDev.Net.<Package>.Tests/FrenchExDev.Net.<Package>.Tests.csproj `
  --solution-folder test
```

### Verify
Expected result:
- `dotnet sln ... list --solution-folders` shows all 3 package projects.

### Troubleshooting
- Error: project already exists in solution.
- Cause: duplicate add.
- Fix: verify with `dotnet sln ... list`; do not re-add.

### Related
- [Architecture](./ARCHITECTURE.md)

## Add package projects to aggregate solution
### Goal
Register package projects in `Net/FrenchExDev/FrenchExDev.Net.slnx`.

### Prerequisites
- Aggregate solution exists.
- Package projects already exist.

### Steps
1. Add runtime:
```powershell
dotnet sln Net/FrenchExDev/FrenchExDev.Net.slnx add `
  Net/FrenchExDev/<Package>/src/FrenchExDev.Net.<Package>/FrenchExDev.Net.<Package>.csproj `
  --solution-folder <Package>/src
```
2. Add testing helper:
```powershell
dotnet sln Net/FrenchExDev/FrenchExDev.Net.slnx add `
  Net/FrenchExDev/<Package>/src/FrenchExDev.Net.<Package>.Testing/FrenchExDev.Net.<Package>.Testing.csproj `
  --solution-folder <Package>/src
```
3. Add tests:
```powershell
dotnet sln Net/FrenchExDev/FrenchExDev.Net.slnx add `
  Net/FrenchExDev/<Package>/test/FrenchExDev.Net.<Package>.Tests/FrenchExDev.Net.<Package>.Tests.csproj `
  --solution-folder <Package>/test
```

### Verify
Expected result:
- Aggregate solution list includes all package projects under package solution folders.

### Troubleshooting
- Error: cannot find solution file.
- Cause: wrong path or solution missing.
- Fix: verify `Net/FrenchExDev/FrenchExDev.Net.slnx` exists.

### Related
- [Architecture](./ARCHITECTURE.md)

## Add or update project references
### Goal
Define project dependencies using relative project references.

### Prerequisites
- Source and target projects exist.

### Steps
1. Add test references to runtime and testing helper projects:
```powershell
dotnet add Net/FrenchExDev/<Package>/test/FrenchExDev.Net.<Package>.Tests/FrenchExDev.Net.<Package>.Tests.csproj reference `
  Net/FrenchExDev/<Package>/src/FrenchExDev.Net.<Package>/FrenchExDev.Net.<Package>.csproj `
  Net/FrenchExDev/<Package>/src/FrenchExDev.Net.<Package>.Testing/FrenchExDev.Net.<Package>.Testing.csproj
```
2. Add cross-package runtime dependency (example):
```powershell
dotnet add Net/FrenchExDev/Builder/src/FrenchExDev.Net.Builder/FrenchExDev.Net.Builder.csproj reference `
  Net/FrenchExDev/Result/src/FrenchExDev.Net.Result/FrenchExDev.Net.Result.csproj
```

### Verify
Expected result:
- Referencing `.csproj` files include expected `<ProjectReference>` entries.

### Troubleshooting
- Error: incompatible dependency direction.
- Cause: referencing `.Testing` across packages or incorrect layer usage.
- Fix: keep cross-package references runtime-to-runtime unless explicitly justified.

### Related
- [Architecture](./ARCHITECTURE.md)

## Build and run package tests
### Goal
Validate package compiles and tests execute through package solution.

### Prerequisites
- Package `.slnx` contains all package projects.

### Steps
1. Run build/test:
```powershell
dotnet test Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx
```

### Verify
Expected result:
- Build succeeds.
- Tests run successfully.

### Troubleshooting
- Error: tests not discovered.
- Cause: missing test project in package `.slnx`.
- Fix: add test project to package solution and rerun.

### Related
- [Architecture](./ARCHITECTURE.md)

## Validate package documentation links
### Goal
Ensure docs are navigable and cross-linked.

### Prerequisites
- `ARCHITECTURE.md`, `HOW-TO.md`, `INDEX.md` exist.

### Steps
1. In `INDEX.md`, link both architecture and HOW-TO docs.
2. In `HOW-TO.md`, include `Related Documentation` links section.
3. Verify anchors in task list resolve to matching headings.

### Verify
Expected result:
- No broken local links.
- Maintainers can navigate from index to tasks in one hop.

### Troubleshooting
- Error: broken heading anchor.
- Cause: heading text changed without updating anchor.
- Fix: regenerate anchor link using final heading text.

### Related
- [Documentation Index](./INDEX.md)
- [Architecture](./ARCHITECTURE.md)
```

## Guardrails and Anti-Patterns
### Guardrails
- Keep tasks outcome-driven and repeatable.
- Use exact commands and repository-relative paths.
- Include verification criteria for every task.
- Include troubleshooting for common failures.
- Keep terminology aligned with architecture conventions.

### Anti-Patterns to avoid
- Vague steps without commands.
- Missing expected result/verification outcomes.
- Duplicating deep architecture rationale inside HOW-TO.
- Commands with incorrect working directory assumptions.
- Stale instructions that no longer match `.slnx` or project layout.

## Review Checklist
Use this checklist before finalizing any package HOW-TO.

- [ ] Headings are task-oriented and actionable.
- [ ] Commands are copy-paste ready.
- [ ] Every task includes an explicit expected result.
- [ ] Every task includes troubleshooting guidance.
- [ ] Links to `ARCHITECTURE.md`, `INDEX.md`, and related tasks are valid.
- [ ] Paths and naming align with FrenchExDev conventions.
- [ ] Instructions do not contradict `Skills/Net/Documentation/ARCHITECTURE.md`.

## Validation Scenarios
### 1. Greenfield package HOW-TO bootstrap (`Parser`)
Validation:
- Create a new package HOW-TO from the template.
- Confirm tasks and links are present.

Expected result:
- New package HOW-TO is immediately usable by maintainers.

### 2. Cross-package dependency task documentation (`Builder` -> `Result`)
Validation:
- Add a documented task for runtime-to-runtime reference wiring.
- Confirm command accuracy and expected result clarity.

Expected result:
- Maintainer can apply dependency change without guessing paths.

### 3. Onboarding readability
Validation:
- Ask a maintainer new to the package to execute one task end-to-end.

Expected result:
- Task can be executed without external tribal knowledge.

### 4. Drift detection and correction
Validation:
- Intentionally break one command/path in draft HOW-TO and run review checklist.

Expected result:
- Checklist detects stale instruction before merge.

## Assumptions and Defaults
- Target of this standard is package `doc/HOW-TO.md` files under `Net/FrenchExDev/<Package>/doc/`.
- Language is English.
- Primary audience is internal maintainers.
- Guide is comprehensive, not quickstart-only.
- Scope includes both file creation and content authoring methodology.
- `Skills/Net/Documentation/ARCHITECTURE.md` is the governing architecture reference.
- This standard does not require runtime code changes; it is documentation-focused.
