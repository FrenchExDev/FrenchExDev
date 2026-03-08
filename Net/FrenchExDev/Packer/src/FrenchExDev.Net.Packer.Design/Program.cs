using System.Collections.Concurrent;
using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.Packer.Design;

var parallel = 4;
var outputDir = Path.GetFullPath(Path.Combine("..", "FrenchExDev.Net.Packer", "scrape"));
string? minVersion = null;
var runtimeBinary = "podman";
var listOnly = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--parallel": parallel = int.Parse(args[++i]); break;
        case "--output": outputDir = args[++i]; break;
        case "--min-version": minVersion = args[++i]; break;
        case "--runtime": runtimeBinary = args[++i]; break;
        case "--list": listOnly = true; break;
    }
}

var collector = new PackerVersionCollector();

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
var containerIds = new ConcurrentBag<string>();

var scraper = new MultiVersionScraper(
    versionCollector: collector,
    pipelineFactory: version =>
    {
        string? containerId = null;

        return new ScrapePipeline()
            .Binary("packer")
            .UseParser<PackerHelpParser>()
            .HelpFlag("-h")
            .WithRunHelp(async helpArgs =>
            {
                if (containerId is null)
                {
                    containerId = (await runProcess([runtimeBinary, "run", "-d", "alpine:3.19", "sleep", "infinity"])).Trim();
                    containerIds.Add(containerId);

                    await runProcess([runtimeBinary, "exec", containerId, "sh", "-c",
                        "apk add --no-cache curl unzip && " +
                        $"curl -fsSL https://releases.hashicorp.com/packer/{version}/packer_{version}_linux_amd64.zip -o /tmp/p.zip && " +
                        "unzip /tmp/p.zip -d /usr/local/bin && rm /tmp/p.zip"]);
                }

                var execArgs = new List<string> { runtimeBinary, "exec", containerId };
                execArgs.AddRange(helpArgs);
                return await runProcess(execArgs.ToArray());
            })
            .OutputTo(Path.Combine(outputDir, $"packer-{version}.json"));
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
