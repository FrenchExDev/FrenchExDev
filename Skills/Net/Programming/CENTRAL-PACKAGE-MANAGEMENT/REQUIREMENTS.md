# CENTRAL-PACKAGE-MANAGEMENT — Requirements

## Mandatory rules

- All NuGet package versions are defined in
  [Directory.Packages.props](../../../../Net/FrenchExDev/Directory.Packages.props).
- `Directory.Packages.props` sets `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`.
- No project file may include an inline `Version=` attribute on any `<PackageReference>`.
- New `<PackageVersion>` entries are inserted in alphabetical order within the existing
  `<ItemGroup>`.
- Source generator projects that reference `Microsoft.CodeAnalysis.*` must use the
  centrally-pinned version; the generator's emitter code must compile against that
  same version.
- Roslyn versions across `Microsoft.CodeAnalysis.CSharp`,
  `Microsoft.CodeAnalysis.CSharp.Workspaces`,
  `Microsoft.CodeAnalysis.Analyzers`, and
  `Microsoft.CodeAnalysis.Workspaces.MSBuild` must be identical.

## Forbidden patterns

- Inline `Version=` on `<PackageReference>`.
- Per-project `Directory.Packages.props` overrides — there is one file at
  `Net/FrenchExDev/Directory.Packages.props` and only one.
- Mixing two Roslyn versions across projects (causes opaque source generator failures).
- Adding `<PackageVersion>` entries in random order — alphabetical only.
- Pinning a transitive dependency without a comment explaining why.

## Audit one-liner

```bash
grep -rn 'PackageReference[^>]*Version=' Net/FrenchExDev/**/*.csproj
```

Zero matches = compliant.

## Required `<PackageVersion>` entries

Every package the monorepo consumes must have an entry. Notable required versions:

| Package | Pinned to | Why |
|---|---|---|
| `Microsoft.CodeAnalysis.CSharp` | 5.3.0 | Source generators target this Roslyn release |
| `Microsoft.CodeAnalysis.Analyzers` | 5.3.0 | Must match the C# package |
| `xunit` | latest stable | Test stack |
| `coverlet.collector` | latest stable | Coverage in tests |
