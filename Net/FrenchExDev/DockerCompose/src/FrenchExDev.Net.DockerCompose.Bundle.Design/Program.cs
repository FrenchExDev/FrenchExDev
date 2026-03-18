using FrenchExDev.Net.BinaryWrapper.Design;

var outputDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.DockerCompose.Bundle", "schemas"));

Directory.CreateDirectory(outputDir);

var collector = new GitHubReleasesVersionCollector("compose-spec", "compose-go");
var allVersions = await collector.CollectVersionsAsync();

// Filter: latest patch per major.minor, skip pre-releases (already filtered by collector)
var latestPerMinor = allVersions
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
    .ToList();

var missing = args.Contains("--missing");

if (args.Contains("--list"))
{
    var listVersions = missing
        ? latestPerMinor.Where(v => !File.Exists(Path.Combine(outputDir, $"compose-spec-v{v.Version}.json"))).ToList()
        : latestPerMinor;

    Console.WriteLine($"Found {listVersions.Count} versions{(missing ? " (missing)" : "")} (latest patch per minor):");
    foreach (var v in listVersions)
        Console.WriteLine($"  v{v.Version}");
    return 0;
}

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FrenchExDev-BundleDesign/1.0");

var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (!string.IsNullOrEmpty(githubToken))
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", githubToken);

var semaphore = new SemaphoreSlim(6);
var downloaded = 0;
var skipped = 0;
var failed = 0;

var tasks = latestPerMinor.Select(async v =>
{
    var outputFile = Path.Combine(outputDir, $"compose-spec-v{v.Version}.json");

    if (missing && File.Exists(outputFile))
    {
        Interlocked.Increment(ref skipped);
        return;
    }

    await semaphore.WaitAsync();
    try
    {
        var url = $"https://raw.githubusercontent.com/compose-spec/compose-go/v{v.Version}/schema/compose-spec.json";
        var json = await httpClient.GetStringAsync(url);
        await File.WriteAllTextAsync(outputFile, json);
        Interlocked.Increment(ref downloaded);
        Console.WriteLine($"  Downloaded v{v.Version}");
    }
    catch (Exception ex)
    {
        Interlocked.Increment(ref failed);
        Console.Error.WriteLine($"  Failed v{v.Version}: {ex.Message}");
    }
    finally
    {
        semaphore.Release();
    }
});

Console.WriteLine($"Downloading {latestPerMinor.Count} schemas to {outputDir}...");
await Task.WhenAll(tasks);
Console.WriteLine($"Done: {downloaded} downloaded, {skipped} skipped, {failed} failed.");

return failed > 0 ? 1 : 0;
