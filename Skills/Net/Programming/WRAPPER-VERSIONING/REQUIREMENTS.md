# WRAPPER-VERSIONING — Requirements

Hard rules for any project that uses or extends the wrapper-versioning pattern.

## Item Collectors

- **MUST** implement `IItemCollector<TItem>` (or `IVersionCollector` for the string case).
- **MUST** be safe to call concurrently. The runner does not serialize collector access.
- **MUST** sort returned items deterministically. Use `GitHubReleasesVersionCollector.CompareVersionStrings` for version strings.
- **MUST** filter pre-releases by default. The semantic of a "version" is "stable release".
- **MAY** accept a `tagToVersion` lambda for non-`v`-prefixed tags. Returning `null` from the lambda **MUST** filter the tag out entirely.
- **MAY** accept an explicit `HttpClient`. When provided, the collector **MUST NOT** add auth headers — the caller manages auth.
- **MUST NOT** throw on empty results. An empty list is valid.

## Pipeline Composition

- **MUST** be assembled via `DesignPipeline<TItem>.Use*()` and finalised with `.Build()`.
- **MUST NOT** invoke middleware manually outside the runner.
- **SHOULD** order middleware as: download → transform → validate → save. Other orderings work but surprise readers.
- **MUST** call `await next(ctx)` from custom middleware unless intentionally short-circuiting.
- **MUST NOT** mutate `ctx.Item`, `ctx.Key`, `ctx.OutputDir`, or `ctx.OutputFilePattern`. Only `Content`, `OutputFilePath`, and `Progress` are mutable.

## Pipeline Runner

- **MUST** be configured with `ItemCollector`, `Pipeline`, `KeySelector`, and `OutputDir`. These are non-optional.
- **MUST** define `OutputFilePattern` containing `{key}` if writing files. Default `"{key}.json"` works for most consumers.
- **MUST** call `RunAsync(args)` from `Main` and return its int result. The runner returns `0` on success, `1` on any failure.
- **MUST** keep `DefaultParallelism` modest (default 6). Aggressive parallelism will trip rate limits.
- **MUST NOT** wrap `RunAsync` in `try/catch` for happy-path exit codes. Per-item exceptions are already handled.

## Authentication

- **MUST** support token loading from environment variables. The pattern is `GITHUB_TOKEN` for GitHub, `GITLAB_TOKEN` for GitLab.
- **MUST** load `.env` via `DotEnvLoader.Load()` (recursive upward traversal). A single `.env` at the monorepo root serves every consumer.
- **MUST** keep `.env` in `.gitignore`. Never commit a token.
- **MUST NOT** hard-code tokens in `Program.cs`.
- **MAY** disable auth by setting `AuthTokenEnvVar = null`.

## Concurrency & Resource Sharing

- **MUST** share a single `HttpClient` across pipeline workers (the runner does this automatically).
- **MUST NOT** create per-item `HttpClient` instances inside middleware.
- **MUST NOT** mutate `HttpClient.DefaultRequestHeaders` from middleware. Pass headers via `HttpRequestMessage` if needed.
- **MUST** use `ctx.Logger` for logging. The runner provides a per-item logger with the key in scope.
- **MUST** use `ctx.Progress?.SetStage(...)` for stage updates. Progress is null when no progress sink is attached.

## CLI Argument Handling

- **MUST** support `--parallel N`, `--output DIR`, `--list`, `--missing`. The runner parses these from `args`.
- **SHOULD NOT** add a separate CLI parser unless additional flags are needed. Pre-parse before calling `RunAsync` if you must.
- **MUST** treat `--list` as side-effect-free. No downloads, no file writes.
- **MUST** treat `--missing` as a file-existence check on `OutputFilePath`. Do not check content.

## Error Handling

- **MUST** count per-item failures via interlocked counters. Do not abort the batch on a single failure.
- **MUST** log failures with the item key in context.
- **MUST** return non-zero exit code when any item failed.
- **MUST NOT** silently swallow `HttpRequestException` — log it.
- **MUST** propagate `OperationCanceledException` (Ctrl+C) without counting as failure.

## Generic `TItem` Support

- **MAY** use any reference or value type as `TItem` (immutable record preferred).
- **MUST** define a `KeySelector : TItem -> string` that yields a stable, file-safe key.
- **MUST NOT** use `KeySelector = item => item.ToString()` unless `ToString()` is overridden to a stable form.
- **SHOULD** prefer `record` over `class` for `TItem`. Records are immutable and value-equal by default.

## What Collectors Are NOT Responsible For

- **MUST NOT** download artifacts. Discovery only.
- **MUST NOT** write files. Discovery only.
- **MUST NOT** parse content (JSON schemas, binaries, etc.). That is the pipeline's job.
- **MUST NOT** depend on `BinaryWrapper` or any consumer-specific abstraction.

## Forbidden

- **NEVER** add a NuGet dependency on `Wrapper.Versioning`. It is consumed via `ProjectReference` only.
- **NEVER** use `Parallel.ForEachAsync` instead of the runner's `SemaphoreSlim` topology. The runner needs per-item exception isolation that `Parallel.ForEachAsync` does not provide cleanly.
- **NEVER** hard-code parallelism. Default is 6, override via `DefaultParallelism` or `--parallel`.
- **NEVER** introduce a HTTP cache layer. File-level caching via `--missing` is sufficient.
- **NEVER** add a YAML / TOML / INI parser. Configuration is constructor properties on the runner.
