# SOLUTION-LAYOUT — Requirements

Every package in [Net/FrenchExDev/](../../../../Net/FrenchExDev/) **must** satisfy the
following constraints. The list is normative — deviations are bugs.

## Directories

- `doc/`, `src/`, `test/` all exist at the package root.
- `doc/` contains at least `ARCHITECTURE.md`, `HOW-TO.md`, and `INDEX.md`. `PHILOSOPHY.md`
  is encouraged for non-trivial packages.
- `CLAUDE.md` and `README.md` exist at the package root.

## Projects

- Exactly three C# projects per package: Runtime, Testing-support, Tests.
- Runtime project name: `FrenchExDev.Net.<Package>`.
- Testing-support project name: `FrenchExDev.Net.<Package>.Testing`.
- Tests project name: `FrenchExDev.Net.<Package>.Tests`.
- All projects use the canonical `csproj` shape (see
  [ARCHITECTURE.md](ARCHITECTURE.md)).

## Target frameworks

- Runtime project: `net10.0` or `netstandard2.0;net10.0` (libraries that downstream
  generators may consume from `netstandard2.0`).
- Testing-support project: `netstandard2.0;net10.0`.
- Tests project: `net10.0` only.

## Project properties

- `<Nullable>enable</Nullable>` everywhere.
- `<ImplicitUsings>enable</ImplicitUsings>` everywhere.
- `<IsPackable>false</IsPackable>` on the Tests project.

## Package references

- **No inline `Version=`** on any `<PackageReference>`. Versions live in
  [Directory.Packages.props](../../../../Net/FrenchExDev/Directory.Packages.props).
- Tests reference `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`,
  `coverlet.collector`.

## Solution files

- One `FrenchExDev.Net.<Package>.slnx` at each package root containing only the
  package's three projects.
- The aggregate `Net/FrenchExDev/FrenchExDev.Net.slnx` includes every package's three
  projects, organised by package name.
- Solution files use `.slnx` format — never the legacy `.sln` format.
- Project paths in solution files are relative.

## Cross-package dependencies

- Runtime → Runtime only.
- Runtime → another package's `.Testing` is forbidden.
- Tests → another package's `.Testing` is allowed for shared fakes.
- Tests → another package's runtime directly is discouraged; prefer routing through
  the Testing project.

## Documentation

- `doc/ARCHITECTURE.md` exists and follows the format defined in
  [../../Documentation/ARCHITECTURE.md](../../Documentation/ARCHITECTURE.md).
- `doc/HOW-TO.md` exists and follows the format defined in
  [../../Documentation/HOW-TO.md](../../Documentation/HOW-TO.md).
- `README.md` follows the format defined in
  [../../Documentation/HOW-TO-README.md](../../Documentation/HOW-TO-README.md).
- `CLAUDE.md` follows the template at
  [../../Documentation/CLAUDE-MD-TEMPLATE.md](../../Documentation/CLAUDE-MD-TEMPLATE.md).
