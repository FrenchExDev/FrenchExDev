# CENTRAL-PACKAGE-MANAGEMENT — Architecture

## File location

```
Net/FrenchExDev/Directory.Packages.props
```

MSBuild auto-discovers `Directory.Packages.props` by walking up from each `.csproj`.
Because the file lives at `Net/FrenchExDev/`, every package under that directory
inherits it. Packages outside `Net/FrenchExDev/` (none currently) would need their own
or a parent at the repo root.

## File shape

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <!-- Test stack -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk"   Version="18.3.0" />
    <PackageVersion Include="xunit"                    Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="coverlet.collector"       Version="6.0.4" />

    <!-- Roslyn (pinned to match what source generators use) -->
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp"           Version="5.3.0" />
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.3.0" />
    <PackageVersion Include="Microsoft.CodeAnalysis.Analyzers"        Version="5.3.0" />
    <PackageVersion Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="5.3.0" />

    <!-- Common runtime libraries -->
    <PackageVersion Include="System.Text.Json"  Version="9.0.0" />
    <PackageVersion Include="YamlDotNet"        Version="16.3.3" />
    <PackageVersion Include="JsonSchema.Net"    Version="7.3.0" />
    <PackageVersion Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />

    <!-- DI / Logging -->
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging"             Version="9.0.0" />

    <!-- EF Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore"            Version="9.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="9.0.0" />

    <!-- Testing utilities -->
    <PackageVersion Include="Shouldly" Version="4.3.0" />
    <PackageVersion Include="CsCheck"  Version="4.0.0" />
  </ItemGroup>
</Project>
```

## Consumer project shape

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YamlDotNet" />        <!-- ✓ -->
    <PackageReference Include="System.CommandLine" /><!-- ✓ -->
  </ItemGroup>
</Project>
```

## Anti-shape

```xml
<ItemGroup>
  <PackageReference Include="YamlDotNet" Version="16.3.3" />  <!-- ✗ inline version -->
</ItemGroup>
```

The inline `Version="16.3.3"` overrides the central definition. Even if the version
*matches*, this is still wrong — the override means future bumps won't apply here, and
the project drifts.

## Source generator and Roslyn pinning

Source generators use `Microsoft.CodeAnalysis.CSharp` at **compile time** of the host
project (the project consuming the generator). The version is determined by what is
shipped *inside* the generator's NuGet package, which in turn depends on what the
generator's `.csproj` referenced when it was built.

The rule:

```
SG project's Microsoft.CodeAnalysis.CSharp version
  ==
Directory.Packages.props's Microsoft.CodeAnalysis.CSharp version
  ==
APIs the SG emitter actually calls
```

If any of these three diverge, builds fail in opaque ways. The fix is always to align
all three at the same version.
