# WRAPPER-VERSIONING — Philosophy

A *wrapper-versioning* library extracts the design-time concerns shared by every "scrape stuff from the internet for each version of X" tool: discover items from a remote API, download something per item in parallel, transform it, and write it to disk. This is the foundation under any binary wrapper, schema bundler, or plugin registry generator.

The reference implementation lives at `Net/FrenchExDev/Wrapper.Versioning/`.

## One Library for All Design-Time Scraping

Before this pattern exists, every wrapper project (Docker, Podman, Vagrant, schema bundlers, plugin registries) reimplements the same code: parallel downloading, CLI arg parsing, progress reporting, GitHub pagination, rate-limit handling. The result is N nearly-identical 90-line `Program.cs` files that drift apart over time.

This pattern collapses that surface into:

- **One** middleware pipeline (`DesignPipeline<TItem>`).
- **One** parallel runner (`DesignPipelineRunner<TItem>`).
- **A handful** of pluggable item collectors (GitHub releases, GitHub tags, GitLab releases, static).

A consumer's `Program.cs` becomes 15–30 lines: build a pipeline, configure a runner, call `RunAsync(args)`. Done.

## Generic `TItem`, Not Just Strings

The first iteration was hardwired to `string` versions. That works for "download schema for version `1.29.2`", but not for "download a plugin manifest for `(org=hashicorp, repo=docker, kind=builder, type=DockerBuilder)`". Generalising to `DesignPipeline<TItem>` means the same middleware chain handles both:

- `DesignPipeline<string>` — version-keyed downloads (binaries, schemas, scrape JSON).
- `DesignPipeline<PluginRegistryEntry>` — multi-field item-keyed downloads.

A `KeySelector : TItem -> string` bridges the gap. The runner uses the key for file naming, progress display, and the `--missing` filter. The pipeline middleware reads the full `TItem`.

`IVersionCollector` is just `IItemCollector<string>` with a domain-friendly name. Version collectors plug into `DesignPipelineRunner<string>` without adapters.

## Middleware, Not Strategy

ASP.NET Core's request pipeline shape is the right shape:

```csharp
pipeline
    .UseHttpDownload(item => urlFor(item))   // sets ctx.Content
    .UseContentTransform((item, c) => ...)   // mutates ctx.Content
    .UseSave();                              // writes ctx.Content to disk
```

Why middleware over strategy or template method:

1. **Composability** — stages can be mixed per consumer. One project does download + save, another adds validation, another inserts a transform. No combinatorial explosion of subclasses.
2. **No inheritance** — template-method designs couple consumers to a base class. Middleware is purely compositional.
3. **Familiar** — every .NET developer already understands `Use(next => async ctx => { ... await next(ctx); })`.

Custom middleware is one delegate, not a class:

```csharp
pipeline.Use(next => async ctx =>
{
    ctx.Progress?.SetStage("Validating");
    JsonDocument.Parse(ctx.Content!);
    await next(ctx);
});
```

## Per-Collector Pre-Release Filtering

There is no one-size-fits-all pre-release filter. Each upstream uses its own convention, and the collector encodes that convention:

| Collector | Filter |
|---|---|
| GitHub releases | `prerelease: true` JSON field |
| GitHub tags | tag contains `-` (semver convention: `1.0.0-rc.1`) |
| GitLab releases | `upcoming_release: true` JSON field |
| Static | none — caller controls input |

A wrapper that wants to include pre-releases overrides via a custom collector or post-collection `ItemFilter`. The default is "stable releases only" because that is what wrapper consumers want 99% of the time.

## Authentication Is the Collector's Concern, Not the Caller's

Each collector knows what header its API requires:

- GitHub uses `Authorization: Bearer <token>`.
- GitLab uses `PRIVATE-TOKEN: <token>` (v4 API convention).

Tokens come from environment variables (`GITHUB_TOKEN`, `GITLAB_TOKEN`) loaded by `DotEnvLoader` from a `.env` file via recursive upward filesystem traversal. A single `.env` placed at the monorepo root serves every consumer.

Why this matters: **without a token, GitHub rate-limits to 60 requests/hour.** Scraping a few dozen versions exhausts the quota and returns `403`. Token support is not optional — it is the difference between a wrapper that works and one that doesn't.

## SemVer-Aware Comparison Without a SemVer Library

Versions are sorted with a hand-rolled comparator that handles:

- Numeric segments (`1.10.0` > `1.2.3`).
- Pre-release suffixes (`1.0.0-alpha` < `1.0.0-beta` < `1.0.0`).
- Build metadata (ignored, per semver spec).

It lives as a static method on `GitHubReleasesVersionCollector` because that is the most-used collector and the algorithm has zero external dependencies. Adding `Semver.NET` is overkill for sorting tag strings.

## What This Library Does NOT Do

- **It does not parse downloaded content.** That is the consumer's job, via `UseContentTransform` or custom middleware.
- **It does not validate schemas.** Consumers add validation middleware when needed.
- **It does not cache HTTP responses.** The `--missing` flag provides file-level caching; HTTP-level caching is left to the consumer.
- **It does not ship as a NuGet package.** It is internal, consumed via `ProjectReference`.
- **It does not support non-HTTP sources.** Implement `IItemCollector<TItem>` directly for filesystem or database sources.

These boundaries are deliberate. The library does collection, parallelism, and the middleware shell. Everything else is the consumer's domain.
