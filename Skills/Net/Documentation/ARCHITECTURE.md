# FrenchExDev .NET Solution Directory Architecture

## Audience
This document is for internal maintainers working on the FrenchExDev .NET codebase.

## Scope
This is the greenfield architecture standard for creating new packages under `Net/FrenchExDev`.

## Non-Scope
This document does not define retrofit or migration procedures for existing packages.

## Why This Convention Exists
The convention optimizes for:
- Predictable package onboarding.
- Fast navigation in IDE and CLI.
- Clear separation of production code, test support code, and executable tests.
- Consistent solution wiring at package and aggregate levels.
- Centralized dependency version management.

## Canonical Top-Level Layout
Every FrenchExDev .NET repository root follows this shape:

```text
Net/
  FrenchExDev/
    Directory.Packages.props
    FrenchExDev.Net.slnx
    <PackageA>/
    <PackageB>/
    <PackageN>/
```

### Top-Level Requirements
- `Directory.Packages.props` is required and must centrally manage package versions.
- `FrenchExDev.Net.slnx` is required and must aggregate all package projects.
- Each package lives in its own folder directly under `Net/FrenchExDev`.

## Canonical Package Layout
Each package folder must follow this exact structure:

```text
Net/FrenchExDev/<Package>/
  doc/
    ARCHITECTURE.md
    HOW-TO.md
    INDEX.md
  src/
    FrenchExDev.Net.<Package>/
      FrenchExDev.Net.<Package>.csproj
      *.cs
    FrenchExDev.Net.<Package>.Testing/
      FrenchExDev.Net.<Package>.Testing.csproj
      *.cs
  test/
    FrenchExDev.Net.<Package>.Tests/
      FrenchExDev.Net.<Package>.Tests.csproj
      *Tests.cs
  FrenchExDev.Net.<Package>.slnx
```

### Package-Level Requirements
- `doc/`, `src/`, and `test/` directories are mandatory.
- `FrenchExDev.Net.<Package>.slnx` is mandatory for every package.
- The documentation placeholders `ARCHITECTURE.md`, `HOW-TO.md`, and `INDEX.md` are mandatory in `doc/`.

## Naming Contract
Use these project names exactly:
- Runtime project: `FrenchExDev.Net.<Package>`
- Testing helper project: `FrenchExDev.Net.<Package>.Testing`
- Executable test project: `FrenchExDev.Net.<Package>.Tests`

Use these solution names:
- Aggregate solution: `FrenchExDev.Net.slnx`
- Package solution: `FrenchExDev.Net.<Package>.slnx`

## Directory and Project Responsibilities
### `doc/`
- Holds package-level documentation.
- `ARCHITECTURE.md` explains internal design and boundaries.
- `HOW-TO.md` explains usage and operational tasks.
- `INDEX.md` is the local table of contents.

### `src/FrenchExDev.Net.<Package>`
- Contains production code only.
- Defines public package contracts and runtime implementations.
- Must not contain test fixtures or fake data builders.

### `src/FrenchExDev.Net.<Package>.Testing`
- Contains reusable test-support code.
- Typical content: builders, fakes, stubs, test data factories, assertion helpers.
- Must not be used as a place for production runtime features.

### `test/FrenchExDev.Net.<Package>.Tests`
- Contains executable tests (unit/integration as applicable).
- May reference `FrenchExDev.Net.<Package>` and `FrenchExDev.Net.<Package>.Testing`.
- Should remain focused on test intent, not reusable helper infrastructure.

## Solution Wiring Rules
### Aggregate Solution (`Net/FrenchExDev/FrenchExDev.Net.slnx`)
- Must include all package projects across all packages.
- Must preserve logical folder grouping by package and by `doc/src/test`.
- Must use relative project paths.

### Package Solution (`Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx`)
- Must include package-local `src` and `test` projects.
- Must use relative project paths anchored in the package folder.
- Must be independently usable for a package-only development loop.

### Relative Path Convention
- Keep paths relative and local to the owning solution.
- Prefer path shapes already present in existing `.slnx` files.

## Build, Package, and Test Conventions
### Project-Level Build Baseline
- `TargetFramework` must be `net10.0`.
- `Nullable` must be enabled.
- `ImplicitUsings` must be enabled.

### Dependency Versioning
- Versions are defined in `Net/FrenchExDev/Directory.Packages.props`.
- In project files, `<PackageReference Include="..."/>` must not set inline `Version`.

### Test Stack Baseline
Test projects must include:
- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `coverlet.collector`

Test projects must set:
- `<IsPackable>false</IsPackable>`

## Greenfield Workflow: Create a New Package
The example below creates a package named `Parser` from repository root.

### 1. Create the Package Skeleton
```powershell
New-Item -ItemType Directory -Force -Path `
  "Net/FrenchExDev/Parser/doc", `
  "Net/FrenchExDev/Parser/src", `
  "Net/FrenchExDev/Parser/test" | Out-Null
```

### 2. Create the Three Projects
```powershell
dotnet new classlib `
  -n FrenchExDev.Net.Parser `
  -o Net/FrenchExDev/Parser/src/FrenchExDev.Net.Parser

dotnet new classlib `
  -n FrenchExDev.Net.Parser.Testing `
  -o Net/FrenchExDev/Parser/src/FrenchExDev.Net.Parser.Testing

dotnet new xunit `
  -n FrenchExDev.Net.Parser.Tests `
  -o Net/FrenchExDev/Parser/test/FrenchExDev.Net.Parser.Tests
```

### 3. Create the Package Solution (`.slnx`)
```powershell
dotnet new sln `
  -n FrenchExDev.Net.Parser `
  -o Net/FrenchExDev/Parser `
  --format slnx
```

### 4. Add Projects to the Package Solution
```powershell
dotnet sln Net/FrenchExDev/Parser/FrenchExDev.Net.Parser.slnx add `
  Net/FrenchExDev/Parser/src/FrenchExDev.Net.Parser/FrenchExDev.Net.Parser.csproj `
  --solution-folder src

dotnet sln Net/FrenchExDev/Parser/FrenchExDev.Net.Parser.slnx add `
  Net/FrenchExDev/Parser/src/FrenchExDev.Net.Parser.Testing/FrenchExDev.Net.Parser.Testing.csproj `
  --solution-folder src

dotnet sln Net/FrenchExDev/Parser/FrenchExDev.Net.Parser.slnx add `
  Net/FrenchExDev/Parser/test/FrenchExDev.Net.Parser.Tests/FrenchExDev.Net.Parser.Tests.csproj `
  --solution-folder test
```

### 5. Add Projects to the Aggregate Solution
```powershell
dotnet sln Net/FrenchExDev/FrenchExDev.Net.slnx add `
  Net/FrenchExDev/Parser/src/FrenchExDev.Net.Parser/FrenchExDev.Net.Parser.csproj `
  --solution-folder Parser/src

dotnet sln Net/FrenchExDev/FrenchExDev.Net.slnx add `
  Net/FrenchExDev/Parser/src/FrenchExDev.Net.Parser.Testing/FrenchExDev.Net.Parser.Testing.csproj `
  --solution-folder Parser/src

dotnet sln Net/FrenchExDev/FrenchExDev.Net.slnx add `
  Net/FrenchExDev/Parser/test/FrenchExDev.Net.Parser.Tests/FrenchExDev.Net.Parser.Tests.csproj `
  --solution-folder Parser/test
```

### 6. Create the Baseline Package Docs
```powershell
Set-Content Net/FrenchExDev/Parser/doc/ARCHITECTURE.md "# Parser Architecture"
Set-Content Net/FrenchExDev/Parser/doc/HOW-TO.md "# Parser HOW-TO"
Set-Content Net/FrenchExDev/Parser/doc/INDEX.md "# Parser Documentation Index"
```

### 7. Wire Project References
Tests should reference runtime and testing helper projects:

```powershell
dotnet add Net/FrenchExDev/Parser/test/FrenchExDev.Net.Parser.Tests/FrenchExDev.Net.Parser.Tests.csproj reference `
  Net/FrenchExDev/Parser/src/FrenchExDev.Net.Parser/FrenchExDev.Net.Parser.csproj `
  Net/FrenchExDev/Parser/src/FrenchExDev.Net.Parser.Testing/FrenchExDev.Net.Parser.Testing.csproj
```

For cross-package runtime dependencies, reference runtime-to-runtime only.  
Example: `Builder` depends on `Result`:

```powershell
dotnet add Net/FrenchExDev/Builder/src/FrenchExDev.Net.Builder/FrenchExDev.Net.Builder.csproj reference `
  Net/FrenchExDev/Result/src/FrenchExDev.Net.Result/FrenchExDev.Net.Result.csproj
```

## Guardrails and Anti-Patterns
### Guardrails
- Always create package `.slnx` when creating a package.
- Keep package versions centralized in `Directory.Packages.props`.
- Keep helper-only test infrastructure in `.Testing` projects.
- Keep executable tests in `test/<Package>.Tests`.
- Keep aggregate solution in sync when adding/removing projects.

### Anti-Patterns
- Missing `FrenchExDev.Net.<Package>.slnx`.
- Inline `Version` on `<PackageReference>` when central management is enabled.
- Test helper classes inside runtime project.
- Tests created outside `test/FrenchExDev.Net.<Package>.Tests`.
- Cross-package references to another package’s `.Testing` project.

## Architecture Validation Checklist
Use this checklist before considering a new package compliant.

### Structure Checks
- [ ] Package folder exists under `Net/FrenchExDev/<Package>`.
- [ ] `doc/`, `src/`, and `test/` directories all exist.
- [ ] `doc/ARCHITECTURE.md`, `doc/HOW-TO.md`, `doc/INDEX.md` all exist.
- [ ] `FrenchExDev.Net.<Package>.slnx` exists.

### Naming Checks
- [ ] Runtime project name matches `FrenchExDev.Net.<Package>`.
- [ ] Testing helper project name matches `FrenchExDev.Net.<Package>.Testing`.
- [ ] Test project name matches `FrenchExDev.Net.<Package>.Tests`.

### Solution Membership Checks
- [ ] Package `.slnx` lists package-local `src` and `test` projects.
- [ ] Aggregate `.slnx` lists all projects of the package.
- [ ] `.slnx` entries use relative paths.

### Build and Dependency Checks
- [ ] Projects target `net10.0`.
- [ ] `Nullable` and `ImplicitUsings` are enabled.
- [ ] Test project sets `IsPackable=false`.
- [ ] Test project references xUnit stack and `Microsoft.NET.Test.Sdk`.
- [ ] No inline package versions are used when centrally managed.

### Optional CLI Verification Commands
```powershell
dotnet sln Net/FrenchExDev/Parser/FrenchExDev.Net.Parser.slnx list --solution-folders
dotnet sln Net/FrenchExDev/FrenchExDev.Net.slnx list --solution-folders
dotnet test Net/FrenchExDev/Parser/FrenchExDev.Net.Parser.slnx
```

## Plan Validation Scenarios
### 1. New Package Bootstrap Scenario (`Parser`)
Validation intent:
- Confirm required directory tree exists.
- Confirm three projects exist with correct names.
- Confirm package and aggregate solutions include the projects.

Expected result:
- All checklist items pass for `Parser`.

### 2. Dependency Scenario (`Builder` depends on `Result`)
Validation intent:
- Confirm dependency is runtime-to-runtime.
- Confirm project reference path remains relative and stable.

Expected result:
- `Builder` runtime project references `Result` runtime project only.
- No solution structure drift is introduced.

### 3. Package Isolation Scenario
Validation intent:
- Confirm package `.slnx` can be opened/tested independently.

Expected result:
- Running build/test through package `.slnx` succeeds without requiring manual aggregate-solution edits.

### 4. Compliance Failure Scenario
Validation intent:
- Intentionally remove one required element (for example, missing `.Testing` project or missing package `.slnx`) and rerun checklist.

Expected result:
- Checklist fails immediately and pinpoints the missing architecture requirement.

## Assumptions and Defaults for This Standard
- The canonical root for these rules is `Net/FrenchExDev`.
- The standard language for these architecture docs is English.
- This is a maintainer-facing standard, not an external onboarding guide.
- The standard is prescriptive enough to enforce consistency but explained in narrative form.
- Every package is expected to own a package-level `.slnx`.
- Greenfield package creation is in scope; retrofit procedures are out of scope.
- Existing observable conventions are treated as baseline defaults: aggregate `.slnx`, `doc/src/test` split, centralized package versions, `net10.0`, and xUnit-based testing.
