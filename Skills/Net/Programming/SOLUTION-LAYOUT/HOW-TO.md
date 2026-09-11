# SOLUTION-LAYOUT — How-To

## Create a new package `MyFeature`

### 1. Create the directory tree

```bash
mkdir -p Net/FrenchExDev/MyFeature/doc \
         Net/FrenchExDev/MyFeature/src \
         Net/FrenchExDev/MyFeature/test
```

### 2. Generate the three projects

```bash
dotnet new classlib -n FrenchExDev.Net.MyFeature \
  -o Net/FrenchExDev/MyFeature/src/FrenchExDev.Net.MyFeature

dotnet new classlib -n FrenchExDev.Net.MyFeature.Testing \
  -o Net/FrenchExDev/MyFeature/src/FrenchExDev.Net.MyFeature.Testing

dotnet new xunit -n FrenchExDev.Net.MyFeature.Tests \
  -o Net/FrenchExDev/MyFeature/test/FrenchExDev.Net.MyFeature.Tests
```

### 3. Strip inline package versions

The `dotnet new xunit` template emits `<PackageReference Include="xunit" Version="..." />`.
**Remove every `Version=` attribute** — versions live in
[Directory.Packages.props](../../../../Net/FrenchExDev/Directory.Packages.props). Add
any new packages there, in alphabetical order.

### 4. Create the package solution

```bash
dotnet new sln -n FrenchExDev.Net.MyFeature \
  -o Net/FrenchExDev/MyFeature --format slnx

dotnet sln Net/FrenchExDev/MyFeature/FrenchExDev.Net.MyFeature.slnx add \
  Net/FrenchExDev/MyFeature/src/FrenchExDev.Net.MyFeature/FrenchExDev.Net.MyFeature.csproj \
  Net/FrenchExDev/MyFeature/src/FrenchExDev.Net.MyFeature.Testing/FrenchExDev.Net.MyFeature.Testing.csproj \
  Net/FrenchExDev/MyFeature/test/FrenchExDev.Net.MyFeature.Tests/FrenchExDev.Net.MyFeature.Tests.csproj
```

### 5. Wire project references

```bash
# Tests → Runtime + Testing
dotnet add Net/FrenchExDev/MyFeature/test/FrenchExDev.Net.MyFeature.Tests/FrenchExDev.Net.MyFeature.Tests.csproj reference \
  Net/FrenchExDev/MyFeature/src/FrenchExDev.Net.MyFeature/FrenchExDev.Net.MyFeature.csproj \
  Net/FrenchExDev/MyFeature/src/FrenchExDev.Net.MyFeature.Testing/FrenchExDev.Net.MyFeature.Testing.csproj

# Testing → Runtime
dotnet add Net/FrenchExDev/MyFeature/src/FrenchExDev.Net.MyFeature.Testing/FrenchExDev.Net.MyFeature.Testing.csproj reference \
  Net/FrenchExDev/MyFeature/src/FrenchExDev.Net.MyFeature/FrenchExDev.Net.MyFeature.csproj
```

### 6. Create doc placeholders

Create `doc/ARCHITECTURE.md`, `doc/HOW-TO.md`, and `doc/INDEX.md`. See
[../../Documentation/HOW-TO.md](../../Documentation/HOW-TO.md) for content templates.

### 7. Create the package `CLAUDE.md`

Use [../../Documentation/CLAUDE-MD-TEMPLATE.md](../../Documentation/CLAUDE-MD-TEMPLATE.md).

### 8. Add to the aggregate solution

```bash
dotnet sln Net/FrenchExDev/FrenchExDev.Net.slnx add \
  Net/FrenchExDev/MyFeature/src/FrenchExDev.Net.MyFeature/FrenchExDev.Net.MyFeature.csproj \
  Net/FrenchExDev/MyFeature/src/FrenchExDev.Net.MyFeature.Testing/FrenchExDev.Net.MyFeature.Testing.csproj \
  Net/FrenchExDev/MyFeature/test/FrenchExDev.Net.MyFeature.Tests/FrenchExDev.Net.MyFeature.Tests.csproj
```

### 9. Verify the build

```bash
dotnet build Net/FrenchExDev/MyFeature/FrenchExDev.Net.MyFeature.slnx
dotnet test  Net/FrenchExDev/MyFeature/FrenchExDev.Net.MyFeature.slnx
```

## Add a runtime dependency on another package

In the consumer's runtime `.csproj`:

```xml
<ItemGroup>
  <ProjectReference
    Include="..\..\..\OtherPackage\src\FrenchExDev.Net.OtherPackage\FrenchExDev.Net.OtherPackage.csproj" />
</ItemGroup>
```

Never reference an `.Testing` project from a runtime project.

## Reuse another package's fakes in tests

In the test `.csproj`:

```xml
<ItemGroup>
  <ProjectReference
    Include="..\..\..\OtherPackage\src\FrenchExDev.Net.OtherPackage.Testing\FrenchExDev.Net.OtherPackage.Testing.csproj" />
</ItemGroup>
```
