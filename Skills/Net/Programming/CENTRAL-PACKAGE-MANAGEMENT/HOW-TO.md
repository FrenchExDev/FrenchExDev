# CENTRAL-PACKAGE-MANAGEMENT — How-To

## Add a new NuGet package to the monorepo

### 1. Decide the version

Pick the version on [nuget.org](https://www.nuget.org) — usually the latest stable
release that targets `net10.0` (or `netstandard2.0` for source generator dependencies).

### 2. Add a `<PackageVersion>` entry

Edit [Directory.Packages.props](../../../../Net/FrenchExDev/Directory.Packages.props)
and insert in alphabetical order within the existing `<ItemGroup>`:

```xml
<PackageVersion Include="Polly.Core" Version="8.6.0" />
```

### 3. Add a `<PackageReference>` to the consuming project

```xml
<ItemGroup>
  <PackageReference Include="Polly.Core" />
</ItemGroup>
```

**No `Version=` attribute.** If you add one, the package's individual reference will
silently override the central version.

### 4. Restore and build

```bash
dotnet restore Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx
dotnet build   Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx
```

## Bump an existing package version

Edit `Directory.Packages.props` only. All consumers pick up the new version on the
next `dotnet restore`.

```diff
- <PackageVersion Include="YamlDotNet" Version="16.2.0" />
+ <PackageVersion Include="YamlDotNet" Version="16.3.3" />
```

Run a full build + test pass to catch breaking changes:

```bash
dotnet build Net/FrenchExDev/FrenchExDev.Net.slnx
dotnet test  Net/FrenchExDev/FrenchExDev.Net.slnx
```

## Audit for inline `Version=` violations

```bash
grep -rn 'PackageReference[^>]*Version=' Net/FrenchExDev/**/*.csproj
```

Any matches are violations. Move the version to `Directory.Packages.props` and remove
the inline attribute.

## Pin Roslyn for a source generator

Source generators that consume `Microsoft.CodeAnalysis.CSharp` must align with the
central version. In the generator's `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
  <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" PrivateAssets="all" />
</ItemGroup>
```

`PrivateAssets="all"` keeps the Roslyn dependency from leaking into downstream packages.
The version is still resolved through `Directory.Packages.props`.

If the generator emitter uses an API that doesn't exist in the centrally-pinned Roslyn
version, the fix is to **upgrade the central pin**, not add a one-off override.

## Troubleshoot a CPM error

### `NU1008: Projects that use central package version management should not define the version on the PackageReference items`

You added `Version="..."` inline. Remove it.

### `NU1604: Project dependency does not contain an inclusive lower bound`

The package isn't in `Directory.Packages.props`. Add a `<PackageVersion>` entry.

### `error CS0103` from a generated file mentioning a Roslyn API

The generator was built against a different Roslyn version than the host's. Align both
to the central pin.
