# SOLUTION-LAYOUT — Philosophy

Every package in [Net/FrenchExDev/](../../../../Net/FrenchExDev/) follows the same
three-tier shape so that discovery, navigation, and testing are predictable across the
entire monorepo.

## The split

- `doc/` — external-facing contracts (what the package does, how to use it, why it exists)
- `src/` — code boundaries: production code separated from test-support code
- `test/` — executable tests only

A developer opening any package folder should instantly see where to read docs, where to
find runtime code, where test builders live, and where tests run. No ambiguity. No
helpers buried inside production code. **Every package looks the same.**

## Why this matters

When 44 packages all follow the same shape, the value compounds:

- New contributors do not ask "where do I put this?" — they follow the map.
- Tooling (build, test, coverage, mutation, packaging) is generic; one set of rules
  works for every package.
- Cross-package references have predictable shapes: `..\..\..\OtherPackage\src\...`.
- A skill ([SG](../SG/), [BUILDER-PATTERN](../BUILDER-PATTERN/), [BINARY-WRAPPER](../BINARY-WRAPPER/),
  etc.) can describe its conventions once and trust every package to honour them.

## The three projects per package

1. **Runtime** — `FrenchExDev.Net.<Package>` — public contracts, production logic.
2. **Testing support** — `FrenchExDev.Net.<Package>.Testing` — reusable builders, fakes,
   assertion helpers, sample data factories. Never production logic.
3. **Test suite** — `FrenchExDev.Net.<Package>.Tests` — xUnit tests only; references
   both Runtime and Testing-support.

The Testing project is the seam that lets *other* packages reuse a package's fakes and
builders without dragging in test-only NuGet packages (xUnit, coverlet, etc.).

## The two solution files

- **Package solution** — `FrenchExDev.Net.<Package>.slnx` — includes only this package's
  projects. Stands alone. Used for tight inner-loop work on one package.
- **Aggregate solution** — `Net/FrenchExDev/FrenchExDev.Net.slnx` — includes all
  packages organised by package folder. Used for cross-package refactors.

Both `.slnx` formats — never `.sln`.

## Tooling defaults

- Target: `net10.0` for runtime apps; `netstandard2.0;net10.0` for libraries that
  source generators or downstream code may consume from older toolchains.
- `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` everywhere.
- xUnit + `coverlet.collector`. Test projects are `<IsPackable>false</IsPackable>`.
- Central Package Management — see
  [CENTRAL-PACKAGE-MANAGEMENT](../CENTRAL-PACKAGE-MANAGEMENT/PHILOSOPHY.md).
