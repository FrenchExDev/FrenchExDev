# LOCAL-NUGET-REGISTRY — How-To

## One-time setup

```bash
# Register the local feed (per-user, persists across sessions)
dotnet nuget add source \
  "C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__" \
  --name FrenchExDevLocal

# Verify registration
dotnet nuget list source
```

If the registry directory doesn't exist yet, create it:

```bash
mkdir -p "C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__"
```

## Publish a package locally

### 1. Bump the version in the `.csproj`

```xml
<PropertyGroup>
  <Version>1.0.2</Version>
</PropertyGroup>
```

### 2. Pack

```bash
dotnet pack Net/FrenchExDev/MyPackage/src/FrenchExDev.Net.MyPackage/FrenchExDev.Net.MyPackage.csproj \
  --configuration Release \
  -o ./bin/Release
```

### 3. Copy to the local registry

```bash
cp ./bin/Release/FrenchExDev.Net.MyPackage.1.0.2.nupkg \
   "C:/code/FrenchExDev.Net/FrenchExDev.Net_i2/FrenchExDev.Net/__Local_Nuget_Registry__/"
```

### 4. Restore in the consumer

```bash
dotnet restore Net/FrenchExDev/Consumer/FrenchExDev.Net.Consumer.slnx
```

## Consume a locally-published package

In the consumer's `.csproj` (and add the version to
[Directory.Packages.props](../../../../Net/FrenchExDev/Directory.Packages.props)):

```xml
<ItemGroup>
  <PackageReference Include="FrenchExDev.Net.MyPackage" />
</ItemGroup>
```

In `Directory.Packages.props`:

```xml
<PackageVersion Include="FrenchExDev.Net.MyPackage" Version="1.0.2" />
```

## Publish a source generator package

Source generator packages need the analyzer placed at `analyzers/dotnet/cs` inside the
`.nupkg`. In the SG project's `.csproj`:

```xml
<PropertyGroup>
  <IncludeBuildOutput>false</IncludeBuildOutput>
</PropertyGroup>
<ItemGroup>
  <None Include="$(OutputPath)\$(AssemblyName).dll"
        Pack="true"
        PackagePath="analyzers/dotnet/cs"
        Visible="false" />
</ItemGroup>
```

Then `dotnet pack` and copy to the local feed as above. Consumers reference it as a
plain package — MSBuild loads the analyzer automatically.

## Clean up old `.nupkg`s

```bash
rm "C:/code/FrenchExDev.Net/FrenchExDev.Net_i2/FrenchExDev.Net/__Local_Nuget_Registry__/FrenchExDev.Net.MyPackage.1.0.0.nupkg"
```

After deleting, run `dotnet nuget locals all --clear` if a stale version is cached in
the global packages folder.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `NU1101: Unable to find package` | Feed not registered | `dotnet nuget add source ...` |
| `NU1102: Unable to find version` | Version not in feed | Pack and copy again |
| Consumer sees old code after pack | Global cache stale | `dotnet nuget locals all --clear` |
| SG analyzer not running | `.nupkg` missing `analyzers/dotnet/cs/*.dll` | Fix `<None Pack="true" PackagePath="...">` |
