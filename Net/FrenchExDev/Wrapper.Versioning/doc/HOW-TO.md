# How-To Guide

Practical instructions for using, extending, and maintaining FrenchExDev.Net.Wrapper.Versioning.

## Using Version Collectors

### Collect versions from GitHub releases

```csharp
using FrenchExDev.Net.Wrapper.Versioning;

var collector = new GitHubReleasesVersionCollector("containers", "podman");
var versions = await collector.CollectVersionsAsync();
// ["4.1.1", "4.2.0", ..., "5.8.0"] -- sorted, no pre-releases, no "v" prefix
```

### Collect versions from GitHub tags

```csharp
var collector = new GitHubTagsVersionCollector("docker", "cli");
var versions = await collector.CollectVersionsAsync();
// ["18.09.0", "19.03.0", ..., "29.3.0"] -- no pre-release tags (no dashes)
```

### Collect versions from GitLab releases

```csharp
var collector = new GitLabReleasesVersionCollector("gitlab-org%2Fcli");
var versions = await collector.CollectVersionsAsync();
```

For private GitLab instances:

```csharp
var collector = new GitLabReleasesVersionCollector(
    "my-group%2Fmy-project",
    baseUrl: "https://gitlab.mycompany.com");
```

Set `GITLAB_TOKEN` environment variable for authenticated access.

### Custom tag-to-version mapping

All collectors accept an optional `tagToVersion` function:

```csharp
// Strip a "release-" prefix instead of "v"
var collector = new GitHubReleasesVersionCollector(
    "owner", "repo",
    tagToVersion: tag => tag.Replace("release-", ""));

// Filter tags to a specific pattern (return null to skip)
var collector = new GitHubTagsVersionCollector(
    "owner", "repo",
    tagToVersion: tag => tag.StartsWith("release/") ? tag[8..] : null);
```

### Use a static version list

```csharp
var collector = new StaticVersionCollector(["1.0.0", "1.1.0", "2.0.0"]);
```

### Compare version strings

```csharp
// Semver-aware comparison: numeric segments compared as integers
GitHubReleasesVersionCollector.CompareVersionStrings("1.2.3", "1.10.0"); // negative
GitHubReleasesVersionCollector.CompareVersionStrings("2.0.0", "1.9.9"); // positive
```

### Filter to latest patch per minor

```csharp
var all = new List<string> { "1.0.1", "1.0.3", "1.1.0", "1.1.2", "2.0.0" };
var filtered = VersionFilters.LatestPatchPerMinor(all);
// ["1.0.3", "1.1.2", "2.0.0"]
```

## Building a Design Pipeline

### Minimal pipeline: download and save

```csharp
var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(version =>
        $"https://example.com/schemas/v{version}/spec.json")
    .UseSave()
    .Build();
```

### Pipeline with content transformation

```csharp
var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(version =>
        $"https://example.com/schemas/v{version}/spec.json")
    .UseContentTransform((version, content) =>
    {
        var doc = JsonDocument.Parse(content);
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    })
    .UseSave()
    .Build();
```

### Custom middleware

```csharp
pipeline.Use(next => async ctx =>
{
    ctx.Progress?.SetStage("Validating");

    // Pre-processing: validate before next stage
    if (ctx.Content is null)
        throw new InvalidOperationException($"No content for {ctx.Key}");

    await next(ctx);

    // Post-processing: log after next stage completes
    ctx.Logger.LogInformation("Processed {Key} -> {Path}", ctx.Key, ctx.OutputFilePath);
});
```

### Pipeline with non-string items

```csharp
public record PluginEntry(string Name, string Version, string Url);

var pipeline = new DesignPipeline<PluginEntry>()
    .UseHttpDownload(entry => entry.Url)
    .UseContentTransform((entry, content) =>
        TransformGoSourceToJson(entry, content))
    .UseSave()
    .Build();
```

## Running the Pipeline

### Basic runner setup

```csharp
var runner = new DesignPipelineRunner<string>
{
    ItemCollector     = new GitHubReleasesVersionCollector("compose-spec", "compose-go"),
    Pipeline          = pipeline,
    KeySelector       = version => version,
    OutputDir         = Path.Combine("src", "MyProject", "schemas"),
    OutputFilePattern = "schema-v{key}.json",
};

return await runner.RunAsync(args);
```

### Runner with filtering

```csharp
var runner = new DesignPipelineRunner<string>
{
    ItemCollector     = collector,
    Pipeline          = pipeline,
    KeySelector       = v => v,
    OutputDir         = outputDir,
    ItemFilter        = VersionFilters.LatestPatchPerMinor,
    OutputFilePattern = "spec-v{key}.json",
};
```

### Runner with custom configuration

```csharp
var runner = new DesignPipelineRunner<PluginEntry>
{
    ItemCollector      = new StaticItemCollector<PluginEntry>(entries),
    Pipeline           = pipeline,
    KeySelector        = e => $"{e.Name}-{e.Version}",
    OutputDir          = outputDir,
    OutputFilePattern  = "{key}.json",
    DefaultParallelism = 4,
    UserAgent          = "MyProject-Scraper/1.0",
    AuthTokenEnvVar    = null,              // disable auth
    MinLogLevel        = LogLevel.Debug,
};
```

### CLI arguments

The runner parses these arguments from `args`:

```bash
# List all items without processing
dotnet run -- --list

# Process only items without existing output files
dotnet run -- --missing

# Override parallelism
dotnet run -- --parallel 4

# Override output directory
dotnet run -- --output /tmp/schemas

# Combine flags
dotnet run -- --missing --parallel 8
```

## Adding a New Consumer

To create a new design-time project that uses this library:

### 1. Create the Design project

```
MyProject/
  src/
    FrenchExDev.Net.MyProject.Design/
      FrenchExDev.Net.MyProject.Design.csproj
      Program.cs
```

### 2. Add project reference

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\Wrapper.Versioning\src\FrenchExDev.Net.Wrapper.Versioning\FrenchExDev.Net.Wrapper.Versioning.csproj" />
  </ItemGroup>
</Project>
```

### 3. Write Program.cs

```csharp
using FrenchExDev.Net.Wrapper.Versioning;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "FrenchExDev.Net.MyProject", "schemas"));

var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(v => $"https://api.example.com/v{v}/schema.json")
    .UseSave()
    .Build();

return await new DesignPipelineRunner<string>
{
    ItemCollector     = new GitHubReleasesVersionCollector("owner", "repo"),
    Pipeline          = pipeline,
    KeySelector       = v => v,
    OutputDir         = outputDir,
    OutputFilePattern = "myproject-v{key}.json",
}.RunAsync(args);
```

### 4. Run

```bash
# Discover available versions
dotnet run --project src/FrenchExDev.Net.MyProject.Design -- --list

# Download only missing versions
dotnet run --project src/FrenchExDev.Net.MyProject.Design -- --missing

# Full download
dotnet run --project src/FrenchExDev.Net.MyProject.Design
```

## Implementing a Custom Collector

### Version collector (IVersionCollector)

```csharp
public sealed class NpmRegistryVersionCollector : IVersionCollector
{
    private readonly string _packageName;

    public NpmRegistryVersionCollector(string packageName) =>
        _packageName = packageName;

    public async Task<IReadOnlyList<string>> CollectVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient();
        var json = await http.GetStringAsync(
            $"https://registry.npmjs.org/{_packageName}", cancellationToken);
        var doc = JsonDocument.Parse(json);
        var versions = doc.RootElement
            .GetProperty("versions")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToList();
        versions.Sort(GitHubReleasesVersionCollector.CompareVersionStrings);
        return versions;
    }

    public Task<IReadOnlyList<string>> CollectItemsAsync(
        CancellationToken cancellationToken = default) =>
        CollectVersionsAsync(cancellationToken);
}
```

### Generic item collector (IItemCollector&lt;TItem&gt;)

```csharp
public sealed class PluginRegistryCollector : IItemCollector<PluginEntry>
{
    public Task<IReadOnlyList<PluginEntry>> CollectItemsAsync(
        CancellationToken cancellationToken = default)
    {
        // Load from file, API, database, etc.
        return Task.FromResult<IReadOnlyList<PluginEntry>>(entries);
    }
}
```

## Running Tests

### All tests

```bash
dotnet test Wrapper.Versioning/test/FrenchExDev.Net.Wrapper.Versioning.Tests
```

### With coverage

```bash
dotnet test Wrapper.Versioning/test/FrenchExDev.Net.Wrapper.Versioning.Tests \
    --settings Wrapper.Versioning/coverage.runsettings \
    --collect:"XPlat Code Coverage"
```

### Quality gate

```bash
dotnet run --project QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test \
    -s Wrapper.Versioning/FrenchExDev.Net.Wrapper.Versioning.slnx
```

## Troubleshooting

### GitHub API rate limiting

Unauthenticated requests are limited to 60/hour. Set `GITHUB_TOKEN`:

```bash
export GITHUB_TOKEN=ghp_...
dotnet run --project src/FrenchExDev.Net.MyProject.Design
```

The runner reads this env var and adds a Bearer token to all pipeline HTTP requests. Version collectors create their own HttpClient, so also set the token if you're calling `CollectVersionsAsync()` directly in a rate-limited context.

### GitLab authentication

Set `GITLAB_TOKEN` for private repos or higher rate limits:

```bash
export GITLAB_TOKEN=glpat-...
```

The `GitLabReleasesVersionCollector` reads this automatically and adds a `PRIVATE-TOKEN` header.

### --missing doesn't detect existing files

Verify that `OutputFilePattern` matches the actual file names on disk. The pattern is split on `{key}` to extract prefix and suffix for matching. Example:

- Pattern: `"schema-v{key}.json"`
- File: `schema-v1.29.2.json`
- Extracted key: `1.29.2`

If the pattern doesn't match, all items are treated as missing.

### Pipeline fails with null Content

`UseContentTransform` and `UseSave` check for `null` content. If `Content` is null, the transform is skipped and save writes nothing. Ensure `UseHttpDownload` runs before stages that depend on `Content`.

### Exit code 1 but some items succeeded

The runner returns 1 if *any* item failed. Check `stderr` for `FAILED {key}: {message}` lines to identify which items had errors. Successfully processed items are listed on `stdout`.
