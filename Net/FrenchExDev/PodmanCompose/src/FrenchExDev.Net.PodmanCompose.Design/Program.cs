using System.Collections.Concurrent;
using System.Threading.Channels;
using FrenchExDev.Net.BinaryWrapper.Design;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("PodmanComposeScraper");

var parallel = 4;
var projectDir = AppContext.BaseDirectory;
// Navigate from bin/Debug/net10.0 → src/FrenchExDev.Net.PodmanCompose.Design → src/FrenchExDev.Net.PodmanCompose/scrape
var outputDir = Path.GetFullPath(Path.Combine(projectDir, "..", "..", "..", "..", "FrenchExDev.Net.PodmanCompose", "scrape"));
string? minVersion = "1.0.0";
var runtimeBinary = "podman";
var listOnly = false;
var buildImagesOnly = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--parallel": parallel = int.Parse(args[++i]); break;
        case "--output": outputDir = args[++i]; break;
        case "--min-version": minVersion = args[++i]; break;
        case "--runtime": runtimeBinary = args[++i]; break;
        case "--list": listOnly = true; break;
        case "--build-images": buildImagesOnly = true; break;
    }
}

var collector = new GitHubReleasesVersionCollector("containers", "podman-compose");

Func<string, bool> filter = minVersion is not null
    ? v => GitHubReleasesVersionCollector.CompareVersionStrings(v, minVersion) >= 0
    : _ => true;

if (listOnly)
{
    var allVersions = await collector.CollectVersionsAsync();
    var filtered = allVersions.Where(filter).ToList();
    foreach (var v in filtered)
        Console.WriteLine(v);
    Console.WriteLine($"\nTotal: {filtered.Count} versions");
    return 0;
}

var runProcess = ProcessRunnerContainerRuntime.RunProcessAsync;

var allVersionsList = await collector.CollectVersionsAsync();
var versions = allVersionsList.Where(filter).ToList();

logger.LogInformation("Found {Count} versions to process", versions.Count);

var imageTag = (string version) => $"podman-compose-scrape:{version}";

async Task<bool> ImageExists(string version)
{
    try
    {
        await runProcess([runtimeBinary, "image", "inspect", imageTag(version)]);
        return true;
    }
    catch
    {
        return false;
    }
}

// --- Build-images-only mode (Phase 1 only) ---

if (buildImagesOnly)
{
    var buildSemaphore = new SemaphoreSlim(parallel);

    async Task BuildImageOnly(string version)
    {
        await buildSemaphore.WaitAsync();
        try
        {
            await BuildImage(version);
        }
        finally
        {
            buildSemaphore.Release();
        }
    }

    await Task.WhenAll(versions.Select(BuildImageOnly));

    var builtCount = 0;
    foreach (var v in versions)
    {
        if (await ImageExists(v)) builtCount++;
    }
    Console.WriteLine($"Built {builtCount}/{versions.Count} images.");
    return builtCount == versions.Count ? 0 : 1;
}

// --- Pipelined mode: build image → immediately scrape → cleanup ---
//
// Phase 1 (producers): build images with bounded parallelism, write completed
// versions to a Channel as soon as each image is ready.
// Phase 2 (consumers): read from the channel, scrape, then immediately delete
// the image to free disk space.

logger.LogInformation("Pipelined build+scrape: {Count} versions, parallelism={Parallel}", versions.Count, parallel);

Directory.CreateDirectory(outputDir);

var readyChannel = Channel.CreateUnbounded<string>();
var scrapeResults = new ConcurrentBag<VersionScrapeResult>();
var completed = 0;
var total = versions.Count;

// Track images/containers that may need cleanup on crash
var activeImages = new ConcurrentDictionary<string, byte>();
var activeContainers = new ConcurrentBag<string>();

// -- Phase 1: Build producers --

var buildSem = new SemaphoreSlim(parallel);

async Task BuildAndPublish(string version)
{
    await buildSem.WaitAsync();
    try
    {
        var success = await BuildImage(version);
        if (success)
        {
            activeImages.TryAdd(imageTag(version), 0);
            await readyChannel.Writer.WriteAsync(version);
        }
    }
    finally
    {
        buildSem.Release();
    }
}

var buildTask = Task.Run(async () =>
{
    var tasks = versions.Select(BuildAndPublish).ToArray();
    await Task.WhenAll(tasks);
    readyChannel.Writer.Complete();
    logger.LogInformation("All image builds finished, channel closed");
});

// -- Phase 2: Scrape consumers (with eager cleanup) --

var scrapeWorkers = new Task[Math.Min(parallel, versions.Count)];
for (var i = 0; i < scrapeWorkers.Length; i++)
{
    scrapeWorkers[i] = Task.Run(async () =>
    {
        await foreach (var version in readyChannel.Reader.ReadAllAsync())
        {
            VersionScrapeResult result;
            try
            {
                var tree = await ScrapeVersion(version);
                result = new VersionScrapeResult(version, true, tree, null);
            }
            catch (Exception ex)
            {
                result = new VersionScrapeResult(version, false, null, ex.Message);
            }

            // Eager cleanup: delete image immediately after scrape
            try
            {
                await runProcess([runtimeBinary, "rmi", "-f", imageTag(version)]);
                activeImages.TryRemove(imageTag(version), out _);
                logger.LogInformation("[{Version}] Image cleaned up", version);
            }
            catch { /* best-effort */ }

            scrapeResults.Add(result);
            var done = Interlocked.Increment(ref completed);
            var status = result.Success ? "OK" : $"FAILED: {result.ErrorMessage}";
            Console.WriteLine($"[{done}/{total}] {result.Version}: {status}");
        }
    });
}

try
{
    await buildTask;
    await Task.WhenAll(scrapeWorkers);

    var succeeded = scrapeResults.Count(r => r.Success);
    var failed = scrapeResults.Count(r => !r.Success);
    Console.WriteLine($"\nDone. {succeeded} succeeded, {failed} failed out of {scrapeResults.Count} versions.");

    return failed > 0 ? 1 : 0;
}
finally
{
    // Cleanup stragglers (crash recovery)
    foreach (var id in activeContainers)
    {
        try { await runProcess([runtimeBinary, "rm", "-f", id]); }
        catch { /* best-effort */ }
    }

    foreach (var tag in activeImages.Keys)
    {
        try { await runProcess([runtimeBinary, "rmi", "-f", tag]); }
        catch { /* best-effort */ }
    }
}

// --- Shared helpers ---

async Task<bool> BuildImage(string version)
{
    if (await ImageExists(version))
    {
        logger.LogInformation("[{Version}] Image already exists, skipping build", version);
        return true;
    }

    logger.LogInformation("[{Version}] Building image...", version);

    var containerId = (await runProcess([runtimeBinary, "run", "-d", "alpine:3.19", "sleep", "infinity"])).Trim();

    try
    {
        logger.LogInformation("[{Version}] Installing podman-compose in container {Id}...", version, containerId[..12]);
        await runProcess([runtimeBinary, "exec", containerId, "sh", "-c",
            "apk add --no-cache python3 py3-pip py3-yaml py3-dotenv > /dev/null 2>&1 && " +
            $"pip install --break-system-packages podman-compose=={version} > /dev/null 2>&1"]);

        await runProcess([runtimeBinary, "commit", containerId, imageTag(version)]);
        logger.LogInformation("[{Version}] Image built: {Tag}", version, imageTag(version));
        return true;
    }
    catch (Exception ex)
    {
        logger.LogError("[{Version}] Image build failed: {Error}", version, ex.Message);
        return false;
    }
    finally
    {
        try { await runProcess([runtimeBinary, "rm", "-f", containerId]); }
        catch { /* best-effort */ }
    }
}

async Task<CommandTree?> ScrapeVersion(string version)
{
    string? containerId = null;

    try
    {
        var pipeline = new ScrapePipeline()
            .Binary("podman-compose")
            .UseParser("argparse")
            .WithRunHelp(async helpArgs =>
            {
                if (containerId is null)
                {
                    logger.LogInformation("[{Version}] Starting scrape container...", version);
                    containerId = (await runProcess([runtimeBinary, "run", "-d", imageTag(version), "sleep", "infinity"])).Trim();
                    activeContainers.Add(containerId);
                    logger.LogInformation("[{Version}] Scrape container ready: {Id}", version, containerId[..12]);
                }

                var execArgs = new List<string> { runtimeBinary, "exec", containerId };
                execArgs.AddRange(helpArgs);
                return await runProcess(execArgs.ToArray());
            })
            .OutputTo(Path.Combine(outputDir, $"podman-compose-{version}.json"));

        return await pipeline.ExecuteAsync();
    }
    finally
    {
        if (containerId is not null)
        {
            try { await runProcess([runtimeBinary, "rm", "-f", containerId]); }
            catch { /* best-effort */ }
        }
    }
}
