# CENTRAL-PACKAGE-MANAGEMENT — Philosophy

Central Package Management (CPM) puts every NuGet package version in **one file**:
[Directory.Packages.props](../../../../Net/FrenchExDev/Directory.Packages.props).
Project files reference packages by name only — no inline versions, ever.

## Why one file

1. **One source of truth.** Every project sees the same version of every package. No
   drift between projects, no surprise upgrades, no "works on my machine."
2. **One upgrade decision.** Bumping a package is one edit. The change applies to all
   consumers atomically.
3. **Auditable.** A reviewer reading a PR sees the version change in one place and can
   reason about its blast radius.
4. **Compatible with source generators.** Source generators are sensitive to Roslyn
   versions. CPM forces every project to share the same Roslyn release, eliminating
   the "works in build A, fails in build B" failure mode.

## What this looks like

`Directory.Packages.props` (at the monorepo `Net/FrenchExDev/` root):

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="5.3.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="YamlDotNet" Version="16.3.3" />
    <!-- ... -->
  </ItemGroup>
</Project>
```

A consumer `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="xunit" />          <!-- correct: no Version= -->
  <PackageReference Include="YamlDotNet" />
</ItemGroup>
```

If a developer adds `Version="2.10.0"` to a `<PackageReference>`, MSBuild does not
warn — it silently overrides the central version. CPM compliance is therefore enforced
by code review and CI lint, not by the SDK.

## Roslyn pinning is the most important rule

Source generator projects link against `Microsoft.CodeAnalysis.CSharp`. The version
they use at compile time must match what their generator code calls into at runtime
(during host project compilation). A mismatch produces opaque errors like:

```
error CS0103: The name 'GetAttributeData' does not exist in the current context
```

The fix is **always** to align the version in `Directory.Packages.props` with the
APIs the generator uses, then rebuild the generator project. Never paper over the
issue by adding `Version=` overrides in the generator's `.csproj`.

## When CPM is the wrong tool

CPM does not solve:

- **Source generator build-time references** — those use `<PackageReference ... PrivateAssets="all">`
  and still benefit from CPM, but the generator pin must be compatible with the host.
- **Multi-targeting with conditional packages** — use `<ItemGroup Condition="...">`
  in the consumer `.csproj`, not `<PackageVersion Condition="...">` in the central
  file.
- **Per-environment overrides** — CPM is global. For local-only packages, see
  [LOCAL-NUGET-REGISTRY](../LOCAL-NUGET-REGISTRY/PHILOSOPHY.md).
