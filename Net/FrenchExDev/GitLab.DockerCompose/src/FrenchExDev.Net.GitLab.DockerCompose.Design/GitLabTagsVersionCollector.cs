using System.Net.Http.Headers;
using System.Text.Json;
using FrenchExDev.Net.Wrapper.Versioning;

namespace FrenchExDev.Net.GitLab.DockerCompose.Design;

/// <summary>
/// Collects versions from GitLab Repository Tags API.
/// Uses <c>/api/v4/projects/:id/repository/tags</c> (not Releases API).
/// </summary>
public sealed class GitLabTagsVersionCollector : IVersionCollector
{
    private readonly string _projectPath;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly Func<string, string?> _tagToVersion;

    public GitLabTagsVersionCollector(
        string projectPath,
        string? baseUrl = null,
        HttpClient? httpClient = null,
        Func<string, string?>? tagToVersion = null)
    {
        _projectPath = projectPath;
        _baseUrl = baseUrl?.TrimEnd('/') ?? "https://gitlab.com";
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _tagToVersion = tagToVersion ?? DefaultTagToVersion;
    }

    public async Task<IReadOnlyList<string>> CollectVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        var versions = new List<string>();
        var url = (string?)$"{_baseUrl}/api/v4/projects/{_projectPath}/repository/tags?per_page=100&order_by=version&sort=desc";

        while (url is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var items = JsonSerializer.Deserialize<JsonElement>(json);

            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (!item.TryGetProperty("name", out var nameProp))
                        continue;
                    var tag = nameProp.GetString();
                    if (tag is null) continue;

                    var version = _tagToVersion(tag);
                    if (!string.IsNullOrEmpty(version))
                        versions.Add(version);
                }
            }

            url = ParseNextLink(response.Headers);
        }

        // Sort semantically (ascending)
        versions.Sort((a, b) => CompareVersions(a, b));
        return versions;
    }

    public Task<IReadOnlyList<string>> CollectItemsAsync(CancellationToken cancellationToken = default)
        => CollectVersionsAsync(cancellationToken);

    private static string? DefaultTagToVersion(string tag) =>
        tag.Contains('+') ? null : tag;

    private static string? ParseNextLink(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Link", out var values))
            return null;

        foreach (var value in values)
        {
            foreach (var part in value.Split(','))
            {
                var trimmed = part.Trim();
                if (!trimmed.EndsWith("rel=\"next\""))
                    continue;

                var start = trimmed.IndexOf('<');
                var end = trimmed.IndexOf('>');
                if (start >= 0 && end > start)
                    return trimmed.Substring(start + 1, end - start - 1);
            }
        }

        return null;
    }

    private static int CompareVersions(string a, string b)
    {
        var partsA = a.Split('.');
        var partsB = b.Split('.');
        for (var i = 0; i < Math.Max(partsA.Length, partsB.Length); i++)
        {
            var va = i < partsA.Length && int.TryParse(partsA[i], out var ia) ? ia : 0;
            var vb = i < partsB.Length && int.TryParse(partsB[i], out var ib) ? ib : 0;
            if (va != vb) return va.CompareTo(vb);
        }
        return 0;
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "FrenchExDev-DesignPipeline");
        var token = Environment.GetEnvironmentVariable("GITLAB_TOKEN");
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
        return client;
    }
}
