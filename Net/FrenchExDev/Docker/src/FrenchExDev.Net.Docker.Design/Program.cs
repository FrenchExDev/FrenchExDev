using System.Collections.Concurrent;
using System.Threading.Channels;
using FrenchExDev.Net.BinaryWrapper.Design;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("DockerScraper");

var parallel = 4;
var projectDir = AppContext.BaseDirectory;
// Navigate from bin/Debug/net10.0 → src/FrenchExDev.Net.Docker.Design → src/FrenchExDev.Net.Docker/scrape
var outputDir = Path.GetFullPath(Path.Combine(projectDir, "..", "..", "..", "..", "FrenchExDev.Net.Docker", "scrape"));
string? minVersion = "23.0.0";
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

var collector = new GitHubTagsVersionCollector("docker", "cli");

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

var imageTag = (string version) => $"docker-scrape:{version}";

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

// --- Pipelined mode: build image → immediately scrape ---
//
// Phase 1 (producers): build images with bounded parallelism, write completed
// versions to a Channel as soon as each image is ready.
// Phase 2 (consumers): read from the channel and scrape immediately, no waiting
// for all images to finish.

logger.LogInformation("Pipelined build+scrape: {Count} versions, parallelism={Parallel}", versions.Count, parallel);

Directory.CreateDirectory(outputDir);

var readyChannel = Channel.CreateUnbounded<string>();
var containerIds = new ConcurrentBag<string>();
var scrapeResults = new ConcurrentBag<VersionScrapeResult>();
var completed = 0;
var total = versions.Count;

// -- Phase 1: Build producers --

var buildSem = new SemaphoreSlim(parallel);

async Task BuildAndPublish(string version)
{
    await buildSem.WaitAsync();
    try
    {
        var success = await BuildImage(version);
        if (success)
            await readyChannel.Writer.WriteAsync(version);
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

// -- Phase 2: Scrape consumers --

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
    // Cleanup all containers
    foreach (var id in containerIds)
    {
        try { await runProcess([runtimeBinary, "rm", "-f", id]); }
        catch { /* best-effort */ }
    }

    // Cleanup all docker-scrape images
    logger.LogInformation("Cleaning up {Count} docker-scrape images...", versions.Count);
    foreach (var v in versions)
    {
        try { await runProcess([runtimeBinary, "rmi", "-f", imageTag(v)]); }
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
        logger.LogInformation("[{Version}] Installing docker CLI in container {Id}...", version, containerId[..12]);
        await runProcess([runtimeBinary, "exec", containerId, "sh", "-c",
            "apk add --no-cache curl tar > /dev/null 2>&1 && " +
            $"curl -fsSL https://download.docker.com/linux/static/stable/x86_64/docker-{version}.tgz -o /tmp/docker.tgz && " +
            "tar xzf /tmp/docker.tgz --no-same-owner -C /tmp docker/docker && " +
            "mv /tmp/docker/docker /usr/local/bin/docker && " +
            "chmod +x /usr/local/bin/docker && " +
            "rm -rf /tmp/docker.tgz /tmp/docker"]);

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
            .Binary("docker")
            .UseParser("cobra")
            .WithRunHelp(async helpArgs =>
            {
                if (containerId is null)
                {
                    logger.LogInformation("[{Version}] Starting scrape container...", version);
                    containerId = (await runProcess([runtimeBinary, "run", "-d", imageTag(version), "sleep", "infinity"])).Trim();
                    containerIds.Add(containerId);
                    logger.LogInformation("[{Version}] Scrape container ready: {Id}", version, containerId[..12]);
                }

                var execArgs = new List<string> { runtimeBinary, "exec", containerId };
                execArgs.AddRange(helpArgs);
                return await runProcess(execArgs.ToArray());
            })
            .OutputTo(Path.Combine(outputDir, $"docker-{version}.json"));

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
