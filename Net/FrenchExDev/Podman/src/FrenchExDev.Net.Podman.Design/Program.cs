using System.Collections.Concurrent;
using FrenchExDev.Net.BinaryWrapper.Design;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("PodmanScraper");

var parallel = 4;
var outputDir = Path.GetFullPath(Path.Combine("..", "FrenchExDev.Net.Podman", "scrape"));
string? minVersion = "4.1.0";
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

var collector = new GitHubReleasesVersionCollector("containers", "podman");

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

// --- Phase 1: Build pre-built images for each version ---

var allVersionsList = await collector.CollectVersionsAsync();
var versions = allVersionsList.Where(filter).ToList();

logger.LogInformation("Found {Count} versions to process", versions.Count);

var imageSemaphore = new SemaphoreSlim(parallel);
var imageTag = (string version) => $"podman-scrape:{version}";

// Asset naming changed at v4.4.0: before that "podman-remote-static.tar.gz",
// from v4.4.0 onwards "podman-remote-static-linux_amd64.tar.gz".
// v4.0.0 has no static binary at all (min-version defaults to 4.1.0).
static string AssetName(string version)
{
    return GitHubReleasesVersionCollector.CompareVersionStrings(version, "4.4.0") >= 0
        ? "podman-remote-static-linux_amd64.tar.gz"
        : "podman-remote-static.tar.gz";
}

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

async Task BuildImage(string version)
{
    await imageSemaphore.WaitAsync();
    try
    {
        if (await ImageExists(version))
        {
            logger.LogInformation("[{Version}] Image already exists, skipping build", version);
            return;
        }

        logger.LogInformation("[{Version}] Building image...", version);

        // Create a temporary container, install podman-remote, commit as image
        var containerId = (await runProcess([runtimeBinary, "run", "-d", "alpine:3.19", "sleep", "infinity"])).Trim();

        try
        {
            var asset = AssetName(version);
            logger.LogInformation("[{Version}] Installing podman-remote in container {Id}...", version, containerId[..12]);
            await runProcess([runtimeBinary, "exec", containerId, "sh", "-c",
                "apk add --no-cache curl tar > /dev/null 2>&1 && " +
                $"curl -fsSL https://github.com/containers/podman/releases/download/v{version}/{asset} -o /tmp/podman.tar.gz && " +
                "tar xzf /tmp/podman.tar.gz --no-same-owner -C /tmp && " +
                "find /tmp -name 'podman*' -type f | head -1 | xargs -I{} mv {} /usr/local/bin/podman && " +
                "chmod +x /usr/local/bin/podman && " +
                "rm -rf /tmp/podman.tar.gz /tmp/bin"]);

            // Commit the container as a reusable image
            await runProcess([runtimeBinary, "commit", containerId, imageTag(version)]);
            logger.LogInformation("[{Version}] Image built: {Tag}", version, imageTag(version));
        }
        finally
        {
            try { await runProcess([runtimeBinary, "rm", "-f", containerId]); }
            catch { /* best-effort */ }
        }
    }
    finally
    {
        imageSemaphore.Release();
    }
}

logger.LogInformation("Phase 1: Building {Count} images with parallelism={Parallel}", versions.Count, parallel);

var buildTasks = versions.Select(BuildImage).ToArray();
await Task.WhenAll(buildTasks);

var builtCount = 0;
foreach (var v in versions)
{
    if (await ImageExists(v)) builtCount++;
}
logger.LogInformation("Phase 1 complete: {Built}/{Total} images ready", builtCount, versions.Count);

if (buildImagesOnly)
{
    Console.WriteLine($"Built {builtCount}/{versions.Count} images.");
    return builtCount == versions.Count ? 0 : 1;
}

// --- Phase 2: Scrape from pre-built images ---

logger.LogInformation("Phase 2: Scraping from pre-built images with parallelism={Parallel}", parallel);

var containerIds = new ConcurrentBag<string>();

var scraper = new MultiVersionScraper(
    versionCollector: collector,
    pipelineFactory: version =>
    {
        string? containerId = null;

        return new ScrapePipeline()
            .Binary("podman")
            .UseParser("cobra")
            .WithRunHelp(async helpArgs =>
            {
                if (containerId is null)
                {
                    logger.LogInformation("[{Version}] Starting container from pre-built image...", version);
                    containerId = (await runProcess([runtimeBinary, "run", "-d", imageTag(version), "sleep", "infinity"])).Trim();
                    containerIds.Add(containerId);
                    logger.LogInformation("[{Version}] Container ready: {Id}", version, containerId[..12]);
                }

                var cmd = string.Join(" ", helpArgs);
                logger.LogDebug("[{Version}] Running: {Cmd}", version, cmd);
                var execArgs = new List<string> { runtimeBinary, "exec", containerId };
                execArgs.AddRange(helpArgs);
                var result = await runProcess(execArgs.ToArray());
                logger.LogDebug("[{Version}] Got {Len} chars from: {Cmd}", version, result.Length, cmd);
                return result;
            })
            .OutputTo(Path.Combine(outputDir, $"podman-{version}.json"));
    },
    maxParallelism: parallel);

scraper.Progress += (result, done, total) =>
{
    var status = result.Success ? "OK" : $"FAILED: {result.ErrorMessage}";
    Console.WriteLine($"[{done}/{total}] {result.Version}: {status}");
};

Directory.CreateDirectory(outputDir);

try
{
    var results = await scraper.ScrapeAsync(filter);

    var succeeded = results.Count(r => r.Success);
    var failed = results.Count(r => !r.Success);
    Console.WriteLine($"\nDone. {succeeded} succeeded, {failed} failed out of {results.Count} versions.");

    return failed > 0 ? 1 : 0;
}
finally
{
    // Cleanup all containers we created
    foreach (var id in containerIds)
    {
        try { await runProcess([runtimeBinary, "rm", "-f", id]); }
        catch { /* best-effort */ }
    }

    // Cleanup all podman-scrape images
    logger.LogInformation("Cleaning up {Count} podman-scrape images...", versions.Count);
    foreach (var v in versions)
    {
        try { await runProcess([runtimeBinary, "rmi", "-f", imageTag(v)]); }
        catch { /* best-effort */ }
    }
}
