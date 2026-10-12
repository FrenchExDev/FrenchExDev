using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Tests;

public class GitLabReleasesVersionCollectorTests
{
    [Fact]
    public async Task CollectVersions_ParsesTagNames_StripsVPrefix()
    {
        var json = """
        [
            {"tag_name": "v1.2.0"},
            {"tag_name": "v1.1.0"},
            {"tag_name": "v1.0.0"}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake.gitlab.com") };
        var collector = new GitLabReleasesVersionCollector("test%2Fproject",
            baseUrl: "https://fake.gitlab.com", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0", "1.1.0", "1.2.0"]);
    }

    [Fact]
    public async Task CollectVersions_SkipsUpcomingReleases()
    {
        var json = """
        [
            {"tag_name": "v2.0.0", "upcoming_release": true},
            {"tag_name": "v1.0.0", "upcoming_release": false},
            {"tag_name": "v0.9.0"}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake.gitlab.com") };
        var collector = new GitLabReleasesVersionCollector("test%2Fproject",
            baseUrl: "https://fake.gitlab.com", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["0.9.0", "1.0.0"]);
        versions.ShouldNotContain("2.0.0");
    }

    [Fact]
    public async Task CollectVersions_SkipsNullTags()
    {
        var json = """
        [
            {"tag_name": "v1.0.0"},
            {"tag_name": null},
            {"other_field": "no tag_name"}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake.gitlab.com") };
        var collector = new GitLabReleasesVersionCollector("test%2Fproject",
            baseUrl: "https://fake.gitlab.com", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_EmptyArray_ReturnsEmpty()
    {
        var handler = new FakeHttpHandler("[]");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake.gitlab.com") };
        var collector = new GitLabReleasesVersionCollector("test%2Fproject",
            baseUrl: "https://fake.gitlab.com", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBeEmpty();
    }

    [Fact]
    public async Task CollectVersions_SortsVersionsSemantically()
    {
        var json = """
        [
            {"tag_name": "v1.10.0"},
            {"tag_name": "v1.2.0"},
            {"tag_name": "v1.9.0"},
            {"tag_name": "v2.0.0"}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake.gitlab.com") };
        var collector = new GitLabReleasesVersionCollector("test%2Fproject",
            baseUrl: "https://fake.gitlab.com", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.2.0", "1.9.0", "1.10.0", "2.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_CustomTagToVersion()
    {
        var json = """[{"tag_name": "release-1.0.0"}]""";
        var handler = new FakeHttpHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake.gitlab.com") };
        var collector = new GitLabReleasesVersionCollector("test%2Fproject",
            baseUrl: "https://fake.gitlab.com", httpClient: client,
            tagToVersion: tag => tag.Replace("release-", ""));

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_TagWithoutVPrefix_PassedThrough()
    {
        var json = """[{"tag_name": "1.5.0"}]""";
        var handler = new FakeHttpHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake.gitlab.com") };
        var collector = new GitLabReleasesVersionCollector("test%2Fproject",
            baseUrl: "https://fake.gitlab.com", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.5.0"]);
    }

    [Fact]
    public async Task CollectVersions_Pagination_FollowsLinkHeader()
    {
        var page1Json = """[{"tag_name": "v1.0.0"}]""";
        var page2Json = """[{"tag_name": "v2.0.0"}]""";

        var handler = new SequencingHttpHandler([
            (page1Json, "<https://fake.gitlab.com/api/v4/projects/p/releases?page=2>; rel=\"next\""),
            (page2Json, null)
        ]);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake.gitlab.com") };
        var collector = new GitLabReleasesVersionCollector("p",
            baseUrl: "https://fake.gitlab.com", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0", "2.0.0"]);
    }

    [Fact]
    public void Constructor_DefaultBaseUrl_IsGitLabCom()
    {
        var collector = new GitLabReleasesVersionCollector("test%2Fproject");
        collector.ShouldNotBeNull();
    }

    [Fact]
    public async Task CollectVersions_EmptyTagToVersion_SkipsEntry()
    {
        var json = """[{"tag_name": "v1.0.0"}, {"tag_name": "skip-me"}]""";
        var handler = new FakeHttpHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake.gitlab.com") };
        var collector = new GitLabReleasesVersionCollector("test%2Fproject",
            baseUrl: "https://fake.gitlab.com", httpClient: client,
            tagToVersion: tag => tag.StartsWith('v') ? tag[1..] : "");

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0"]);
    }
}

/// <summary>
/// HTTP handler that returns responses in sequence, supporting pagination testing.
/// </summary>
internal sealed class SequencingHttpHandler : HttpMessageHandler
{
    private readonly (string Json, string? LinkHeader)[] _responses;
    private int _callIndex;

    public SequencingHttpHandler((string Json, string? LinkHeader)[] responses)
        => _responses = responses;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (json, linkHeader) = _callIndex < _responses.Length
            ? _responses[_callIndex++]
            : ("[]", null);

        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        if (linkHeader is not null)
            response.Headers.TryAddWithoutValidation("Link", linkHeader);
        return Task.FromResult(response);
    }
}
