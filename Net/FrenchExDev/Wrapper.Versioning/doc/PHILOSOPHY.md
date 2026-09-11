# Philosophy

Design rationale and key decisions behind FrenchExDev.Net.Wrapper.Versioning.

## Core Principle: One Library for All Design-Time Scraping

Every wrapper project (Docker, Podman, Vagrant, Packer, GitLab CLI, DockerCompose) needs the same infrastructure: discover versions from a remote API, download something for each version, transform it, and save it to disk. Before this library existed, each project reimplemented parallel downloading, CLI arg parsing, progress reporting, and pagination from scratch.

Wrapper.Versioning extracts this shared concern into a single library. The design-time `Program.cs` for any consumer is now 15-30 lines instead of 90+.

## Why Generic TItem, Not Just Strings

The first iteration (`JsonSchema.Design`) was hardwired to `string` versions. This worked for DockerCompose (download schema for version `"1.29.2"`) but not for Packer.Bundle.Design, which iterates a `PluginRegistryEntry` with fields like `Org`, `Repo`, `Path`, `PluginKind`, and `TypeName`.

Generalizing to `DesignPipeline<TItem>` lets the same middleware chain handle both cases:

- `DesignPipeline<string>` -- version-keyed downloads (schemas, binaries)
- `DesignPipeline<PluginRegistryEntry>` -- multi-field item downloads

The `KeySelector` function bridges the gap: it extracts a string key from any `TItem` for file naming and progress display.

## Why IVersionCollector Extends IItemCollector

`IVersionCollector` adds `CollectVersionsAsync()` for domain clarity -- callers working with versions use a name that matches the concept. But it extends `IItemCollector<string>` so that version collectors plug directly into `DesignPipelineRunner<TItem>` without adapters.

The explicit `CollectItemsAsync` implementation delegates to `CollectVersionsAsync`, avoiding code duplication while maintaining the generic interface contract.

## Why Middleware, Not Strategy or Template Method

The middleware pattern was chosen over alternatives for three reasons:

1. **Composability** -- stages can be mixed and matched per consumer. DockerCompose uses download + save. Packer uses download + transform + save. Custom projects can insert validation, caching, or logging between any stages.

2. **No inheritance** -- template method patterns require subclassing, which couples consumers to a base class hierarchy. Middleware is purely compositional.

3. **Familiarity** -- the pattern mirrors ASP.NET Core's request pipeline, which .NET developers already know. The `Use(next => async ctx => { ... await next(ctx); })` shape is immediately recognizable.

## Why LIFO Composition

Middleware is composed in reverse order (last added = innermost). This matches how humans read pipeline setup code:

```csharp
pipeline
    .UseHttpDownload(...)      // runs first
    .UseContentTransform(...)  // runs second
    .UseSave()                 // runs third
```

The execution order matches the declaration order, even though internally each stage wraps the next.

## Why a Single Code.cs

The entire public API fits in 622 lines. Splitting it into 15+ files would add navigation overhead without improving clarity. The file uses section headers (`// -- Version Collector --`) that make navigation trivial.

This is a deliberate tradeoff: a library this focused benefits from having its entire surface visible in one scroll.

## Why SemaphoreSlim, Not Parallel.ForEachAsync

`Parallel.ForEachAsync` (introduced in .NET 6) is a cleaner API for simple cases, but the runner needs:

- Per-item exception handling without aborting the batch
- Interlocked counters for success/failure tracking
- Shared HttpClient across all tasks
- Individual progress reporting

`SemaphoreSlim` + `Task.WhenAll` provides the necessary control without hiding error handling behind framework abstractions.

## Why CompareVersionStrings Lives on GitHubReleasesVersionCollector

The version comparison algorithm is used by `PaginationHelper.CollectPaginatedAsync` to sort results. Placing it on `GitHubReleasesVersionCollector` as a public static method keeps it discoverable (it's the most commonly used collector) while avoiding a separate utility class for a single method.

`VersionFilters.LatestPatchPerMinor` also depends on version parsing, but uses its own `int.TryParse` on split segments -- it doesn't need the full comparison algorithm.

## Authentication Design

Each collector creates its own `HttpClient` with appropriate auth headers:

- **GitHub collectors** -- no auth by default (public API), but the runner injects a Bearer token via `AuthTokenEnvVar`
- **GitLab collector** -- reads `GITLAB_TOKEN` env var and adds a `PRIVATE-TOKEN` header (GitLab's v4 API convention)

The runner's `HttpClient` (with Bearer token) is separate from the collectors' clients. Collectors use their own clients during version discovery; the runner's client is used during pipeline execution (downloading artifacts). This separation allows different auth for discovery vs. download.

## Pre-Release Filtering

Each collector has its own pre-release filtering strategy:

| Collector | Filter | Rationale |
|-----------|--------|-----------|
| GitHubReleasesVersionCollector | `prerelease: true` JSON field | GitHub marks pre-releases explicitly |
| GitHubTagsVersionCollector | Tags containing `-` | Semver convention: `1.0.0-rc.1` |
| GitLabReleasesVersionCollector | `upcoming_release: true` JSON field | GitLab's equivalent of pre-release |
| StaticVersionCollector | None | Caller controls the input list |

This avoids a one-size-fits-all filter that would miss platform-specific conventions.

## What This Library Does NOT Do

- **Parse downloaded content** -- that's the consumer's job (via `UseContentTransform` or custom middleware)
- **Validate schemas or artifacts** -- consumers add validation middleware when needed
- **Cache HTTP responses** -- the `--missing` flag provides file-level caching; HTTP-level caching is left to the consumer's middleware
- **Provide a NuGet package** -- this is an internal library consumed via `ProjectReference`
- **Support non-HTTP sources** -- if a future consumer needs file-system or database sources, it implements `IItemCollector<TItem>` directly
