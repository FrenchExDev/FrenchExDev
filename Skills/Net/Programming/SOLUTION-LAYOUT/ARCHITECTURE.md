# SOLUTION-LAYOUT — Architecture

## Canonical directory tree

```
Net/FrenchExDev/<Package>/
  doc/
    ARCHITECTURE.md      internal design, boundaries
    HOW-TO.md            usage and operational tasks
    PHILOSOPHY.md        why this package exists (optional but encouraged)
    INDEX.md             local table of contents
  src/
    FrenchExDev.Net.<Package>/
      FrenchExDev.Net.<Package>.csproj
      *.cs               production code
    FrenchExDev.Net.<Package>.Testing/
      FrenchExDev.Net.<Package>.Testing.csproj
      *.cs               builders, fakes, sample data
  test/
    FrenchExDev.Net.<Package>.Tests/
      FrenchExDev.Net.<Package>.Tests.csproj
      *Tests.cs          xUnit tests
  CLAUDE.md
  README.md
  FrenchExDev.Net.<Package>.slnx
```

## Project file shapes

### Runtime project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <!-- or: <TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks> for libraries -->
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

### Testing-support project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\FrenchExDev.Net.<Package>\FrenchExDev.Net.<Package>.csproj" />
  </ItemGroup>
</Project>
```

### Test project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\FrenchExDev.Net.<Package>\FrenchExDev.Net.<Package>.csproj" />
    <ProjectReference Include="..\..\src\FrenchExDev.Net.<Package>.Testing\FrenchExDev.Net.<Package>.Testing.csproj" />
  </ItemGroup>
</Project>
```

Note: **no `Version=` attributes** on `<PackageReference>`. See
[CENTRAL-PACKAGE-MANAGEMENT](../CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md).

## Solution file structure

The package `.slnx` lists its three projects under `src/` and `test/` solution folders.
The aggregate `Net/FrenchExDev/FrenchExDev.Net.slnx` includes every package, organised
by package name.

## Cross-package dependencies

- **Runtime → Runtime only.** A runtime project may reference another package's runtime
  project, never another package's `.Testing` project.
- **Tests → Testing.** A test project of package A may reference package B's
  `FrenchExDev.Net.B.Testing` project to reuse fakes/builders.
- **Tests → Runtime (other package): allowed but rare.** Prefer routing through the
  Testing project for shared fixtures.

## Validation checklist

- [ ] `doc/`, `src/`, `test/` all exist
- [ ] `doc/ARCHITECTURE.md`, `doc/HOW-TO.md`, `doc/INDEX.md` exist
- [ ] Runtime project targets `net10.0` or `netstandard2.0;net10.0`
- [ ] Testing project targets `netstandard2.0;net10.0`
- [ ] Test project targets `net10.0` only
- [ ] All projects have `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`
- [ ] Test project has `<IsPackable>false</IsPackable>`
- [ ] Test project references xUnit + coverlet
- [ ] Package `.slnx` lists only its own projects
- [ ] No inline `Version=` on any `<PackageReference>`
- [ ] `CLAUDE.md` exists at the package root and links to relevant skills
