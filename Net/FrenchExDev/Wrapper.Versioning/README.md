# FrenchExDev.Net.Wrapper.Versioning

Generic version collection and design-time pipeline infrastructure for discovering versions from GitHub/GitLab, downloading versioned artifacts in parallel, and transforming them through a composable middleware chain.

## Architecture

```
IItemCollector<TItem>          Collect items (versions, plugins, schemas...)
  ├── IVersionCollector        Specialization for string versions
  │     ├── GitHubReleasesVersionCollector
  │     ├── GitHubTagsVersionCollector
  │     ├── GitLabReleasesVersionCollector
  │     └── StaticVersionCollector
  └── StaticItemCollector<T>

DesignPipeline<TItem>          Composable middleware chain
  ├── UseHttpDownload          Download content from URL
  ├── UseContentTransform      Transform downloaded content
  ├── UseSave                  Write content to disk
  └── Use(custom)              Any custom middleware

DesignPipelineRunner<TItem>    Orchestrator: collect → filter → process in parallel
```

## Quick Start

```csharp
using FrenchExDev.Net.Wrapper.Versioning;

// 1. Define how to collect versions
var collector = new GitHubReleasesVersionCollector("hashicorp", "packer");

// 2. Build a middleware pipeline
var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(version =>
        $"https://example.com/schemas/{version}/schema.json")
    .UseContentTransform((version, content) =>
        content.Replace("draft-07", "draft-2020-12"))
    .UseSave()
    .Build();

// 3. Configure and run
var runner = new DesignPipelineRunner<string>
{
    ItemCollector   = collector,
    Pipeline        = pipeline,
    KeySelector     = version => version,
    OutputDir       = Path.Combine("src", "MyProject", "schemas"),
    OutputFilePattern = "schema-v{key}.json",
};

return await runner.RunAsync(args);
```

## Version Collectors

### GitHubReleasesVersionCollector

Collects versions from GitHub releases API. Filters out pre-releases. Strips leading `v` prefix.

```csharp
var collector = new GitHubReleasesVersionCollector("containers", "podman");

// Custom tag-to-version mapping
var collector = new GitHubReleasesVersionCollector(
    "owner", "repo",
    tagToVersion: tag => tag.Replace("release-", ""));
```

Supports `GITHUB_TOKEN` env var for authentication (via the runner's `AuthTokenEnvVar`).

### GitHubTagsVersionCollector

Collects versions from GitHub tags (for repos that don't use releases). Filters out pre-release tags containing dashes.

```csharp
var collector = new GitHubTagsVersionCollector("compose-spec", "compose-spec");

// Custom filter: only tags matching a pattern
var collector = new GitHubTagsVersionCollector(
    "owner", "repo",
    tagToVersion: tag => tag.StartsWith("release/") ? tag[8..] : null);
```

### GitLabReleasesVersionCollector

Collects versions from GitLab releases API v4. Filters out upcoming releases. Supports `GITLAB_TOKEN` env var for private repos.

```csharp
var collector = new GitLabReleasesVersionCollector("gitlab-org%2Fcli");

// Custom GitLab instance
var collector = new GitLabReleasesVersionCollector(
    "my-group%2Fmy-project",
    baseUrl: "https://gitlab.mycompany.com");
```

### StaticVersionCollector / StaticItemCollector&lt;T&gt;

For pre-computed lists (useful in tests or when versions come from a non-HTTP source).

```csharp
var collector = new StaticVersionCollector(["1.0.0", "1.1.0", "2.0.0"]);
var collector = new StaticItemCollector<MyItem>(myItems);
```

### Version Comparison

All collectors sort results using `GitHubReleasesVersionCollector.CompareVersionStrings`, which handles semantic versioning with numeric and pre-release segments:

```csharp
// "1.0.0" < "1.0.1" < "1.1.0" < "2.0.0"
// "1.0.0-alpha" < "1.0.0-beta"
GitHubReleasesVersionCollector.CompareVersionStrings("1.2.3", "1.10.0"); // negative
```

## Design Pipeline

The pipeline follows a middleware pattern (similar to ASP.NET Core). Each stage wraps the next, sharing a mutable `DesignPipelineContext<TItem>`.

### Built-in Middleware

| Middleware | Sets on Context | Description |
|---|---|---|
| `UseHttpDownload(urlBuilder)` | `Content` | Downloads content via HTTP GET |
| `UseContentTransform(transform)` | `Content` | Transforms `Content` in-place |
| `UseSave()` | `OutputFilePath` | Writes `Content` to disk using `OutputFilePattern` |

### Custom Middleware

```csharp
pipeline.Use(next => async ctx =>
{
    // Before next stage
    ctx.Progress?.SetStage("Validating");
    var json = JsonDocument.Parse(ctx.Content!);

    // Call next stage
    await next(ctx);

    // After next stage (if needed)
    ctx.Logger.LogInformation("Validated {Key}", ctx.Key);
});
```

### Pipeline Context

| Property | Type | Description |
|---|---|---|
| `Item` | `TItem` | The current item being processed |
| `Key` | `string` | Unique key for this item (from `KeySelector`) |
| `OutputDir` | `string` | Target output directory |
| `OutputFilePattern` | `string` | File name pattern with `{key}` placeholder |
| `Content` | `string?` | Mutable — set by download, read by transform/save |
| `OutputFilePath` | `string?` | Mutable — set by save |
| `Logger` | `ILogger` | Logger instance |
| `HttpClient` | `HttpClient` | Shared HTTP client with auth headers |
| `Progress` | `DesignPipelineProgressInfo?` | Thread-safe progress tracking |

## Pipeline Runner

`DesignPipelineRunner<TItem>` orchestrates the full workflow: collect items, filter, process in parallel.

### Configuration

| Property | Default | Description |
|---|---|---|
| `ItemCollector` | *required* | Source of items to process |
| `Pipeline` | *required* | Compiled middleware delegate |
| `KeySelector` | *required* | `TItem -> string` key extraction |
| `OutputDir` | *required* | Output directory path |
| `OutputFilePattern` | `"{key}.json"` | File name pattern (`{key}` is replaced) |
| `DefaultParallelism` | `6` | Max concurrent downloads |
| `UserAgent` | `"FrenchExDev-DesignPipeline/1.0"` | HTTP User-Agent header |
| `AuthTokenEnvVar` | `"GITHUB_TOKEN"` | Env var for Bearer token (null to disable) |
| `ItemFilter` | `null` | Post-collection filter function |
| `MinLogLevel` | `Information` | Minimum log level |

### CLI Arguments

The runner parses CLI arguments passed to `RunAsync(args)`:

| Argument | Description |
|---|---|
| `--parallel N` | Override parallelism (default: 6) |
| `--output DIR` | Override output directory |
| `--list` | Print item keys and exit (no processing) |
| `--missing` | Only process items without existing output files |

```bash
dotnet run -- --missing --parallel 4
dotnet run -- --list
```

### Exit Codes

| Code | Meaning |
|---|---|
| `0` | All items processed successfully (or `--list`) |
| `1` | One or more items failed |

## Version Filters

```csharp
// Keep only the latest patch for each major.minor
// ["1.0.1", "1.0.3", "1.1.0", "1.1.2"] -> ["1.0.3", "1.1.2"]
var runner = new DesignPipelineRunner<string>
{
    ItemFilter = VersionFilters.LatestPatchPerMinor,
    // ...
};
```

## Generic TItem Support

The pipeline is not limited to string versions. Any type can be used as `TItem`:

```csharp
public record PluginEntry(string Name, string Version, string Url);

var runner = new DesignPipelineRunner<PluginEntry>
{
    ItemCollector = new StaticItemCollector<PluginEntry>(plugins),
    KeySelector   = p => $"{p.Name}-{p.Version}",
    Pipeline      = new DesignPipeline<PluginEntry>()
        .UseHttpDownload(p => p.Url)
        .UseSave()
        .Build(),
    OutputDir     = "output",
    OutputFilePattern = "{key}.json",
};
```

## Consumers

This library is the foundation for design-time tooling across the repository:

| Project | Usage |
|---|---|
| BinaryWrapper.Design | Scrape CLI help for Podman, Vagrant, glab, Git |
| DockerCompose.Bundle.Design | Download compose-spec JSON schemas (32 versions) |
| Docker.Design | Docker Engine version discovery |
| Packer.Bundle.Design | Download Packer plugin registry schemas |
| Traefik.Bundle.Design | Download Traefik configuration schemas |

## Quality Gate

- 142 xUnit tests
- Test quality score: 1.0
- Branch + line coverage via Cobertura
- Max cyclomatic complexity: 15
- Max cognitive complexity: 20

```bash
dotnet run --project ../QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test \
  -s Wrapper.Versioning/FrenchExDev.Net.Wrapper.Versioning.slnx
```
