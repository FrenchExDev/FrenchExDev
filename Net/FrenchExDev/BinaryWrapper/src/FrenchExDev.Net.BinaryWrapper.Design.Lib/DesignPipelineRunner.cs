using System.Collections.Concurrent;
using System.Diagnostics;
using FrenchExDev.Net.BinaryWrapper.Design;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FrenchExDev.Net.BinaryWrapper.Design.Lib;

/// <summary>
/// Orchestrates parallel execution of a <see cref="VersionDelegate"/> pipeline
/// across multiple versions. Handles CLI argument parsing, version collection,
/// progress reporting, and crash-recovery cleanup.
/// </summary>
public sealed class DesignPipelineRunner
{
    public required IVersionCollector VersionCollector { get; init; }
    public required VersionDelegate Pipeline { get; init; }

    /// <summary>
    /// Pipeline used when --reparse is specified. Typically uses UseCachedHelp() + UseScraper().
    /// </summary>
    public VersionDelegate? ReparsePipeline { get; init; }

    public string? DefaultMinVersion { get; init; }
    public int DefaultParallelism { get; init; } = 4;
    public int DefaultScrapeParallelism { get; init; } = 4;
    public string RuntimeBinary { get; init; } = "podman";
    public required string OutputDir { get; init; }
    public LogLevel MinLogLevel { get; init; } = LogLevel.Information;

    /// <summary>
    /// Output file pattern with {version} placeholder (e.g., "podman-{version}.json").
    /// Required when using --missing. Must match the pattern used by UseScraper.
    /// </summary>
    public string? OutputFilePattern { get; init; }

    /// <summary>
    /// Injectable process runner. Defaults to <see cref="ProcessRunnerContainerRuntime.RunProcessAsync"/>.
    /// </summary>
    public Func<string[], Task<string>>? RunProcess { get; init; }

    private const string KnownMissingFileName = "_known_missing.txt";

    private static string KnownMissingPath(string outputDir)
        => Path.Combine(outputDir, KnownMissingFileName);

    private static HashSet<string> LoadKnownMissing(string outputDir)
    {
        var path = KnownMissingPath(outputDir);
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToHashSet();
    }

    private static void SaveKnownMissing(string outputDir, IEnumerable<string> versions)
    {
        Directory.CreateDirectory(outputDir);
        File.WriteAllLines(KnownMissingPath(outputDir),
            versions.Order().ToArray());
    }

    public async Task<int> RunAsync(string[] args)
    {
        // Parse CLI args (override defaults)
        var parallel = DefaultParallelism;
        var scrapeParallelism = DefaultScrapeParallelism;
        var outputDir = OutputDir;
        var minVersion = DefaultMinVersion;
        var runtimeBinary = RuntimeBinary;
        var listOnly = false;
        var missingOnly = false;
        var addKnownMissing = (string?)null;
        var removeKnownMissing = (string?)null;
        var listKnownMissing = false;
        var useDashboard = false;
        var reparse = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--parallel": parallel = int.Parse(args[++i]); break;
                case "--scrape-parallel": scrapeParallelism = int.Parse(args[++i]); break;
                case "--output": outputDir = args[++i]; break;
                case "--min-version": minVersion = args[++i]; break;
                case "--runtime": runtimeBinary = args[++i]; break;
                case "--list": listOnly = true; break;
                case "--missing": missingOnly = true; break;
                case "--add-known-missing": addKnownMissing = args[++i]; break;
                case "--remove-known-missing": removeKnownMissing = args[++i]; break;
                case "--list-known-missing": listKnownMissing = true; break;
                case "--dashboard": useDashboard = true; break;
                case "--reparse": reparse = true; break;
            }
        }

        // Handle --add-known-missing (early exit, no network needed)
        if (addKnownMissing is not null)
        {
            var current = LoadKnownMissing(outputDir);
            foreach (var v in addKnownMissing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                current.Add(v);
            SaveKnownMissing(outputDir, current);
            Console.WriteLine($"Known missing versions updated ({current.Count} total).");
            return 0;
        }

        // Handle --remove-known-missing (early exit, no network needed)
        if (removeKnownMissing is not null)
        {
            var current = LoadKnownMissing(outputDir);
            foreach (var v in removeKnownMissing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                current.Remove(v);
            SaveKnownMissing(outputDir, current);
            Console.WriteLine($"Known missing versions updated ({current.Count} total).");
            return 0;
        }

        // Handle --list-known-missing (early exit, no network needed)
        if (listKnownMissing)
        {
            var current = LoadKnownMissing(outputDir);
            foreach (var v in current.Order())
                Console.WriteLine(v);
            Console.WriteLine($"\nTotal: {current.Count} known missing versions");
            return 0;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(MinLogLevel));
        var logger = loggerFactory.CreateLogger("DesignPipelineRunner");

        var runProcess = RunProcess ?? ProcessRunnerContainerRuntime.RunProcessAsync;

        // Collect and filter versions
        Func<string, bool> filter = minVersion is not null
            ? v => GitHubReleasesVersionCollector.CompareVersionStrings(v, minVersion) >= 0
            : _ => true;

        var activePipeline = reparse ? ReparsePipeline! : Pipeline;

        logger.LogInformation("Output directory: {OutputDir}", outputDir);
        if (reparse)
            logger.LogInformation("Reparse mode: discovering cached versions from {HelpDir}",
                Path.Combine(outputDir, "help"));

        var allVersions = reparse
            ? DiscoverCachedVersions(outputDir)
            : await VersionCollector.CollectVersionsAsync();

        logger.LogInformation("Discovered {Count} total versions", allVersions.Count);
        if (minVersion is not null)
            logger.LogInformation("Filtering to versions >= {MinVersion}", minVersion);

        var versions = allVersions.Where(filter).ToList();

        logger.LogInformation("{FilteredCount} versions after filtering (from {TotalCount} total)",
            versions.Count, allVersions.Count);

        // Filter to missing versions only (not yet scraped to disk)
        if (missingOnly)
        {
            if (OutputFilePattern is null)
                throw new InvalidOperationException(
                    "--missing requires OutputFilePattern to be set on DesignPipelineRunner.");

            var existing = new HashSet<string>();
            if (Directory.Exists(outputDir))
            {
                var parts = OutputFilePattern.Split("{version}");
                foreach (var file in Directory.GetFiles(outputDir, "*.json"))
                {
                    var name = Path.GetFileName(file);
                    if (parts.Length == 2
                        && name.StartsWith(parts[0], StringComparison.Ordinal)
                        && name.EndsWith(parts[1], StringComparison.Ordinal))
                    {
                        existing.Add(name[parts[0].Length..^parts[1].Length]);
                    }
                }
            }

            var knownMissing = LoadKnownMissing(outputDir);
            versions = versions.Where(v => !existing.Contains(v) && !knownMissing.Contains(v)).ToList();
        }

        if (listOnly)
        {
            foreach (var v in versions)
                Console.WriteLine(v);
            var label = missingOnly ? "missing versions" : "versions";
            Console.WriteLine($"\nTotal: {versions.Count} {label}");
            return 0;
        }

        if (versions.Count == 0)
        {
            Console.WriteLine("No versions to process.");
            return 0;
        }

        logger.LogInformation("Processing {Count} versions with parallelism={Parallel}", versions.Count, parallel);

        Directory.CreateDirectory(outputDir);

        // Shared crash-recovery tracking
        var activeContainers = new ConcurrentBag<string>();
        var activeImages = new ConcurrentDictionary<string, byte>();

        // Progress tracking (only when dashboard is enabled)
        var progressInfos = useDashboard
            ? versions.ToDictionary(v => v, v => new VersionProgressInfo(v))
            : null;

        // Channel-based parallel execution
        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();
        var results = new ConcurrentBag<VersionScrapeResult>();
        var completed = 0;
        var total = versions.Count;

        foreach (var v in versions)
            await channel.Writer.WriteAsync(v);
        channel.Writer.Complete();

        var workers = new Task[Math.Min(parallel, versions.Count)];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = Task.Run(async () =>
            {
                await foreach (var version in channel.Reader.ReadAllAsync())
                {
                    var ctx = new VersionContext
                    {
                        Version = version,
                        RuntimeBinary = runtimeBinary,
                        Logger = logger,
                        OutputDir = outputDir,
                        RunProcess = runProcess,
                        ScrapeParallelism = scrapeParallelism,
                        ActiveContainers = activeContainers,
                        ActiveImages = activeImages,
                        Progress = progressInfos?.GetValueOrDefault(version),
                    };

                    VersionScrapeResult result;
                    try
                    {
                        await activePipeline(ctx);
                        ctx.Progress?.SetDone();
                        result = new VersionScrapeResult(version, true, ctx.Result, null);
                    }
                    catch (Exception ex)
                    {
                        ctx.Progress?.SetError(ex.Message);
                        result = new VersionScrapeResult(version, false, null, ex.Message);
                    }

                    results.Add(result);
                    var done = Interlocked.Increment(ref completed);

                    if (!useDashboard)
                    {
                        var status = result.Success ? "OK" : $"FAILED: {result.ErrorMessage}";
                        Console.WriteLine($"[{done}/{total}] {result.Version}: {status}");
                    }
                }
            });
        }

        try
        {
            if (useDashboard)
                await RunWithDashboardAsync(versions, progressInfos!, workers, total);
            else
                await Task.WhenAll(workers);

            var succeeded = results.Count(r => r.Success);
            var failed = results.Count(r => !r.Success);
            Console.WriteLine($"\nDone. {succeeded} succeeded, {failed} failed out of {results.Count} versions.");

            // Auto-save failed versions as known-missing when running in --missing mode
            if (missingOnly && failed > 0)
            {
                var failedVersions = results.Where(r => !r.Success).Select(r => r.Version);
                var current = LoadKnownMissing(outputDir);
                foreach (var v in failedVersions)
                    current.Add(v);
                SaveKnownMissing(outputDir, current);
                Console.WriteLine($"Added {failed} failed version(s) to known missing ({current.Count} total).");
            }

            return failed > 0 ? 1 : 0;
        }
        finally
        {
            // Cleanup stragglers (crash recovery)
            foreach (var id in activeContainers)
            {
                try { await runProcess([runtimeBinary, "rm", "-f", id]); }
                catch { }
            }

            foreach (var tag in activeImages.Keys)
            {
                try { await runProcess([runtimeBinary, "rmi", "-f", tag]); }
                catch { }
            }
        }
    }

    private static async Task RunWithDashboardAsync(
        List<string> versions,
        Dictionary<string, VersionProgressInfo> progressInfos,
        Task[] workers,
        int total)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Version")
            .AddColumn("Stage")
            .AddColumn("Commands")
            .AddColumn("Elapsed");

        var allDone = Task.WhenAll(workers);

        await AnsiConsole.Live(table)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (!allDone.IsCompleted)
                {
                    RebuildTable(table, versions, progressInfos, total);
                    ctx.Refresh();
                    await Task.Delay(250);
                }

                // Final refresh
                RebuildTable(table, versions, progressInfos, total);
                ctx.Refresh();
            });

        // Propagate any exceptions from workers
        await allDone;
    }

    private static void RebuildTable(
        Table table,
        List<string> versions,
        Dictionary<string, VersionProgressInfo> progressInfos,
        int total)
    {
        table.Rows.Clear();

        var doneCount = 0;
        var failedCount = 0;

        foreach (var v in versions)
        {
            var p = progressInfos[v];

            var stageMarkup = p.Stage switch
            {
                "Pending" => "[grey]Pending[/]",
                "Building" => "[yellow]Building[/]",
                "Starting" => "[yellow]Starting[/]",
                "Installing" => "[yellow]Installing[/]",
                "Loading" => "[blue]Loading[/]",
                "Scraping" => "[cyan]Scraping[/]",
                "Done" => "[green]Done[/]",
                "Failed" => "[red]Failed[/]",
                _ => p.Stage
            };

            var cmds = p.CommandsScraped > 0
                ? $"{p.CommandsScraped} cmds"
                : "-";

            var elapsed = p.Stage == "Pending"
                ? "-"
                : $"{p.Elapsed.TotalSeconds:F1}s";

            table.AddRow(p.Version, stageMarkup, cmds, elapsed);

            if (p.Stage == "Done") doneCount++;
            else if (p.Stage == "Failed") failedCount++;
        }

        table.Caption = new TableTitle(
            $"[grey]{doneCount + failedCount}/{total} complete" +
            (failedCount > 0 ? $" ({failedCount} failed)" : "") +
            "[/]");
    }

    private static IReadOnlyList<string> DiscoverCachedVersions(string outputDir)
    {
        var helpDir = Path.Combine(outputDir, "help");
        return Directory.Exists(helpDir)
            ? Directory.GetDirectories(helpDir)
                .Select(Path.GetFileName)
                .Where(v => v is not null)
                .Cast<string>()
                .ToList()
            : [];
    }
}
