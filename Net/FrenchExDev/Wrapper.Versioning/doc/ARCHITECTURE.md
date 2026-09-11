# Architecture

This document describes the system design of FrenchExDev.Net.Wrapper.Versioning -- the shared infrastructure library for version discovery and design-time artifact processing across all wrapper projects.

## Overview

The library has two distinct subsystems: **Version Collection** and **Design Pipeline**. Together they form the foundation that every design-time project builds on.

```
┌──────────────────────────────────────────────────────────────────────┐
│                      VERSION COLLECTION                              │
│                                                                      │
│  IItemCollector<TItem>                                               │
│    │                                                                 │
│    ├── IVersionCollector : IItemCollector<string>                     │
│    │     ├── GitHubReleasesVersionCollector  (releases API, no pre)   │
│    │     ├── GitHubTagsVersionCollector      (tags API, no pre)       │
│    │     ├── GitLabReleasesVersionCollector  (v4 API, no upcoming)    │
│    │     └── StaticVersionCollector          (pre-computed list)      │
│    │                                                                 │
│    └── StaticItemCollector<TItem>            (pre-computed list)      │
│                                                                      │
│  PaginationHelper                           (RFC 8288 Link header)   │
│  CompareVersionStrings                      (semver-aware sort)      │
│  VersionFilters.LatestPatchPerMinor         (group by major.minor)   │
└──────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────────┐
│                       DESIGN PIPELINE                                │
│                                                                      │
│  DesignPipeline<TItem>                                               │
│    Composable middleware chain (LIFO)                                 │
│    │                                                                 │
│    ├── UseHttpDownload(urlBuilder)    → sets ctx.Content              │
│    ├── UseContentTransform(fn)       → transforms ctx.Content        │
│    ├── UseSave()                     → writes ctx.Content to disk    │
│    └── Use(custom)                   → any custom middleware         │
│                                                                      │
│  DesignPipelineRunner<TItem>                                         │
│    CLI arg parsing → collect → filter → parallel process             │
│    │                                                                 │
│    ├── --list       (print keys)                                     │
│    ├── --missing    (skip existing)                                  │
│    ├── --parallel N (concurrency)                                    │
│    └── --output DIR (override dir)                                   │
└──────────────────────────────────────────────────────────────────────┘
```

## Type Hierarchy

### Item Collection

The collection layer uses a two-level interface hierarchy:

```
IItemCollector<TItem>           Generic: any item type
    │
    ├── IVersionCollector       Specialized: string versions
    │     CollectVersionsAsync() + CollectItemsAsync() (delegates)
    │
    └── (any custom impl)       E.g., PluginRegistryEntry collector
```

`IVersionCollector` extends `IItemCollector<string>` so that version collectors work seamlessly with the generic `DesignPipelineRunner<TItem>`. The explicit `CollectItemsAsync` implementation delegates to `CollectVersionsAsync`, preserving backward compatibility.

### Pipeline Middleware

The middleware chain follows the same pattern as ASP.NET Core middleware -- each stage wraps the next, with LIFO composition:

```
Use(A) → Use(B) → Use(C)

Execution order:  A → B → C → terminal
Build order:      terminal ← C ← B ← A
```

Each middleware receives a `DesignPipelineDelegate<TItem>` (the next stage) and returns a new delegate that wraps it. The `Build()` method composes them from right to left, starting with a no-op terminal.

### Context Flow

All middleware shares a mutable `DesignPipelineContext<TItem>`:

```
UseHttpDownload         UseContentTransform         UseSave
    │                        │                         │
    ├─ reads: Item           ├─ reads: Item, Content   ├─ reads: Content
    ├─ sets:  Content        ├─ sets:  Content         ├─ sets:  OutputFilePath
    └─ calls: next ──────────┘─ calls: next ───────────┘─ calls: next (terminal)
```

The context also carries `Key`, `OutputDir`, `OutputFilePattern`, `Logger`, `HttpClient`, and an optional `DesignPipelineProgressInfo` for thread-safe stage tracking.

## Runner Architecture

`DesignPipelineRunner<TItem>` orchestrates the complete workflow:

```
1. ParseArgs(args)
   └── RunOptions(parallel, outputDir, listOnly, missingOnly)

2. CollectAndFilter(logger, outputDir, missingOnly)
   ├── ItemCollector.CollectItemsAsync()
   ├── ItemFilter?(allItems)
   └── FilterToMissing?(items, outputDir)
       └── DiscoverExistingKeys(outputDir)  // parses OutputFilePattern

3. ProcessAll(items, outputDir, parallel, logger)
   ├── CreateHttpClient()  // UserAgent + Bearer token from env var
   ├── SemaphoreSlim(parallel)
   └── Task.WhenAll(items.Select(async item => ...))
       ├── Create DesignPipelineContext<TItem>
       ├── Pipeline(ctx)
       ├── Success: Interlocked.Increment(succeeded)
       └── Error:   Interlocked.Increment(failed)
```

### Missing File Detection

`DiscoverExistingKeys` parses the `OutputFilePattern` (e.g., `"schema-v{key}.json"`) by splitting on `{key}` to get prefix and suffix. It then scans the output directory for files matching `prefix*suffix` and extracts the key from each filename. This allows `--missing` to skip items whose output files already exist.

### Authentication

The runner creates an `HttpClient` with a Bearer token from an environment variable (default: `GITHUB_TOKEN`). This propagates to all middleware via `ctx.HttpClient`. Set `AuthTokenEnvVar = null` to disable.

## Pagination

GitHub and GitLab APIs paginate large result sets using RFC 8288 Link headers:

```
Link: <https://api.github.com/repos/.../releases?page=2>; rel="next",
      <https://api.github.com/repos/.../releases?page=5>; rel="last"
```

`PaginationHelper.ParseNextLink` extracts the `rel="next"` URL. `CollectPaginatedAsync` follows these links until exhausted, collecting items from each page's JSON array. Results are sorted using `CompareVersionStrings`.

## Version Comparison

`CompareVersionStrings` provides semver-aware sorting:

1. Split on `.` and `-` (handles `1.0.0-rc.1`)
2. Compare segments left-to-right
3. Numeric segments compared as integers (`2 < 10`)
4. Non-numeric segments compared as ordinal strings (`alpha < beta`)
5. Shorter versions padded with `"0"` (`1.0` == `1.0.0`)

## Dependency Graph

```
Wrapper.Versioning
  ├── Microsoft.Extensions.Logging
  └── Microsoft.Extensions.Logging.Console
       │
       ├── BinaryWrapper.Design ──► BinaryWrapper.Design.Lib
       │     (Podman, Vagrant, Docker, Git, GitLab.Cli)
       │
       ├── DockerCompose.Bundle.Design
       │     (compose-spec JSON schemas)
       │
       ├── Packer.Bundle.Design
       │     (Packer plugin registry)
       │
       └── Traefik.Bundle.Design
             (Traefik configuration schemas)
```

## Project Structure

```
Wrapper.Versioning/
├── FrenchExDev.Net.Wrapper.Versioning.slnx
├── quality-gate.yml
├── coverage.runsettings
├── README.md
├── doc/
│   ├── ARCHITECTURE.md          (this file)
│   ├── PHILOSOPHY.md
│   ├── HOW-TO.md
│   ├── PLAN.md                  (original extraction plan)
│   └── PLAN-GENERALIZE.md       (TItem generalization plan)
├── src/
│   └── FrenchExDev.Net.Wrapper.Versioning/
│       ├── FrenchExDev.Net.Wrapper.Versioning.csproj
│       └── Code.cs              (622 lines, all public API)
└── test/
    └── FrenchExDev.Net.Wrapper.Versioning.Tests/
        ├── FrenchExDev.Net.Wrapper.Versioning.Tests.csproj
        └── Tests.cs             (142 tests, 2614 lines)
```

All library code lives in a single `Code.cs` file, organized into 9 sections with comment headers. This keeps the API surface visible at a glance and avoids the overhead of many small files for a focused library.

## Evolution

The library was extracted in two phases:

1. **Phase 1** (PLAN.md): `IVersionCollector` and its implementations moved from `BinaryWrapper.Design` to `Wrapper.Versioning`. The design pipeline and runner remained in `JsonSchema.Design`.

2. **Phase 2** (PLAN-GENERALIZE.md): The pipeline was generalized from `SchemaDesignPipeline` (string-only) to `DesignPipeline<TItem>` (any type) and moved into `Wrapper.Versioning`. `JsonSchema.Design` became obsolete and was deleted. `VersionFilters` also moved here.
