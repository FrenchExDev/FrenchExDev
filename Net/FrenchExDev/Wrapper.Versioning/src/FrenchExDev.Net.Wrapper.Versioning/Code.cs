using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.Wrapper.Versioning;

// -- Item Collector ----------------------------------------------------------

public interface IItemCollector<TItem>
{
    Task<IReadOnlyList<TItem>> CollectItemsAsync(CancellationToken cancellationToken = default);
}

public sealed class StaticItemCollector<TItem> : IItemCollector<TItem>
{
    private readonly IReadOnlyList<TItem> _items;

    public StaticItemCollector(IEnumerable<TItem> items) =>
        _items = items.ToList();

    public Task<IReadOnlyList<TItem>> CollectItemsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_items);
}

// -- Version Collector (extends IItemCollector<string>) ----------------------

public interface IVersionCollector : IItemCollector<string>
{
    Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken cancellationToken = default);
}

public sealed class StaticVersionCollector : IVersionCollector
{
    private readonly IReadOnlyList<string> _versions;

    public StaticVersionCollector(IEnumerable<string> versions) =>
        _versions = versions.ToList();

    public Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_versions);

    public Task<IReadOnlyList<string>> CollectItemsAsync(CancellationToken cancellationToken = default)
        => CollectVersionsAsync(cancellationToken);
}

// -- Shared pagination helpers -----------------------------------------------

internal static class PaginationHelper
{
    internal static string? ParseNextLink(System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Link", out var linkValues))
            return null;

        foreach (var link in linkValues)
        {
            foreach (var part in link.Split(','))
            {
                var trimmed = part.Trim();
                if (!trimmed.EndsWith("rel=\"next\"", StringComparison.Ordinal))
                    continue;

                var start = trimmed.IndexOf('<');
                var end = trimmed.IndexOf('>');
                if (start >= 0 && end > start)
                    return trimmed[(start + 1)..end];
            }
        }
        return null;
    }

    internal static async Task<List<string>> CollectPaginatedAsync(
        HttpClient httpClient,
        string startUrl,
        Func<JsonElement, string?> extractVersion,
        CancellationToken cancellationToken)
    {
        var versions = new List<string>();
        var url = (string?)startUrl;

        while (url is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var items = JsonSerializer.Deserialize<JsonElement>(json);

            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var version = extractVersion(item);
                    if (!string.IsNullOrEmpty(version))
                        versions.Add(version);
                }
            }

            url = ParseNextLink(response.Headers);
        }

        versions.Sort(GitHubReleasesVersionCollector.CompareVersionStrings);
        return versions;
    }
}

// -- GitHub Releases ---------------------------------------------------------

public sealed class GitHubReleasesVersionCollector : IVersionCollector
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly HttpClient _httpClient;
    private readonly Func<string, string> _tagToVersion;

    public GitHubReleasesVersionCollector(
        string owner, string repo,
        HttpClient? httpClient = null,
        Func<string, string>? tagToVersion = null)
    {
        _owner = owner;
        _repo = repo;
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _tagToVersion = tagToVersion ?? DefaultTagToVersion;
    }

    public async Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases?per_page=100";
        return await PaginationHelper.CollectPaginatedAsync(
            _httpClient, url, ExtractVersion, cancellationToken);
    }

    public Task<IReadOnlyList<string>> CollectItemsAsync(CancellationToken cancellationToken = default)
        => CollectVersionsAsync(cancellationToken);

    private string? ExtractVersion(JsonElement release)
    {
        if (!release.TryGetProperty("tag_name", out var tagProp))
            return null;
        var tag = tagProp.GetString();
        if (tag is null)
            return null;
        if (release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean())
            return null;
        return _tagToVersion(tag);
    }

    private static string DefaultTagToVersion(string tag) =>
        tag.StartsWith('v') ? tag[1..] : tag;

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "FrenchExDev-Wrapper");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return client;
    }

    public static int CompareVersionStrings(string a, string b)
    {
        var aParts = a.Split('.', '-');
        var bParts = b.Split('.', '-');
        var len = Math.Max(aParts.Length, bParts.Length);
        for (var i = 0; i < len; i++)
        {
            var aVal = i < aParts.Length ? aParts[i] : "0";
            var bVal = i < bParts.Length ? bParts[i] : "0";
            if (int.TryParse(aVal, out var ai) && int.TryParse(bVal, out var bi))
            {
                var cmp = ai.CompareTo(bi);
                if (cmp != 0) return cmp;
            }
            else
            {
                var cmp = string.Compare(aVal, bVal, StringComparison.Ordinal);
                if (cmp != 0) return cmp;
            }
        }
        return 0;
    }
}

// -- GitLab Releases ---------------------------------------------------------

/// <summary>
/// Collects release versions from the GitLab REST API v4.
/// </summary>
public sealed class GitLabReleasesVersionCollector : IVersionCollector
{
    private readonly string _projectPath;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly Func<string, string> _tagToVersion;

    public GitLabReleasesVersionCollector(
        string projectPath,
        string? baseUrl = null,
        HttpClient? httpClient = null,
        Func<string, string>? tagToVersion = null)
    {
        _projectPath = projectPath;
        _baseUrl = baseUrl?.TrimEnd('/') ?? "https://gitlab.com";
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _tagToVersion = tagToVersion ?? DefaultTagToVersion;
    }

    public async Task<IReadOnlyList<string>> CollectVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/releases?per_page=100";
        return await PaginationHelper.CollectPaginatedAsync(
            _httpClient, url, ExtractVersion, cancellationToken);
    }

    public Task<IReadOnlyList<string>> CollectItemsAsync(CancellationToken cancellationToken = default)
        => CollectVersionsAsync(cancellationToken);

    private string? ExtractVersion(JsonElement release)
    {
        if (!release.TryGetProperty("tag_name", out var tagProp))
            return null;
        var tag = tagProp.GetString();
        if (tag is null)
            return null;
        if (release.TryGetProperty("upcoming_release", out var upcoming) && upcoming.GetBoolean())
            return null;
        return _tagToVersion(tag);
    }

    private static string DefaultTagToVersion(string tag) =>
        tag.StartsWith('v') ? tag[1..] : tag;

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "FrenchExDev-Wrapper");

        var token = Environment.GetEnvironmentVariable("GITLAB_TOKEN");
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);

        return client;
    }
}

// -- GitHub Tags -------------------------------------------------------------

/// <summary>
/// Collects versions from GitHub repository tags (not releases).
/// </summary>
public sealed class GitHubTagsVersionCollector : IVersionCollector
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly HttpClient _httpClient;
    private readonly Func<string, string?> _tagToVersion;

    public GitHubTagsVersionCollector(
        string owner, string repo,
        HttpClient? httpClient = null,
        Func<string, string?>? tagToVersion = null)
    {
        _owner = owner;
        _repo = repo;
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _tagToVersion = tagToVersion ?? DefaultTagToVersion;
    }

    public async Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{_owner}/{_repo}/tags?per_page=100";
        return await PaginationHelper.CollectPaginatedAsync(
            _httpClient, url, ExtractVersion, cancellationToken);
    }

    public Task<IReadOnlyList<string>> CollectItemsAsync(CancellationToken cancellationToken = default)
        => CollectVersionsAsync(cancellationToken);

    private string? ExtractVersion(JsonElement tag)
    {
        if (!tag.TryGetProperty("name", out var nameProp))
            return null;
        var name = nameProp.GetString();
        if (name is null)
            return null;
        return _tagToVersion(name);
    }

    private static string? DefaultTagToVersion(string tag)
    {
        var version = tag.StartsWith('v') ? tag[1..] : tag;
        if (version.Contains('-'))
            return null;
        return version;
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "FrenchExDev-Wrapper");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return client;
    }
}

// -- Generic Design Pipeline -------------------------------------------------

public delegate Task DesignPipelineDelegate<TItem>(DesignPipelineContext<TItem> ctx);

public sealed class DesignPipelineContext<TItem>
{
    public required TItem Item { get; init; }
    public required string Key { get; init; }
    public required string OutputDir { get; init; }
    public required ILogger Logger { get; init; }
    public required HttpClient HttpClient { get; init; }
    public required string OutputFilePattern { get; init; }

    public string? Content { get; set; }
    public string? OutputFilePath { get; set; }

    public DesignPipelineProgressInfo? Progress { get; init; }
}

public sealed class DesignPipelineProgressInfo
{
    public string Label { get; }

    private volatile string _stage = "Pending";
    private volatile string? _error;
    private readonly long _startTicks = Stopwatch.GetTimestamp();

    public DesignPipelineProgressInfo(string label) => Label = label;

    public string Stage => _stage;
    public string? Error => _error;
    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTicks);

    public void SetStage(string stage) => _stage = stage;
    public void SetError(string error) { _error = error; _stage = "Failed"; }
    public void SetDone() => _stage = "Done";
}

public sealed class DesignPipeline<TItem>
{
    private readonly List<Func<DesignPipelineDelegate<TItem>, DesignPipelineDelegate<TItem>>> _middleware = [];

    public DesignPipeline<TItem> Use(Func<DesignPipelineDelegate<TItem>, DesignPipelineDelegate<TItem>> middleware)
    {
        _middleware.Add(middleware);
        return this;
    }

    public DesignPipelineDelegate<TItem> Build()
    {
        DesignPipelineDelegate<TItem> terminal = _ => Task.CompletedTask;
        foreach (var mw in _middleware.AsEnumerable().Reverse())
            terminal = mw(terminal);
        return terminal;
    }
}

// -- Middleware Extensions (item-based overloads only) ------------------------

public static class DesignPipelineExtensions
{
    public static DesignPipeline<TItem> UseHttpDownload<TItem>(
        this DesignPipeline<TItem> pipeline,
        Func<TItem, string> urlBuilder)
    {
        return pipeline.Use(next => async ctx =>
        {
            ctx.Progress?.SetStage("Downloading");
            var url = urlBuilder(ctx.Item);
            ctx.Logger.LogDebug("Downloading {Key} from {Url}", ctx.Key, url);
            ctx.Content = await ctx.HttpClient.GetStringAsync(url);
            await next(ctx);
        });
    }

    public static DesignPipeline<TItem> UseContentTransform<TItem>(
        this DesignPipeline<TItem> pipeline,
        Func<TItem, string, string> transform)
    {
        return pipeline.Use(next => async ctx =>
        {
            ctx.Progress?.SetStage("Transforming");
            if (ctx.Content is not null)
                ctx.Content = transform(ctx.Item, ctx.Content);
            await next(ctx);
        });
    }

    public static DesignPipeline<TItem> UseSave<TItem>(this DesignPipeline<TItem> pipeline)
    {
        return pipeline.Use(next => async ctx =>
        {
            ctx.Progress?.SetStage("Saving");
            if (ctx.Content is not null)
            {
                var fileName = ctx.OutputFilePattern.Replace("{key}", ctx.Key);
                var filePath = Path.Combine(ctx.OutputDir, fileName);
                await File.WriteAllTextAsync(filePath, ctx.Content);
                ctx.OutputFilePath = filePath;
                ctx.Logger.LogDebug("Saved {Key} to {Path}", ctx.Key, filePath);
            }
            await next(ctx);
        });
    }
}

// -- Design Pipeline Runner --------------------------------------------------

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

    public async Task<int> RunAsync(string[] args)
    {
        var options = ParseArgs(args);

        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(MinLogLevel));
        var logger = loggerFactory.CreateLogger("DesignPipelineRunner");

        var items = await CollectAndFilter(logger, options.OutputDir, options.MissingOnly);
        var keys = items.Select(KeySelector).ToList();

        if (options.ListOnly)
            return ListItems(keys, options.MissingOnly);

        if (items.Count == 0)
        {
            Console.WriteLine("No items to process.");
            return 0;
        }

        return await ProcessAll(items, options.OutputDir, options.Parallel, logger);
    }

    private RunOptions ParseArgs(string[] args)
    {
        var parallel = DefaultParallelism;
        var outputDir = OutputDir;
        var listOnly = false;
        var missingOnly = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--parallel" when i + 1 < args.Length: parallel = int.Parse(args[++i]); break;
                case "--output" when i + 1 < args.Length: outputDir = args[++i]; break;
                case "--list": listOnly = true; break;
                case "--missing": missingOnly = true; break;
            }
        }

        return new RunOptions(parallel, outputDir, listOnly, missingOnly);
    }

    private async Task<IReadOnlyList<TItem>> CollectAndFilter(
        ILogger logger, string outputDir, bool missingOnly)
    {
        logger.LogInformation("Collecting items...");
        var allItems = await ItemCollector.CollectItemsAsync();
        logger.LogInformation("Discovered {Count} total items", allItems.Count);

        IReadOnlyList<TItem> items = ItemFilter is not null
            ? ItemFilter(allItems)
            : allItems;

        if (missingOnly)
            items = FilterToMissing(items, outputDir);

        return items;
    }

    private IReadOnlyList<TItem> FilterToMissing(IReadOnlyList<TItem> items, string outputDir)
    {
        var existing = DiscoverExistingKeys(outputDir);
        return items.Where(item => !existing.Contains(KeySelector(item))).ToList();
    }

    private HashSet<string> DiscoverExistingKeys(string outputDir)
    {
        var existing = new HashSet<string>();
        if (!Directory.Exists(outputDir))
            return existing;

        var parts = OutputFilePattern.Split("{key}");
        if (parts.Length != 2)
            return existing;

        foreach (var file in Directory.GetFiles(outputDir, "*.*"))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith(parts[0], StringComparison.Ordinal)
                && name.EndsWith(parts[1], StringComparison.Ordinal))
            {
                existing.Add(name[parts[0].Length..^parts[1].Length]);
            }
        }

        return existing;
    }

    private static int ListItems(IReadOnlyList<string> keys, bool missingOnly)
    {
        foreach (var key in keys)
            Console.WriteLine($"  {key}");
        var label = missingOnly ? "missing items" : "items";
        Console.WriteLine($"\nTotal: {keys.Count} {label}");
        return 0;
    }

    private async Task<int> ProcessAll(
        IReadOnlyList<TItem> items, string outputDir, int parallel, ILogger logger)
    {
        logger.LogInformation("Processing {Count} items with parallelism={Parallel}",
            items.Count, parallel);

        Directory.CreateDirectory(outputDir);

        using var httpClient = CreateHttpClient();
        var semaphore = new SemaphoreSlim(parallel);
        var succeeded = 0;
        var failed = 0;

        Console.WriteLine($"Processing {items.Count} items to {outputDir}...");

        await Task.WhenAll(items.Select(async item =>
        {
            var key = KeySelector(item);
            await semaphore.WaitAsync();
            try
            {
                var ctx = new DesignPipelineContext<TItem>
                {
                    Item = item,
                    Key = key,
                    OutputDir = outputDir,
                    Logger = logger,
                    HttpClient = httpClient,
                    OutputFilePattern = OutputFilePattern,
                };

                await Pipeline(ctx);
                Interlocked.Increment(ref succeeded);
                Console.WriteLine($"  {key}");
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                Console.Error.WriteLine($"  FAILED {key}: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }));

        Console.WriteLine($"Done: {succeeded} succeeded, {failed} failed.");
        return failed > 0 ? 1 : 0;
    }

    private HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        if (AuthTokenEnvVar is null)
            return httpClient;

        var token = Environment.GetEnvironmentVariable(AuthTokenEnvVar);
        if (!string.IsNullOrEmpty(token))
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return httpClient;
    }

    private sealed record RunOptions(int Parallel, string OutputDir, bool ListOnly, bool MissingOnly);
}

// -- Version Filters ---------------------------------------------------------

public static class VersionFilters
{
    public static IReadOnlyList<string> LatestPatchPerMinor(IReadOnlyList<string> versions)
    {
        return versions
            .Select(v =>
            {
                var parts = v.Split('.');
                if (parts.Length < 3) return null;
                if (!int.TryParse(parts[0], out var major)) return null;
                if (!int.TryParse(parts[1], out var minor)) return null;
                if (!int.TryParse(parts[2], out var patch)) return null;
                return new { Version = v, Major = major, Minor = minor, Patch = patch };
            })
            .Where(v => v is not null)
            .GroupBy(v => (v!.Major, v.Minor))
            .Select(g => g.OrderByDescending(v => v!.Patch).First()!)
            .OrderBy(v => v.Major).ThenBy(v => v.Minor)
            .Select(v => v.Version)
            .ToList();
    }
}
