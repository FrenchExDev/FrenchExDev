# FrenchExDev .NET Monorepo — Claude Context

This is the root context file for the .NET monorepo. **Read this first**, then read the
package-specific `CLAUDE.md` for whatever package you are working in.

## Layout

- [Net/FrenchExDev/](.) — 44 .NET packages, each with `src/`, `test/`, `doc/`, and a
  `FrenchExDev.Net.<Package>.slnx` solution file.
- [Skills/Net/Programming/](../../Skills/Net/Programming/) — reusable skills (one folder
  per pattern). Start with [INDEX.md](../../Skills/Net/Programming/INDEX.md).
- [Skills/Net/Documentation/](../../Skills/Net/Documentation/) — meta-docs (canonical
  package layout, README/HOW-TO conventions, CLAUDE.md template).

## Canonical package layout

Every package follows this shape — see
[SOLUTION-LAYOUT](../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md):

```
Net/FrenchExDev/<Package>/
  doc/{ARCHITECTURE,HOW-TO,PHILOSOPHY,INDEX}.md
  src/FrenchExDev.Net.<Package>/
  src/FrenchExDev.Net.<Package>.Testing/   (optional)
  test/FrenchExDev.Net.<Package>.Tests/
  FrenchExDev.Net.<Package>.slnx
  CLAUDE.md
  README.md
```

## Global rules

- **Central Package Management is active.** Never add `Version=` to `<PackageReference>`
  in `.csproj`. Add the version to [Directory.Packages.props](Directory.Packages.props).
  See [CENTRAL-PACKAGE-MANAGEMENT](../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md).
- **All files are UTF-8.** Use the standard `Write` / `Edit` tools — no encoding tricks.
- **.NET SDK is 10.0.200-preview.** `NETSDK1057` warning is normal, not an error.
- **Roslyn pinning**: `Microsoft.CodeAnalysis.CSharp 5.3.0`,
  `Microsoft.CodeAnalysis.Analyzers 5.3.0`. Source generators target `netstandard2.0`.
- **Local NuGet registry** at `C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__`
  must exist for cross-package consumption. See
  [LOCAL-NUGET-REGISTRY](../../Skills/Net/Programming/LOCAL-NUGET-REGISTRY/HOW-TO.md).
- **Never run Design project downloads.** The user runs scrape/download/Design CLIs
  manually — they hit external services and take a long time.
- **Hand-written fakes, not mocking frameworks.** See
  [HAND-WRITTEN-FAKES](../../Skills/Net/Programming/HAND-WRITTEN-FAKES/PHILOSOPHY.md).

## Where to look for what

| If you need to... | Read |
|---|---|
| Understand a package's purpose | `<Package>/README.md` then `<Package>/CLAUDE.md` |
| Understand a package's internals | `<Package>/doc/ARCHITECTURE.md` |
| Add a feature to a package | `<Package>/doc/HOW-TO.md` + the relevant skill |
| Apply a cross-cutting pattern | [Skills/Net/Programming/INDEX.md](../../Skills/Net/Programming/INDEX.md) |
| Create a new package | [SOLUTION-LAYOUT/HOW-TO.md](../../Skills/Net/Programming/SOLUTION-LAYOUT/HOW-TO.md) |
| Write a `CLAUDE.md` for a new package | [Skills/Net/Documentation/CLAUDE-MD-TEMPLATE.md](../../Skills/Net/Documentation/CLAUDE-MD-TEMPLATE.md) |

## Build/test commands

```
dotnet build Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx
dotnet test  Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx
```

For the QualityGate CLI:
```
dotnet run --project Net/FrenchExDev/QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test
```
