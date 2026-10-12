# WRAPPER-VERSIONING — Architecture

## Type Hierarchy

```
IItemCollector<TItem>
  Task<IReadOnlyList<TItem>> CollectItemsAsync(CancellationToken ct);

IVersionCollector : IItemCollector<string>
  Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken ct);
  // explicit IItemCollector<string>.CollectItemsAsync delegates to CollectVersionsAsync

GitHubReleasesVersionCollector : IVersionCollector
GitHubTagsVersionCollector     : IVersionCollector
GitLabReleasesVersionCollector : IVersionCollector
StaticVersionCollector         : IVersionCollector
StaticItemCollector<T>         : IItemCollector<T>
```

`IVersionCollector` is the domain-friendly name for "give me a list of version strings". It is structurally an `IItemCollector<string>`, so version collectors plug into `DesignPipelineRunner<TItem>` without an adapter.

## Pipeline Composition

```
DesignPipeline<TItem>
  Use(Func<PipelineDelegate<TItem>, PipelineDelegate<TItem>> middleware)
  Build() -> PipelineDelegate<TItem>

UseHttpDownload(urlBuilder)        // sets ctx.Content
UseContentTransform(transform)     // mutates ctx.Content
UseSave()                          // writes ctx.Content to ctx.OutputFilePath
Use(custom)                        // arbitrary middleware
```

LIFO composition: middleware added later wraps middleware added earlier, so execution order matches declaration order:

```csharp
pipeline
    .UseHttpDownload(...)        // runs 1st
    .UseContentTransform(...)    // runs 2nd
    .UseSave();                  // runs 3rd
```

Internally `Build()` folds the list right-to-left so the first-added middleware ends up the outermost wrapper.

## Pipeline Context

`DesignPipelineContext<TItem>` is the mutable bag passed through every middleware:

| Property | Type | Mutable | Set by |
|---|---|---|---|
| `Item` | `TItem` | no | runner (per-iteration) |
| `Key` | `string` | no | runner (via `KeySelector`) |
| `OutputDir` | `string` | no | runner |
| `OutputFilePattern` | `string` | no | runner |
| `Content` | `string?` | yes | `UseHttpDownload` / `UseContentTransform` / custom |
| `OutputFilePath` | `string?` | yes | `UseSave` |
| `HttpClient` | `HttpClient` | no | runner (shared) |
| `Logger` | `ILogger` | no | runner |
| `Progress` | `DesignPipelineProgressInfo?` | yes (thread-safe) | runner |

`HttpClient` is constructed once by the runner with the runner's auth header attached, and shared across every parallel worker. Collectors create their own clients during version discovery — see "Two Layers of HTTP" below.

## Pipeline Runner

```csharp
new DesignPipelineRunner<TItem>
{
    ItemCollector     = ...,                    // required
    Pipeline          = pipeline,               // required
    KeySelector       = item => item.ToKey(),   // required
    OutputDir         = "src/MyProject/scrape", // required
    OutputFilePattern = "schema-{key}.json",    // default "{key}.json"
    DefaultParallelism = 6,
    UserAgent         = "FrenchExDev-DesignPipeline/1.0",
    AuthTokenEnvVar   = "GITHUB_TOKEN",         // null disables
    ItemFilter        = items => items,         // post-collection filter
    MinLogLevel       = LogLevel.Information,
}.RunAsync(args);
```

## CLI Arguments (Parsed by `RunAsync`)

| Flag | Effect |
|---|---|
| `--parallel N` | Override worker count (default 6) |
| `--output DIR` | Override `OutputDir` |
| `--list` | Print item keys and exit (no processing) |
| `--missing` | Skip items whose output file already exists |

The argument parser is intentionally minimal — it is just a few `args.Contains` checks. Consumers that need richer CLI use their own parser before calling `RunAsync`.

## Parallel Worker Topology

```
collect items -> filter -> SemaphoreSlim(parallelism)
   |
   +-- task 1: clone ctx, run pipeline, increment counters
   +-- task 2: ...
   +-- task N: ...
   |
Task.WhenAll -> aggregate success / failure counts -> exit code
```

`SemaphoreSlim` is chosen over `Parallel.ForEachAsync` because the runner needs:

- Per-item exception handling without aborting the batch.
- Interlocked counters for success/failure tracking.
- A single shared `HttpClient` across all tasks.
- Individual progress reporting per item.

## Two Layers of HTTP

Authentication is split between two clients with different lifetimes and configurations:

```
Discovery (collector-owned HttpClient)
  - lives inside the IVersionCollector implementation
  - configures its own auth header (Bearer for GitHub, PRIVATE-TOKEN for GitLab)
  - used during CollectVersionsAsync only

Pipeline execution (runner-shared HttpClient)
  - constructed by DesignPipelineRunner
  - attaches Bearer token from AuthTokenEnvVar (if set)
  - shared across all parallel workers via DesignPipelineContext.HttpClient
  - used by UseHttpDownload and any custom middleware
```

This separation is deliberate: discovery (GitHub API) and download (CDN, releases.hashicorp.com) often live on different hosts and want different headers.

## Built-in Collectors

```csharp
// GitHub releases (preferred — explicit prerelease flag)
new GitHubReleasesVersionCollector("containers", "podman");
new GitHubReleasesVersionCollector("owner", "repo",
    tagToVersion: tag => tag.Replace("release-", ""),
    token: githubToken);

// GitHub tags (use when releases don't exist or don't track CLI versions)
new GitHubTagsVersionCollector("docker", "cli");

// GitLab releases v4
new GitLabReleasesVersionCollector("gitlab-org%2Fcli");
new GitLabReleasesVersionCollector("group%2Fproj", baseUrl: "https://gitlab.example.com");

// Static
new StaticVersionCollector(["1.0.0", "1.1.0", "2.0.0"]);
new StaticItemCollector<MyItem>(items);
```

All GitHub/GitLab collectors handle pagination internally via `PaginationHelper.CollectPaginatedAsync`, which sorts results by `GitHubReleasesVersionCollector.CompareVersionStrings` (a static semver-aware comparator).

## Version Filters

Reusable post-collection filters live in `VersionFilters`:

```csharp
ItemFilter = VersionFilters.LatestPatchPerMinor;
// ["1.0.1", "1.0.3", "1.1.0", "1.1.2"] -> ["1.0.3", "1.1.2"]
```

`LatestPatchPerMinor` does its own `int.TryParse` on segments — it does not depend on the full comparator. This avoids coupling the filter to the comparison algorithm.

## Anchors

| File | Purpose |
|---|---|
| `Net/FrenchExDev/Wrapper.Versioning/src/.../Code.cs` | Single file hosting the entire public API (~622 lines) |
| `Net/FrenchExDev/Wrapper.Versioning/doc/PHILOSOPHY.md` | Design rationale |
| `Net/FrenchExDev/Wrapper.Versioning/doc/TOKENS.md` | Auth token configuration |
| `Net/FrenchExDev/BinaryWrapper/src/.../Design.Lib/DotEnvLoader.cs` | `.env` recursive loader |
| `Net/FrenchExDev/Podman/src/.../Podman.Design/Program.cs` | Real consumer using `GitHubReleasesVersionCollector` |
| `Net/FrenchExDev/GitLab.Cli/src/.../GitLab.Cli.Design/Program.cs` | Real consumer using `GitLabReleasesVersionCollector` |
