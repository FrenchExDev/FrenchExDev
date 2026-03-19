using System.Text.Json;
using FrenchExDev.Net.BinaryWrapper.Design;

namespace FrenchExDev.Net.GitLab.Cli.Design;

/// <summary>
/// Collects release versions from the GitLab REST API v4.
/// glab publishes releases on gitlab.com (not GitHub), so
/// <see cref="GitHubReleasesVersionCollector"/> cannot be used.
/// </summary>
public sealed class GitLabReleasesVersionCollector : IVersionCollector
{
    private readonly string _projectPath;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly Func<string, string> _tagToVersion;

    /// <param name="projectPath">URL-encoded project path, e.g. <c>"gitlab-org%2Fcli"</c>.</param>
    /// <param name="baseUrl">GitLab instance base URL. Defaults to <c>https://gitlab.com</c>.</param>
    /// <param name="httpClient">Optional HTTP client (for testing or custom auth).</param>
    /// <param name="tagToVersion">Optional tag-to-version transformer. Defaults to stripping <c>v</c> prefix.</param>
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
        var versions = new List<string>();
        var url = (string?)$"{_baseUrl}/api/v4/projects/{_projectPath}/releases?per_page=100";

        while (url is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var releases = JsonSerializer.Deserialize<JsonElement>(json);

            if (releases.ValueKind == JsonValueKind.Array)
            {
                foreach (var release in releases.EnumerateArray())
                {
                    if (!release.TryGetProperty("tag_name", out var tagProp))
                        continue;
                    var tag = tagProp.GetString();
                    if (tag is null)
                        continue;

                    // Skip upcoming releases (GitLab's equivalent of pre-release)
                    if (release.TryGetProperty("upcoming_release", out var upcoming) && upcoming.GetBoolean())
                        continue;

                    var version = _tagToVersion(tag);
                    if (!string.IsNullOrEmpty(version))
                        versions.Add(version);
                }
            }

            url = ParseNextLink(response.Headers);
        }

        versions.Sort(GitHubReleasesVersionCollector.CompareVersionStrings);
        return versions;
    }

    private static string? ParseNextLink(System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Link", out var linkValues))
            return null;

        foreach (var link in linkValues)
        {
            foreach (var part in link.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.EndsWith("rel=\"next\"", StringComparison.Ordinal))
                {
                    var start = trimmed.IndexOf('<');
                    var end = trimmed.IndexOf('>');
                    if (start >= 0 && end > start)
                        return trimmed[(start + 1)..end];
                }
            }
        }
        return null;
    }

    private static string DefaultTagToVersion(string tag) =>
        tag.StartsWith('v') ? tag[1..] : tag;

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "FrenchExDev-BinaryWrapper");

        // Use GITLAB_TOKEN for higher rate limits if available
        var token = Environment.GetEnvironmentVariable("GITLAB_TOKEN");
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);

        return client;
    }
}
