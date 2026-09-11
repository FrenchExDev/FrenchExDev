# WRAPPER-VERSIONING — How To

## Scenario 1 — Download a JSON Schema per Version

Goal: download `https://example.com/schemas/{version}/schema.json` for every release of `acme/widget` on GitHub, save as `schema-{version}.json`.

```csharp
using FrenchExDev.Net.Wrapper.Versioning;

var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(version => $"https://example.com/schemas/{version}/schema.json")
    .UseSave()
    .Build();

return await new DesignPipelineRunner<string>
{
    ItemCollector     = new GitHubReleasesVersionCollector("acme", "widget"),
    Pipeline          = pipeline,
    KeySelector       = v => v,
    OutputDir         = Path.GetFullPath(Path.Combine("..", "src", "MyProject", "schemas")),
    OutputFilePattern = "schema-{key}.json",
}.RunAsync(args);
```

Run it:

```bash
dotnet run -- --parallel 8
dotnet run -- --missing       # only fetch new versions
dotnet run -- --list          # print versions, do nothing
```

## Scenario 2 — Transform Content Before Saving

Add `UseContentTransform` between download and save:

```csharp
var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(v => $"https://example.com/schemas/{v}/schema.json")
    .UseContentTransform((v, content) => content
        .Replace("draft-07", "draft-2020-12")
        .Replace("$ref-stub", $"https://schemas.example.com/{v}/"))
    .UseSave()
    .Build();
```

`UseContentTransform` mutates `ctx.Content` in place. Subsequent middleware sees the transformed content.

## Scenario 3 — Custom Middleware (Validation)

Insert arbitrary middleware via `Use(next => async ctx => ...)`:

```csharp
var pipeline = new DesignPipeline<string>()
    .UseHttpDownload(v => $"https://example.com/schemas/{v}/schema.json")
    .Use(next => async ctx =>
    {
        ctx.Progress?.SetStage("Validating");
        try
        {
            using var doc = JsonDocument.Parse(ctx.Content!);
            doc.RootElement.GetProperty("$schema");
        }
        catch (JsonException ex)
        {
            ctx.Logger.LogWarning(ex, "Invalid schema for {Key}", ctx.Key);
            return; // skip save
        }
        await next(ctx);
    })
    .UseSave()
    .Build();
```

Returning early from a middleware aborts the rest of the pipeline for that item. The runner counts it as a success unless an exception escapes.

## Scenario 4 — Non-String Items (Plugin Registry)

Use `DesignPipeline<TItem>` with any record type. Provide a `KeySelector` to give each item a stable file name.

```csharp
public sealed record PluginEntry(string Org, string Repo, string Kind);

var entries = new[]
{
    new PluginEntry("hashicorp", "docker", "builder"),
    new PluginEntry("hashicorp", "amazon", "builder"),
};

var pipeline = new DesignPipeline<PluginEntry>()
    .UseHttpDownload(p => $"https://github.com/{p.Org}/{p.Repo}/raw/main/plugin.json")
    .UseSave()
    .Build();

return await new DesignPipelineRunner<PluginEntry>
{
    ItemCollector     = new StaticItemCollector<PluginEntry>(entries),
    Pipeline          = pipeline,
    KeySelector       = p => $"{p.Org}-{p.Repo}-{p.Kind}",
    OutputDir         = "plugins",
    OutputFilePattern = "{key}.json",
}.RunAsync(args);
```

## Scenario 5 — Authenticated GitHub / GitLab

Place a `.env` at `Net/FrenchExDev/.env`:

```
GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
GITLAB_TOKEN=glpat-xxxxxxxxxxxxxxxxxxxx
```

The runner reads the env var named in `AuthTokenEnvVar` (default `GITHUB_TOKEN`) and attaches it as `Authorization: Bearer ...` on the shared `HttpClient`.

For collector-side auth (during version discovery), pass the token explicitly:

```csharp
var env = DotEnvLoader.Load();
env.TryGetValue("GITHUB_TOKEN", out var githubToken);

var collector = new GitHubReleasesVersionCollector(
    "containers", "podman", token: githubToken);

var collector2 = new GitLabReleasesVersionCollector(
    "gitlab-org%2Fcli"); // reads GITLAB_TOKEN from env directly
```

`DotEnvLoader.Load()` walks up the filesystem from the current directory looking for `.env`, so it works regardless of where `dotnet run` is launched.

## Scenario 6 — Filtering with `LatestPatchPerMinor`

Reduce a noisy version list to one per minor:

```csharp
return await new DesignPipelineRunner<string>
{
    ItemCollector = new GitHubReleasesVersionCollector("acme", "widget"),
    Pipeline      = pipeline,
    KeySelector   = v => v,
    OutputDir     = "out",
    ItemFilter    = VersionFilters.LatestPatchPerMinor,
}.RunAsync(args);
```

`ItemFilter` runs after collection, before parallel processing.

## Scenario 7 — Custom Tag-to-Version Mapping

Some repos prefix tags with `release-` or use a non-`v` prefix:

```csharp
var collector = new GitHubReleasesVersionCollector(
    "owner", "repo",
    tagToVersion: tag => tag.StartsWith("release-")
        ? tag.Substring("release-".Length)
        : null);   // returning null skips the tag
```

Returning `null` from `tagToVersion` filters the tag out entirely.

## Scenario 8 — Custom Collector (Non-GitHub Source)

Implement `IVersionCollector` for sources outside GitHub / GitLab. It is a 1-method interface.

```csharp
public sealed class HashicorpVersionCollector : IVersionCollector
{
    private readonly HttpClient _http = new();

    public async Task<IReadOnlyList<string>> CollectVersionsAsync(
        CancellationToken ct = default)
    {
        var json = await _http.GetStringAsync(
            "https://releases.hashicorp.com/packer/index.json", ct);
        using var doc = JsonDocument.Parse(json);
        var versions = doc.RootElement
            .GetProperty("versions")
            .EnumerateObject()
            .Select(p => p.Name)
            .Where(v => !v.Contains('-')) // drop pre-releases
            .ToList();
        versions.Sort(GitHubReleasesVersionCollector.CompareVersionStrings);
        return versions;
    }
}
```

## Scenario 9 — Combine Two Collectors

Concatenate or merge collectors using a small wrapper:

```csharp
public sealed class CombinedCollector(params IVersionCollector[] inner) : IVersionCollector
{
    public async Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken ct = default)
    {
        var all = new List<string>();
        foreach (var c in inner)
            all.AddRange(await c.CollectVersionsAsync(ct));
        all.Sort(GitHubReleasesVersionCollector.CompareVersionStrings);
        return all.Distinct().ToList();
    }
}
```

## Common Gotchas

- **Without a token, GitHub returns 403 after ~60 requests.** Set `GITHUB_TOKEN` before running.
- **`--missing` only checks file existence**, not content. Delete a file to force re-download.
- **`ItemFilter` runs after collection.** It does not save API calls — it only reduces the number of items processed.
- **`OutputFilePattern` must contain `{key}`.** Without it every item overwrites the same file.
- **`HttpClient` is shared across workers.** Do not mutate its `DefaultRequestHeaders` from inside middleware.
- **`UseHttpDownload` only does GET.** For POST/PUT, write a custom middleware.
- **A returning-early middleware does not raise an error.** It just stops the chain. The item counts as success.
- **`tagToVersion` returning `null` filters the tag.** Useful for skipping non-release tags.
