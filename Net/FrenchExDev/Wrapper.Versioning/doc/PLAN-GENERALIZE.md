# Plan: Generalize Design Pipeline + Migrate Packer.Bundle.Design

## Context

The `SchemaDesignPipeline` in `JsonSchema.Design` is hardwired to `string` versions. Packer.Bundle.Design has a different pattern: it iterates a `PluginRegistry` of `(Org, Repo, Path, PluginKind, TypeName)` entries. To reuse the same middleware pipeline, parallel runner, and CLI arg handling, we generalize the pipeline to `DesignPipeline<TItem>` and move it into `Wrapper.Versioning`.

## Dependency Graph (After)

```
Wrapper.Versioning
  (IVersionCollector : IItemCollector<string>)
  (DesignPipeline<T>, DesignPipelineRunner<T>)
  (VersionFilters)
       ^
       ├── BinaryWrapper.Design
       ├── JsonSchema.Design (thin: re-exports + future JSON-schema helpers)
       │        ^
       │        └── DockerCompose.Bundle.Design
       └── Packer.Bundle.Design  (DesignPipeline<PluginRegistryEntry>)
```

---

## Phase 1: Generalize pipeline types in Wrapper.Versioning

### Modify `Wrapper.Versioning/src/.../Code.cs`

**Make `IVersionCollector` extend the new generic interface:**
```csharp
public interface IItemCollector<TItem>
{
    Task<IReadOnlyList<TItem>> CollectItemsAsync(CancellationToken ct = default);
}

public interface IVersionCollector : IItemCollector<string>
{
    Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken ct = default);
}
```
Existing collectors implement both via explicit interface implementation. `CollectItemsAsync` delegates to `CollectVersionsAsync`.

**Add `StaticItemCollector<TItem>`:**
```csharp
public sealed class StaticItemCollector<TItem> : IItemCollector<TItem> { ... }
```

**Add generic pipeline types:**
```csharp
public delegate Task DesignPipelineDelegate<TItem>(DesignPipelineContext<TItem> ctx);

public sealed class DesignPipelineContext<TItem>
{
    public required TItem Item { get; init; }
    public required string Key { get; init; }       // keySelector(Item)
    public required string OutputDir { get; init; }
    public required ILogger Logger { get; init; }
    public required HttpClient HttpClient { get; init; }
    public required string OutputFilePattern { get; init; }
    public string? Content { get; set; }
    public string? OutputFilePath { get; set; }
    public DesignPipelineProgressInfo? Progress { get; init; }
}

public sealed class DesignPipelineProgressInfo { ... }

public sealed class DesignPipeline<TItem> { Use(...); Build(); }
```

**Middleware extensions — item-based overloads only** (no key-based, avoids ambiguity when TItem=string):
```csharp
public static class DesignPipelineExtensions
{
    UseHttpDownload<TItem>(Func<TItem, string> urlBuilder)
    UseContentTransform<TItem>(Func<TItem, string, string> transform)
    UseSave<TItem>()
}
```

**Runner:**
```csharp
public sealed class DesignPipelineRunner<TItem>
{
    public required IItemCollector<TItem> ItemCollector { get; init; }
    public required DesignPipelineDelegate<TItem> Pipeline { get; init; }
    public required Func<TItem, string> KeySelector { get; init; }
    public required string OutputDir { get; init; }
    public string OutputFilePattern { get; init; } = "{key}.json";
    public int DefaultParallelism { get; init; } = 6;
    public string UserAgent { get; init; } = "FrenchExDev-DesignPipeline/1.0";
    public string? AuthTokenEnvVar { get; init; } = "GITHUB_TOKEN";
    public Func<IReadOnlyList<TItem>, IReadOnlyList<TItem>>? ItemFilter { get; init; }
    public LogLevel MinLogLevel { get; init; } = LogLevel.Information;
    public Task<int> RunAsync(string[] args);
}
```

**Move `VersionFilters` from JsonSchema.Design.**

### Add NuGet refs to `Wrapper.Versioning.csproj`
```xml
<PackageReference Include="Microsoft.Extensions.Logging" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" />
```

---

## Phase 2: Slim down `JsonSchema.Design`

Keep the project as a thin convenience layer. Remove pipeline types (now in Wrapper.Versioning). Keep:
- Re-exports via `using` / public surface
- Room for future JSON-schema-specific helpers (schema validation, oneOf resolution, etc.)

Update `JsonSchema.Design.csproj` to reference `Wrapper.Versioning` (already does).

---

## Phase 3: Update tests

**`Wrapper.Versioning.Tests`:** Add pipeline/runner/middleware tests from the old JsonSchema.Design.Tests, updated to use `DesignPipeline<string>`. Add tests for:
- `IItemCollector<TItem>` / `StaticItemCollector<T>`
- `IVersionCollector` now implementing `IItemCollector<string>`
- `DesignPipeline<PluginRegistryEntry>` (non-string TItem proof)
- `VersionFilters` (moved from JsonSchema.Design.Tests)

**`JsonSchema.Design.Tests`:** Slim down or delete depending on remaining surface.

Maintain: 100% branch/line coverage, quality score 1.0.

---

## Phase 4: Update DockerCompose.Bundle.Design

```csharp
using FrenchExDev.Net.Wrapper.Versioning;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "FrenchExDev.Net.DockerCompose.Bundle", "schemas"));

var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(v =>
        $"https://raw.githubusercontent.com/compose-spec/compose-go/v{v}/schema/compose-spec.json")
    .UseSave()
    .Build();

return await new DesignPipelineRunner<string>
{
    ItemCollector = new GitHubReleasesVersionCollector("compose-spec", "compose-go"),
    Pipeline = pipeline,
    KeySelector = v => v,
    ItemFilter = VersionFilters.LatestPatchPerMinor,
    OutputDir = outputDir,
    OutputFilePattern = "compose-spec-v{key}.json",
}.RunAsync(args);
```

No adapter needed — `GitHubReleasesVersionCollector` implements `IItemCollector<string>` via `IVersionCollector`.

Update csproj: ref `Wrapper.Versioning` (can keep or drop `JsonSchema.Design` ref).

---

## Phase 5: Migrate Packer.Bundle.Design

```csharp
using System.Text.Json;
using FrenchExDev.Net.Wrapper.Versioning;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "FrenchExDev.Net.Packer.Bundle", "scrape"));

var jsonOptions = new JsonSerializerOptions
{ WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

var pipeline = new DesignPipeline<PluginRegistryEntry>()
    .UseHttpDownload(entry =>
        $"https://raw.githubusercontent.com/{entry.Org}/{entry.Repo}/main/{entry.Path}")
    .UseContentTransform((entry, goSource) =>
    {
        var (fields, nestedBlocks) = Hcl2SpecParser.Parse(goSource);
        return JsonSerializer.Serialize(new ScrapedPluginType
        {
            PluginKind = entry.PluginKind, TypeName = entry.TypeName,
            Repo = $"{entry.Org}/{entry.Repo}", SourcePath = entry.Path,
            Fields = fields, NestedBlocks = nestedBlocks,
        }, jsonOptions);
    })
    .UseSave()
    .Build();

return await new DesignPipelineRunner<PluginRegistryEntry>
{
    ItemCollector = new StaticItemCollector<PluginRegistryEntry>(PluginRegistry.Entries),
    Pipeline = pipeline,
    KeySelector = e => e.TypeName,
    OutputDir = outputDir,
    OutputFilePattern = "{key}.json",
}.RunAsync(args);
```

94 lines → ~30 lines. Add `Wrapper.Versioning` ref to csproj.

---

## Critical files

| File | Action |
|------|--------|
| `Wrapper.Versioning/src/.../Code.cs` | Add generic pipeline + make IVersionCollector extend IItemCollector |
| `Wrapper.Versioning/src/.../csproj` | Add logging NuGet refs |
| `Wrapper.Versioning/test/.../Tests.cs` | Merge pipeline tests, add generic TItem tests |
| `JsonSchema.Design/src/.../Code.cs` | Remove pipeline types (moved), keep as thin layer |
| `JsonSchema.Design/test/.../Tests.cs` | Slim down or remove |
| `DockerCompose/src/.../Bundle.Design/Program.cs` | Update to DesignPipeline<string> |
| `DockerCompose/src/.../Bundle.Design/csproj` | Update ref |
| `Packer/src/.../Bundle.Design/Program.cs` | Rewrite 94 → ~30 lines |
| `Packer/src/.../Bundle.Design/csproj` | Add Wrapper.Versioning ref |
| `Wrapper.Versioning/quality-gate.yml` | Verify thresholds after growth |

## Verification
- `dotnet test Wrapper.Versioning/` — all tests pass, 100% b/l coverage
- `dotnet run --project DockerCompose/.../Bundle.Design -- --list` — lists 32 versions
- `dotnet run --project Packer/.../Bundle.Design -- --list` — lists 75 plugins
- Quality gate: test quality 1.0 on Wrapper.Versioning
