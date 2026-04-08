# LOCAL-NUGET-REGISTRY — Architecture

## Layout

```
C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__\
  FrenchExDev.Net.Result.1.0.0.nupkg
  FrenchExDev.Net.Builder.1.0.0.nupkg
  FrenchExDev.Net.Builder.SourceGenerator.1.0.0.nupkg
  FrenchExDev.Net.Vos.2.1.0.nupkg
  ...
```

A flat directory of `.nupkg` files. NuGet enumerates the directory on each restore and
matches packages by id and version.

## NuGet source registration

Per-user registration (preferred for development machines):

```bash
dotnet nuget add source "C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__" \
  --name FrenchExDevLocal
```

Per-repo registration via a `nuget.config` next to the solution:

```xml
<configuration>
  <packageSources>
    <add key="FrenchExDevLocal"
         value="C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

## Publish flow

```
dotnet pack
  ↓
bin/Release/FrenchExDev.Net.<Pkg>.<Version>.nupkg
  ↓
copy → __Local_Nuget_Registry__/
  ↓
dotnet restore (consumer)
  ↓
NuGet finds .nupkg, extracts to global packages cache
  ↓
Consumer compiles
```

## Versioning

The local registry supports multiple versions of the same package side-by-side. Bump
the version in the `.csproj` (`<Version>1.0.1</Version>`) before each pack to avoid
collisions.

```
__Local_Nuget_Registry__/
  FrenchExDev.Net.Result.1.0.0.nupkg   ← old
  FrenchExDev.Net.Result.1.0.1.nupkg   ← new
```

NuGet's restore picks the highest matching version per consumer's `<PackageReference>`.

## Source generator packages

A SG package needs the analyzer assembly placed inside the `.nupkg` at:

```
analyzers/dotnet/cs/<MyGenerator>.dll
analyzers/dotnet/cs/<MyGenerator>.Lib.dll   (if using a shared lib)
```

This is achieved in the SG `.csproj` via:

```xml
<ItemGroup>
  <None Include="$(OutputPath)\$(AssemblyName).dll"
        Pack="true"
        PackagePath="analyzers/dotnet/cs"
        Visible="false" />
</ItemGroup>
```

When restored, MSBuild loads the analyzer DLL and runs the generator.
