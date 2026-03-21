using System.Text.Json;
using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.Wrapper.Versioning;

namespace FrenchExDev.Net.Packer.Design;

/// <summary>
/// Collects Packer versions from the HashiCorp releases API.
/// </summary>
public sealed class PackerVersionCollector : IVersionCollector
{
    private readonly HttpClient _httpClient;
    private const string ReleasesUrl = "https://releases.hashicorp.com/packer/index.json";

    public PackerVersionCollector(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    public async Task<IReadOnlyList<string>> CollectVersionsAsync(CancellationToken cancellationToken = default)
    {
        var json = await _httpClient.GetStringAsync(ReleasesUrl, cancellationToken);
        var doc = JsonDocument.Parse(json);

        var versions = new List<string>();

        if (doc.RootElement.TryGetProperty("versions", out var versionsObj))
        {
            foreach (var versionEntry in versionsObj.EnumerateObject())
            {
                var version = versionEntry.Name;

                // Skip pre-release versions (contain - like rc, beta, alpha)
                if (version.Contains('-'))
                    continue;

                // Validate it looks like a semver
                var parts = version.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
                    versions.Add(version);
            }
        }

        versions.Sort(GitHubReleasesVersionCollector.CompareVersionStrings);
        return versions;
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "FrenchExDev-Packer-Scrape");
        return client;
    }
}
