using System.Collections.Concurrent;
using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.Vagrant.Design;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger("VagrantScraper");

var parallel = 4;
var outputDir = Path.GetFullPath(Path.Combine("Vagrant", "src", "FrenchExDev.Net.Vagrant", "scrape"));
string? minVersion = null;
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

var collector = new VagrantVersionCollector();

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
var imageTag = (string version) => $"vagrant-scrape:{version}";

async Task<bool> ImageExists(string version)
{
    try
    {
        var output = await runProcess([runtimeBinary, "image", "inspect", imageTag(version)]);
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

        // Create a temporary container, install vagrant, commit as image
        var containerId = (await runProcess([runtimeBinary, "run", "-d", "debian:bookworm", "sleep", "infinity"])).Trim();

        try
        {
            logger.LogInformation("[{Version}] Installing vagrant in container {Id}...", version, containerId[..12]);
            await runProcess([runtimeBinary, "exec", containerId, "bash", "-c",
                "apt-get update -qq > /dev/null 2>&1 && " +
                "apt-get install -y -qq curl > /dev/null 2>&1 && " +
                $"curl -fsSL https://releases.hashicorp.com/vagrant/{version}/vagrant_{version}-1_amd64.deb -o /tmp/vagrant.deb && " +
                "dpkg -i /tmp/vagrant.deb > /dev/null 2>&1 && rm /tmp/vagrant.deb && " +
                "sed -i 's/@_wsl = true/@_wsl = false/' /opt/vagrant/embedded/gems/gems/vagrant-*/lib/vagrant/util/platform.rb && " +
                "apt-get clean > /dev/null 2>&1"]);

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

        var vagrantParser = new VagrantHelpParser();
        var loggingParser = new LoggingHelpParser(vagrantParser, logger, version);

        return new ScrapePipeline()
            .Binary("vagrant")
            .UseParser(loggingParser)
            .HelpFlag("-h")
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
            .OutputTo(Path.Combine(outputDir, $"vagrant-{version}.json"));
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
}
