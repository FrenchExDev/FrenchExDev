# COMPOSE-BUNDLE — How-To

## Bootstrap a new bundle for format `Xxx`

1. Create the four projects under `Net/FrenchExDev/Xxx/src/`:

   ```
   FrenchExDev.Net.Xxx.Bundle.Attributes
   FrenchExDev.Net.Xxx.Bundle.SourceGenerator
   FrenchExDev.Net.Xxx.Bundle.Design
   FrenchExDev.Net.Xxx.Bundle
   ```

   Plus a `test/FrenchExDev.Net.Xxx.Bundle.Tests` xUnit project.

2. Add the `[XxxBundle]` marker attribute to the `.Attributes` project, multi-
   targeted `netstandard2.0;net10.0`. Empty body.

3. In `Bundle.SourceGenerator` (`netstandard2.0`), reference:
   - `Microsoft.CodeAnalysis.CSharp` (5.3.0)
   - `Microsoft.CodeAnalysis.Analyzers` (5.3.0)
   - `Builder.SourceGenerator.Lib` as a project reference with
     `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`

4. In `Bundle` (the consumer library), reference the SG project as an analyzer:

   ```xml
   <ProjectReference Include="..\..\Bundle.SourceGenerator\Bundle.SourceGenerator.csproj"
                     OutputItemType="Analyzer"
                     ReferenceOutputAssembly="false" />
   ```

5. Drop `XxxBundleDescriptor.cs` into `Bundle/`:

   ```csharp
   [XxxBundle]
   public sealed partial class XxxBundleDescriptor { }
   ```

6. Declare schemas as additional files in `Bundle/Bundle.csproj`:

   ```xml
   <ItemGroup>
     <AdditionalFiles Include="schemas\xxx-spec-*.json" />
   </ItemGroup>
   ```

## Implement the Design project

The Design project downloads schemas from upstream into `Bundle/schemas/`.

```csharp
// Bundle.Design/Program.cs
var versions = await GitHubReleasesAsync("upstream-org", "upstream-repo");
var http = new HttpClient();

foreach (var v in versions)
{
    var url = $"https://raw.githubusercontent.com/.../schema-{v}.json";
    var json = await http.GetStringAsync(url);
    var path = Path.Combine("..", "Bundle", "schemas", $"xxx-spec-{v}.json");
    await File.WriteAllTextAsync(path, json);
}
```

Run it manually when bumping schemas:

```bash
dotnet run --project Xxx/src/FrenchExDev.Net.Xxx.Bundle.Design
```

The Design project must never run during normal builds. Only on demand by a
human or CI when bumping versions.

## Implement the source generator

The generator skeleton:

```csharp
[Generator]
public class XxxBundleGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var schemas = context.AdditionalTextsProvider
            .Where(f => Path.GetFileName(f.Path).StartsWith("xxx-spec-")
                     && f.Path.EndsWith(".json"));

        context.RegisterSourceOutput(schemas.Collect(), Generate);
    }

    private void Generate(SourceProductionContext ctx, ImmutableArray<AdditionalText> files)
    {
        try
        {
            var models = files
                .Select(f => SchemaReader.Parse(f.GetText()!.ToString(),
                                                ExtractVersion(f.Path)))
                .ToList();

            var unified = SchemaVersionMerger.Merge(models);

            VersionMetadataEmitter.Emit(ctx, unified);
            ModelClassEmitter.Emit(ctx, unified);

            foreach (var def in unified.Definitions)
            {
                var builderModel = BuilderHelper.From(def);
                var source = BuilderEmitter.Emit(builderModel);
                ctx.AddSource($"{def.ClassName}Builder.g.cs", source);
            }
        }
        catch (Exception ex)
        {
            ctx.AddSource("GenerateError.g.cs",
                $"// Generator error: {ex.GetType().Name}: {ex.Message}\n// {ex.StackTrace}");
        }
    }
}
```

## Add a new property type

When upstream introduces a schema construct the generator does not yet know:

1. Add a `PropertyType` enum value in `SchemaModels.cs`.
2. Detect the construct in `SchemaReader.ParseProperty` (or `ParseOneOf`).
3. Add the C# type mapping in `NamingHelper.MapCSharpType`.
4. If it is a collection, ensure `BuilderHelper.DetectCollection` extracts
   the item type so the builder can emit `Validate{Prop}Item` hooks.
5. Add a unit test in `Bundle.Tests` covering the new shape.

## Use the bundle from consumer code

```csharp
var compose = new ComposeFile()
    .Apply(new TraefikContributor())
    .Apply(new GitLabContributor());

var yaml = ComposeSerializer.Serialize(compose);
File.WriteAllText("docker-compose.yml", yaml);
```

Or via builders for explicit construction:

```csharp
var result = await new ComposeFileBuilder()
    .WithName("my-stack")
    .WithServices(new Dictionary<string, ComposeService>
    {
        ["web"] = new ComposeServiceBuilder()
            .WithImage("nginx:alpine")
            .WithPorts(["80:80"])
            .BuildAsync().Result.ValueOrThrow().Value
    })
    .BuildAsync();

var compose = result.ValueOrThrow().Value;
```

## Write a contributor

```csharp
public sealed class PostgresContributor : IComposeFileContributor
{
    public void Contribute(ComposeFile bundle)
    {
        bundle.Services ??= new Dictionary<string, ComposeService>();
        bundle.Services["postgres"] = new ComposeService
        {
            Image = "postgres:16-alpine",
            Environment = new Dictionary<string, string?>
            {
                ["POSTGRES_USER"] = "app",
                ["POSTGRES_DB"] = "app",
            },
            Volumes = new List<ComposeServiceVolumesConfig>
            {
                new() { Type = "volume", Source = "pgdata", Target = "/var/lib/postgresql/data" }
            }
        };

        bundle.Volumes ??= new Dictionary<string, ComposeVolume?>();
        bundle.Volumes["pgdata"] = null; // upstream-defined "default" semantics
    }
}
```

## Bump schemas

```bash
dotnet run --project Xxx/src/FrenchExDev.Net.Xxx.Bundle.Design
dotnet build Xxx/FrenchExDev.Net.Xxx.Bundle.slnx
dotnet test  Xxx/FrenchExDev.Net.Xxx.Bundle.slnx
```

Inspect the generated files in `Bundle/obj/Generated/` (or via the IDE
`Dependencies → Analyzers` node) to verify new properties appeared with the
correct `[SinceVersion]`.

## Debug the source generator

The generator emits a `DebugInfo.g.cs` file containing the unified schema
summary (definition count, version count, extracted version list). On error,
`GenerateError.g.cs` contains the exception stack as a comment. The build does
not fail; instead the consumer fails to compile against missing types — that
is your signal to inspect the error file.

For deeper debugging, set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>`
in the consumer csproj and inspect `obj/Generated/` directly.

## Things to never do

- Never edit a `.g.cs` file. They are overwritten on every build.
- Never add a hand-written model class for something the schema covers.
- Never bypass the `Builder.SourceGenerator.Lib` and roll a new builder
  emitter — consistency across the monorepo matters more than local
  optimization.
- Never call the Design project from the build. It hits the network.
- Never put schemas under source control without a corresponding scrape
  command in the Design project.
