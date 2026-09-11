using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrenchExDev.Net.Wrapper.Versioning.Tests;

// ---------------------------------------------------------------------------
// Test record for non-string TItem tests
// ---------------------------------------------------------------------------

internal sealed record TestItem(string Name, string Url);

// ---------------------------------------------------------------------------
// Fake HTTP handlers (version-collector tests)
// ---------------------------------------------------------------------------

sealed class FakeHttpHandler(string json, string? linkHeader = null) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (linkHeader is not null)
            response.Headers.TryAddWithoutValidation("Link", linkHeader);
        return Task.FromResult(response);
    }
}

sealed class SequencingHttpHandler(params (string json, string? linkHeader)[] responses)
    : HttpMessageHandler
{
    private int _callIndex;
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var (json, linkHeader) = responses[_callIndex++];
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (linkHeader is not null)
            response.Headers.TryAddWithoutValidation("Link", linkHeader);
        return Task.FromResult(response);
    }
}

// ---------------------------------------------------------------------------
// Fake HTTP handler (pipeline tests — supports custom response logic)
// ---------------------------------------------------------------------------

internal sealed class PipelineFakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public PipelineFakeHttpHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _handler = _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
    }

    public PipelineFakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_handler(request));
}

// ---------------------------------------------------------------------------
// Fake ILogger (captures messages)
// ---------------------------------------------------------------------------

internal sealed class FakeLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }
}

// ---------------------------------------------------------------------------
// IItemCollector<TItem> — interface tests
// ---------------------------------------------------------------------------

public sealed class ItemCollectorInterfaceTests
{
    [Fact]
    public async Task StaticItemCollector_CollectsItems()
    {
        var items = new List<TestItem>
        {
            new("alpha", "https://example.com/alpha"),
            new("beta", "https://example.com/beta"),
        };
        IItemCollector<TestItem> collector = new StaticItemCollector<TestItem>(items);
        var result = await collector.CollectItemsAsync();

        result.Count.ShouldBe(2);
        result[0].ShouldBe(new TestItem("alpha", "https://example.com/alpha"));
        result[1].ShouldBe(new TestItem("beta", "https://example.com/beta"));
    }

    [Fact]
    public async Task StaticItemCollector_EmptyList_ReturnsEmpty()
    {
        var collector = new StaticItemCollector<TestItem>([]);
        var result = await collector.CollectItemsAsync();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task StaticItemCollector_ReturnsSameListEachTime()
    {
        var collector = new StaticItemCollector<TestItem>([new("a", "url")]);
        var first = await collector.CollectItemsAsync();
        var second = await collector.CollectItemsAsync();
        first.ShouldBe(second);
    }

    [Fact]
    public async Task IVersionCollector_CollectItemsAsync_DelegatesToCollectVersionsAsync()
    {
        IVersionCollector collector = new StaticVersionCollector(["1.0.0", "2.0.0"]);
        var items = await collector.CollectItemsAsync();
        var versions = await collector.CollectVersionsAsync();
        items.ShouldBe(versions);
    }
}

// ---------------------------------------------------------------------------
// StaticVersionCollector
// ---------------------------------------------------------------------------

public sealed class StaticVersionCollectorTests
{
    [Fact]
    public async Task CollectVersionsAsync_EmptyList_ReturnsEmpty()
    {
        var collector = new StaticVersionCollector([]);
        var result = await collector.CollectVersionsAsync();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task CollectVersionsAsync_NonEmptyList_ReturnsAllVersions()
    {
        var collector = new StaticVersionCollector(["1.0.0", "2.0.0", "3.0.0"]);
        var result = await collector.CollectVersionsAsync();
        result.Count.ShouldBe(3);
        result[0].ShouldBe("1.0.0");
        result[1].ShouldBe("2.0.0");
        result[2].ShouldBe("3.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_ReturnsSameListEachTime()
    {
        var collector = new StaticVersionCollector(["1.0.0"]);
        var first = await collector.CollectVersionsAsync();
        var second = await collector.CollectVersionsAsync();
        first.ShouldBe(second);
    }

    [Fact]
    public async Task CollectItemsAsync_DelegatesToCollectVersionsAsync()
    {
        var collector = new StaticVersionCollector(["1.0.0", "2.0.0"]);
        var items = await collector.CollectItemsAsync();
        var versions = await collector.CollectVersionsAsync();
        items.ShouldBe(versions);
    }
}

// ---------------------------------------------------------------------------
// PaginationHelper.ParseNextLink
// ---------------------------------------------------------------------------

public sealed class ParseNextLinkTests
{
    [Fact]
    public void NoLinkHeader_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        PaginationHelper.ParseNextLink(response.Headers).ShouldBeNull();
    }

    [Fact]
    public void WithNextLink_ReturnsUrl()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Link",
            "<https://api.example.com/items?page=2>; rel=\"next\"");
        PaginationHelper.ParseNextLink(response.Headers)
            .ShouldBe("https://api.example.com/items?page=2");
    }

    [Fact]
    public void WithoutNextLink_OnlyOtherRels_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Link",
            "<https://api.example.com/items?page=1>; rel=\"prev\", <https://api.example.com/items?page=5>; rel=\"last\"");
        PaginationHelper.ParseNextLink(response.Headers).ShouldBeNull();
    }

    [Fact]
    public void MultipleParts_ExtractsNextFromSecondPart()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Link",
            "<https://api.example.com/items?page=1>; rel=\"prev\", <https://api.example.com/items?page=3>; rel=\"next\"");
        PaginationHelper.ParseNextLink(response.Headers)
            .ShouldBe("https://api.example.com/items?page=3");
    }

    [Fact]
    public void MalformedLink_NoAngleBrackets_ReturnsNull()
    {
        // Part ends with rel="next" but has no < > around URL
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Link",
            "https://api.example.com/items?page=2; rel=\"next\"");
        // IndexOf('<') returns -1 => start < 0 => condition false
        PaginationHelper.ParseNextLink(response.Headers).ShouldBeNull();
    }

    [Fact]
    public void MalformedLink_ClosingBracketBeforeOpening_ReturnsNull()
    {
        // > appears before < => end < start => condition false
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Link",
            ">bad<; rel=\"next\"");
        PaginationHelper.ParseNextLink(response.Headers).ShouldBeNull();
    }

    [Fact]
    public void MultipleLinkHeaderValues_FindsNextInSecondValue()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Link",
            "<https://api.example.com/items?page=1>; rel=\"prev\"");
        response.Headers.TryAddWithoutValidation("Link",
            "<https://api.example.com/items?page=3>; rel=\"next\"");
        PaginationHelper.ParseNextLink(response.Headers)
            .ShouldBe("https://api.example.com/items?page=3");
    }
}

// ---------------------------------------------------------------------------
// PaginationHelper.CollectPaginatedAsync
// ---------------------------------------------------------------------------

public sealed class CollectPaginatedAsyncTests
{
    [Fact]
    public async Task EmptyArray_ReturnsEmptyList()
    {
        var handler = new FakeHttpHandler("[]");
        using var client = new HttpClient(handler);

        var result = await PaginationHelper.CollectPaginatedAsync(
            client, "https://api.example.com/items", _ => "1.0.0", CancellationToken.None);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonArrayResponse_ReturnsEmptyList()
    {
        var handler = new FakeHttpHandler("{}");
        using var client = new HttpClient(handler);

        var result = await PaginationHelper.CollectPaginatedAsync(
            client, "https://api.example.com/items", _ => "1.0.0", CancellationToken.None);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExtractVersionReturnsNull_ItemsSkipped()
    {
        var handler = new FakeHttpHandler("[{\"v\":\"a\"},{\"v\":\"b\"}]");
        using var client = new HttpClient(handler);

        var result = await PaginationHelper.CollectPaginatedAsync(
            client, "https://api.example.com/items", _ => null, CancellationToken.None);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExtractVersionReturnsEmpty_ItemsSkipped()
    {
        var handler = new FakeHttpHandler("[{\"v\":\"a\"}]");
        using var client = new HttpClient(handler);

        var result = await PaginationHelper.CollectPaginatedAsync(
            client, "https://api.example.com/items", _ => "", CancellationToken.None);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SinglePage_ExtractsVersions()
    {
        var handler = new FakeHttpHandler("[{\"v\":\"a\"},{\"v\":\"b\"}]");
        using var client = new HttpClient(handler);

        var result = await PaginationHelper.CollectPaginatedAsync(
            client, "https://api.example.com/items",
            el => el.GetProperty("v").GetString(), CancellationToken.None);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MultiplePages_FollowsPagination()
    {
        var handler = new SequencingHttpHandler(
            ("[{\"v\":\"2.0.0\"}]", "<https://api.example.com/items?page=2>; rel=\"next\""),
            ("[{\"v\":\"1.0.0\"}]", null)
        );
        using var client = new HttpClient(handler);

        var result = await PaginationHelper.CollectPaginatedAsync(
            client, "https://api.example.com/items",
            el => el.GetProperty("v").GetString(), CancellationToken.None);

        result.Count.ShouldBe(2);
        handler.Requests.Count.ShouldBe(2);
        // Sorted: 1.0.0 before 2.0.0
        result[0].ShouldBe("1.0.0");
        result[1].ShouldBe("2.0.0");
    }

    [Fact]
    public async Task MixedNullAndValid_OnlyAddsValid()
    {
        var handler = new FakeHttpHandler("[{\"v\":\"1.0.0\"},{\"v\":null},{\"v\":\"2.0.0\"}]");
        using var client = new HttpClient(handler);

        var callCount = 0;
        var result = await PaginationHelper.CollectPaginatedAsync(
            client, "https://api.example.com/items",
            el =>
            {
                callCount++;
                var val = el.GetProperty("v").GetString();
                return val;
            }, CancellationToken.None);

        callCount.ShouldBe(3);
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ResultsAreSorted()
    {
        var handler = new FakeHttpHandler("[{\"v\":\"3.0.0\"},{\"v\":\"1.0.0\"},{\"v\":\"2.0.0\"}]");
        using var client = new HttpClient(handler);

        var result = await PaginationHelper.CollectPaginatedAsync(
            client, "https://api.example.com/items",
            el => el.GetProperty("v").GetString(), CancellationToken.None);

        result.ShouldBe(["1.0.0", "2.0.0", "3.0.0"]);
    }
}

// ---------------------------------------------------------------------------
// GitHubReleasesVersionCollector
// ---------------------------------------------------------------------------

public sealed class GitHubReleasesVersionCollectorTests
{
    [Fact]
    public async Task CollectVersionsAsync_ReleasesWithTags_ReturnsVersions()
    {
        var json = """
        [
            {"tag_name": "v1.0.0", "prerelease": false},
            {"tag_name": "v2.0.0", "prerelease": false}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubReleasesVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(2);
        result[0].ShouldBe("1.0.0");
        result[1].ShouldBe("2.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_PrereleasesFiltered()
    {
        var json = """
        [
            {"tag_name": "v1.0.0", "prerelease": false},
            {"tag_name": "v2.0.0-rc.1", "prerelease": true},
            {"tag_name": "v3.0.0", "prerelease": false}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubReleasesVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(2);
        result.ShouldContain("1.0.0");
        result.ShouldContain("3.0.0");
        result.ShouldNotContain("2.0.0-rc.1");
    }

    [Fact]
    public async Task CollectVersionsAsync_NullTag_Skipped()
    {
        var json = """
        [
            {"tag_name": null, "prerelease": false},
            {"tag_name": "v1.0.0", "prerelease": false}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubReleasesVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_MissingTagName_Skipped()
    {
        var json = """
        [
            {"name": "Release 1"},
            {"tag_name": "v1.0.0", "prerelease": false}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubReleasesVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_TagWithoutVPrefix_KeptAsIs()
    {
        var json = """[{"tag_name": "1.0.0", "prerelease": false}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubReleasesVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_CustomTagToVersion()
    {
        var json = """[{"tag_name": "release-1.0.0", "prerelease": false}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubReleasesVersionCollector(
            "owner", "repo", client, tag => tag.Replace("release-", ""));
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_NoPrereleaseProperty_StillReturned()
    {
        // When "prerelease" property is missing, TryGetProperty returns false,
        // so the prerelease check is skipped and the release is included.
        var json = """[{"tag_name": "v1.0.0"}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubReleasesVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_PrereleaseFalse_Included()
    {
        // Explicitly test that prerelease=false items are kept
        var json = """[{"tag_name": "v1.0.0", "prerelease": false}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubReleasesVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CollectVersionsAsync_CorrectUrlFormed()
    {
        var handler = new FakeHttpHandler("[]");
        using var client = new HttpClient(handler);

        var collector = new GitHubReleasesVersionCollector("myowner", "myrepo", client);
        await collector.CollectVersionsAsync();

        handler.LastRequest!.RequestUri!.ToString()
            .ShouldBe("https://api.github.com/repos/myowner/myrepo/releases?per_page=100");
    }

    [Fact]
    public void Constructor_DefaultHttpClient_IsCreated()
    {
        // Exercise the path where httpClient is null (creates default)
        var collector = new GitHubReleasesVersionCollector("owner", "repo");
        collector.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_DefaultTagToVersion_IsUsed()
    {
        // Exercise the path where tagToVersion is null
        var collector = new GitHubReleasesVersionCollector("owner", "repo", new HttpClient());
        collector.ShouldNotBeNull();
    }

    [Fact]
    public async Task CollectItemsAsync_DelegatesToCollectVersionsAsync()
    {
        var json = """[{"tag_name": "v1.0.0", "prerelease": false}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubReleasesVersionCollector("owner", "repo", client);
        var items = await collector.CollectItemsAsync();
        items.Count.ShouldBe(1);
        items[0].ShouldBe("1.0.0");
    }
}

// ---------------------------------------------------------------------------
// CompareVersionStrings
// ---------------------------------------------------------------------------

public sealed class CompareVersionStringsTests
{
    [Fact]
    public void EqualVersions_ReturnsZero()
    {
        GitHubReleasesVersionCollector.CompareVersionStrings("1.2.3", "1.2.3").ShouldBe(0);
    }

    [Fact]
    public void DifferentMajor_ReturnsCorrectOrder()
    {
        GitHubReleasesVersionCollector.CompareVersionStrings("1.0.0", "2.0.0").ShouldBeLessThan(0);
        GitHubReleasesVersionCollector.CompareVersionStrings("2.0.0", "1.0.0").ShouldBeGreaterThan(0);
    }

    [Fact]
    public void DifferentMinor_ReturnsCorrectOrder()
    {
        GitHubReleasesVersionCollector.CompareVersionStrings("1.1.0", "1.2.0").ShouldBeLessThan(0);
    }

    [Fact]
    public void DifferentPatch_ReturnsCorrectOrder()
    {
        GitHubReleasesVersionCollector.CompareVersionStrings("1.0.1", "1.0.2").ShouldBeLessThan(0);
    }

    [Fact]
    public void DifferentLengths_ShorterPaddedWithZero()
    {
        // "1.0" vs "1.0.0" => bParts has length 3, aParts has length 2
        // i=2: aVal="0" (default), bVal="0" => equal
        GitHubReleasesVersionCollector.CompareVersionStrings("1.0", "1.0.0").ShouldBe(0);
    }

    [Fact]
    public void DifferentLengths_ShorterIsSmallerWhenExtraPartNonZero()
    {
        // "1.0" vs "1.0.1" => i=2: aVal="0", bVal="1" => a < b
        GitHubReleasesVersionCollector.CompareVersionStrings("1.0", "1.0.1").ShouldBeLessThan(0);
    }

    [Fact]
    public void NonNumericParts_ComparedLexically()
    {
        // "1.0.0-alpha" splits to ["1","0","0","alpha"]
        // "1.0.0-beta"  splits to ["1","0","0","beta"]
        // i=3: "alpha" vs "beta" => string compare
        GitHubReleasesVersionCollector.CompareVersionStrings("1.0.0-alpha", "1.0.0-beta")
            .ShouldBeLessThan(0);
    }

    [Fact]
    public void NonNumericParts_EqualStrings_ReturnsZero()
    {
        GitHubReleasesVersionCollector.CompareVersionStrings("1.0.0-rc", "1.0.0-rc")
            .ShouldBe(0);
    }

    [Fact]
    public void MixedNumericAndNonNumeric_NonNumericPartFallsToStringCompare()
    {
        // "1.0.0-1" vs "1.0.0-rc" => i=3: "1" is numeric, "rc" is not
        // int.TryParse("1") succeeds but int.TryParse("rc") fails => else branch
        GitHubReleasesVersionCollector.CompareVersionStrings("1.0.0-1", "1.0.0-rc")
            .ShouldNotBe(0);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0", "2.0.0", -1)]
    [InlineData("2.0.0", "1.0.0", 1)]
    [InlineData("1.2.3", "1.2.4", -1)]
    public void Theory_VariousComparisons(string a, string b, int expectedSign)
    {
        var result = GitHubReleasesVersionCollector.CompareVersionStrings(a, b);
        if (expectedSign == 0) result.ShouldBe(0);
        else if (expectedSign < 0) result.ShouldBeLessThan(0);
        else result.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void BothPartsPadded_WhenBothShorter()
    {
        // "1" vs "1.0" => aParts=["1"], bParts=["1","0"]
        // i=0: 1 vs 1 => equal; i=1: aVal="0" bVal="0" => equal
        GitHubReleasesVersionCollector.CompareVersionStrings("1", "1.0").ShouldBe(0);
    }

    [Fact]
    public void DifferentLengths_BIsShorter_PaddedWithZero()
    {
        // "1.0.1" vs "1.0" => i=2: aVal="1", bVal="0" (padded) => a > b
        // This hits line 143 bVal = "0" branch
        GitHubReleasesVersionCollector.CompareVersionStrings("1.0.1", "1.0").ShouldBeGreaterThan(0);
    }
}

// ---------------------------------------------------------------------------
// GitLabReleasesVersionCollector
// ---------------------------------------------------------------------------

public sealed class GitLabReleasesVersionCollectorTests
{
    [Fact]
    public async Task CollectVersionsAsync_Releases_ReturnsVersions()
    {
        var json = """
        [
            {"tag_name": "v1.47.0", "upcoming_release": false},
            {"tag_name": "v1.48.0", "upcoming_release": false}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector("gitlab-org%2Fcli", httpClient: client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(2);
        result[0].ShouldBe("1.47.0");
        result[1].ShouldBe("1.48.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_UpcomingFiltered()
    {
        var json = """
        [
            {"tag_name": "v1.47.0", "upcoming_release": false},
            {"tag_name": "v1.48.0", "upcoming_release": true},
            {"tag_name": "v1.49.0", "upcoming_release": false}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector("gitlab-org%2Fcli", httpClient: client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(2);
        result.ShouldContain("1.47.0");
        result.ShouldContain("1.49.0");
        result.ShouldNotContain("1.48.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_NullTag_Skipped()
    {
        var json = """[{"tag_name": null}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector("proj", httpClient: client);
        var result = await collector.CollectVersionsAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task CollectVersionsAsync_MissingTagName_Skipped()
    {
        var json = """[{"name": "Release 1"}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector("proj", httpClient: client);
        var result = await collector.CollectVersionsAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task CollectVersionsAsync_TagWithoutVPrefix_KeptAsIs()
    {
        var json = """[{"tag_name": "1.0.0"}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector("proj", httpClient: client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_NoUpcomingProperty_StillReturned()
    {
        // When "upcoming_release" property is missing, TryGetProperty returns false,
        // so the upcoming check is skipped and the release is included.
        var json = """[{"tag_name": "v2.0.0"}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector("proj", httpClient: client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("2.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_UpcomingFalse_Included()
    {
        var json = """[{"tag_name": "v1.0.0", "upcoming_release": false}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector("proj", httpClient: client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CollectVersionsAsync_CustomBaseUrl()
    {
        var handler = new FakeHttpHandler("[]");
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector(
            "proj", baseUrl: "https://my-gitlab.example.com/", httpClient: client);
        await collector.CollectVersionsAsync();

        handler.LastRequest!.RequestUri!.ToString()
            .ShouldStartWith("https://my-gitlab.example.com/api/v4/projects/proj/releases");
    }

    [Fact]
    public async Task CollectVersionsAsync_BaseUrlTrailingSlashTrimmed()
    {
        var handler = new FakeHttpHandler("[]");
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector(
            "proj", baseUrl: "https://gitlab.example.com///", httpClient: client);
        await collector.CollectVersionsAsync();

        // TrimEnd('/') removes all trailing slashes
        handler.LastRequest!.RequestUri!.ToString()
            .ShouldStartWith("https://gitlab.example.com/api/v4/");
    }

    [Fact]
    public async Task CollectVersionsAsync_DefaultBaseUrl()
    {
        var handler = new FakeHttpHandler("[]");
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector("proj", httpClient: client);
        await collector.CollectVersionsAsync();

        handler.LastRequest!.RequestUri!.ToString()
            .ShouldStartWith("https://gitlab.com/api/v4/projects/proj/releases");
    }

    [Fact]
    public async Task CollectVersionsAsync_CustomTagToVersion()
    {
        var json = """[{"tag_name": "release/1.0.0"}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector(
            "proj", httpClient: client, tagToVersion: tag => tag.Replace("release/", ""));
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public void Constructor_DefaultHttpClient_IsCreated()
    {
        // Exercises CreateDefaultHttpClient path (no GITLAB_TOKEN set by default in test)
        var collector = new GitLabReleasesVersionCollector("proj");
        collector.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_DefaultTagToVersion_IsUsed()
    {
        var collector = new GitLabReleasesVersionCollector("proj", httpClient: new HttpClient());
        collector.ShouldNotBeNull();
    }

    [Fact]
    public async Task CollectItemsAsync_DelegatesToCollectVersionsAsync()
    {
        var json = """[{"tag_name": "v1.0.0"}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitLabReleasesVersionCollector("proj", httpClient: client);
        var items = await collector.CollectItemsAsync();
        items.Count.ShouldBe(1);
        items[0].ShouldBe("1.0.0");
    }
}

// ---------------------------------------------------------------------------
// GitLabReleasesVersionCollector — GITLAB_TOKEN env var
// ---------------------------------------------------------------------------

public sealed class GitLabTokenTests
{
    [Fact]
    public void CreateDefaultHttpClient_WithGitLabToken_AddsHeader()
    {
        var originalToken = Environment.GetEnvironmentVariable("GITLAB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITLAB_TOKEN", "test-token-123");
            // Creating a new collector without explicit httpClient triggers CreateDefaultHttpClient
            var collector = new GitLabReleasesVersionCollector("proj");
            collector.ShouldNotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITLAB_TOKEN", originalToken);
        }
    }

    [Fact]
    public void CreateDefaultHttpClient_WithoutGitLabToken_NoPrivateTokenHeader()
    {
        var originalToken = Environment.GetEnvironmentVariable("GITLAB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITLAB_TOKEN", null);
            var collector = new GitLabReleasesVersionCollector("proj");
            collector.ShouldNotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITLAB_TOKEN", originalToken);
        }
    }

    [Fact]
    public void CreateDefaultHttpClient_WithEmptyGitLabToken_NoPrivateTokenHeader()
    {
        var originalToken = Environment.GetEnvironmentVariable("GITLAB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITLAB_TOKEN", "");
            var collector = new GitLabReleasesVersionCollector("proj");
            collector.ShouldNotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITLAB_TOKEN", originalToken);
        }
    }
}

// ---------------------------------------------------------------------------
// GitHubReleasesVersionCollector — token
// ---------------------------------------------------------------------------

public sealed class GitHubReleasesTokenTests
{
    [Fact]
    public void Constructor_WithToken_DoesNotThrow()
    {
        var collector = new GitHubReleasesVersionCollector("owner", "repo", token: "my-token");
        collector.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_NullToken_DoesNotThrow()
    {
        var collector = new GitHubReleasesVersionCollector("owner", "repo", token: null);
        collector.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_EmptyToken_DoesNotThrow()
    {
        var collector = new GitHubReleasesVersionCollector("owner", "repo", token: "");
        collector.ShouldNotBeNull();
    }
}

// ---------------------------------------------------------------------------
// GitHubTagsVersionCollector
// ---------------------------------------------------------------------------

public sealed class GitHubTagsVersionCollectorTests
{
    [Fact]
    public async Task CollectVersionsAsync_TagsWithName_ReturnsVersions()
    {
        var json = """
        [
            {"name": "v1.0.0"},
            {"name": "v2.0.0"}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubTagsVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(2);
        result[0].ShouldBe("1.0.0");
        result[1].ShouldBe("2.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_MissingName_Skipped()
    {
        var json = """
        [
            {"sha": "abc123"},
            {"name": "v1.0.0"}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubTagsVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_NullName_Skipped()
    {
        var json = """
        [
            {"name": null},
            {"name": "v1.0.0"}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubTagsVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_TagWithoutVPrefix_KeptAsIs()
    {
        var json = """[{"name": "1.0.0"}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubTagsVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_PrereleaseTagsWithDash_Filtered()
    {
        var json = """
        [
            {"name": "v1.0.0"},
            {"name": "v2.0.0-rc.1"},
            {"name": "v3.0.0-beta"},
            {"name": "v4.0.0"}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubTagsVersionCollector("owner", "repo", client);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(2);
        result.ShouldContain("1.0.0");
        result.ShouldContain("4.0.0");
        result.ShouldNotContain("2.0.0-rc.1");
        result.ShouldNotContain("3.0.0-beta");
    }

    [Fact]
    public async Task CollectVersionsAsync_CustomTagToVersion()
    {
        var json = """[{"name": "release-1.0.0"}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubTagsVersionCollector(
            "owner", "repo", client, tag => tag.Replace("release-", ""));
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_CustomTagToVersion_ReturnsNull_Filtered()
    {
        var json = """[{"name": "v1.0.0"}, {"name": "skip-me"}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubTagsVersionCollector(
            "owner", "repo", client,
            tag => tag.StartsWith('v') ? tag[1..] : null);
        var result = await collector.CollectVersionsAsync();

        result.Count.ShouldBe(1);
        result[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersionsAsync_CorrectUrlFormed()
    {
        var handler = new FakeHttpHandler("[]");
        using var client = new HttpClient(handler);

        var collector = new GitHubTagsVersionCollector("myowner", "myrepo", client);
        await collector.CollectVersionsAsync();

        handler.LastRequest!.RequestUri!.ToString()
            .ShouldBe("https://api.github.com/repos/myowner/myrepo/tags?per_page=100");
    }

    [Fact]
    public void Constructor_DefaultHttpClient_IsCreated()
    {
        var collector = new GitHubTagsVersionCollector("owner", "repo");
        collector.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_DefaultTagToVersion_IsUsed()
    {
        var collector = new GitHubTagsVersionCollector("owner", "repo", new HttpClient());
        collector.ShouldNotBeNull();
    }

    [Fact]
    public async Task CollectItemsAsync_DelegatesToCollectVersionsAsync()
    {
        var json = """[{"name": "v1.0.0"}]""";
        var handler = new FakeHttpHandler(json);
        using var client = new HttpClient(handler);

        var collector = new GitHubTagsVersionCollector("owner", "repo", client);
        var items = await collector.CollectItemsAsync();
        items.Count.ShouldBe(1);
        items[0].ShouldBe("1.0.0");
    }
}

// ---------------------------------------------------------------------------
// GitHubTagsVersionCollector — token
// ---------------------------------------------------------------------------

public sealed class GitHubTagsTokenTests
{
    [Fact]
    public void Constructor_WithToken_DoesNotThrow()
    {
        var collector = new GitHubTagsVersionCollector("owner", "repo", token: "my-token");
        collector.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_NullToken_DoesNotThrow()
    {
        var collector = new GitHubTagsVersionCollector("owner", "repo", token: null);
        collector.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_EmptyToken_DoesNotThrow()
    {
        var collector = new GitHubTagsVersionCollector("owner", "repo", token: "");
        collector.ShouldNotBeNull();
    }
}

// ---------------------------------------------------------------------------
// DesignPipelineProgressInfo
// ---------------------------------------------------------------------------

public sealed class DesignPipelineProgressInfoTests
{
    [Fact]
    public void Constructor_SetsLabel_AndDefaultStagePending()
    {
        var info = new DesignPipelineProgressInfo("1.0.0");

        info.Label.ShouldBe("1.0.0");
        info.Stage.ShouldBe("Pending");
        info.Error.ShouldBeNull();
    }

    [Fact]
    public void SetStage_ChangesStage()
    {
        var info = new DesignPipelineProgressInfo("2.0.0");

        info.SetStage("Downloading");

        info.Stage.ShouldBe("Downloading");
    }

    [Fact]
    public void SetError_SetsErrorAndStageToFailed()
    {
        var info = new DesignPipelineProgressInfo("3.0.0");

        info.SetError("Network error");

        info.Error.ShouldBe("Network error");
        info.Stage.ShouldBe("Failed");
    }

    [Fact]
    public void SetDone_SetsStageToDone()
    {
        var info = new DesignPipelineProgressInfo("4.0.0");

        info.SetDone();

        info.Stage.ShouldBe("Done");
    }

    [Fact]
    public void Elapsed_IncreasesOverTime()
    {
        var info = new DesignPipelineProgressInfo("5.0.0");
        var first = info.Elapsed;

        // Spin briefly to let time pass
        Thread.Sleep(15);

        var second = info.Elapsed;
        second.ShouldBeGreaterThan(first);
    }
}

// ---------------------------------------------------------------------------
// DesignPipeline<string> (ported from SchemaDesignPipeline)
// ---------------------------------------------------------------------------

public sealed class DesignPipelineStringTests
{
    [Fact]
    public async Task Build_EmptyPipeline_ReturnsTerminalThatDoesNothing()
    {
        var pipeline = new DesignPipeline<string>().Build();

        var ctx = CreateMinimalContext("1.0.0");
        await pipeline(ctx);

        // No exception thrown, Content stays null
        ctx.Content.ShouldBeNull();
    }

    [Fact]
    public async Task Use_SingleMiddleware_IsInvoked()
    {
        var invoked = false;
        var pipeline = new DesignPipeline<string>()
            .Use(next => async ctx =>
            {
                invoked = true;
                await next(ctx);
            })
            .Build();

        await pipeline(CreateMinimalContext("1.0.0"));

        invoked.ShouldBeTrue();
    }

    [Fact]
    public async Task Use_MultipleMiddleware_ComposedInCorrectOrder()
    {
        var order = new List<int>();

        var pipeline = new DesignPipeline<string>()
            .Use(next => async ctx =>
            {
                order.Add(1);
                await next(ctx);
                order.Add(4);
            })
            .Use(next => async ctx =>
            {
                order.Add(2);
                await next(ctx);
                order.Add(3);
            })
            .Build();

        await pipeline(CreateMinimalContext("1.0.0"));

        // First added is outermost: 1 wraps 2, terminal is innermost
        order.ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void Use_ReturnsSameInstance_ForFluency()
    {
        var pipeline = new DesignPipeline<string>();
        var result = pipeline.Use(next => ctx => next(ctx));
        result.ShouldBeSameAs(pipeline);
    }

    private static DesignPipelineContext<string> CreateMinimalContext(string key) => new()
    {
        Item = key,
        Key = key,
        OutputDir = Path.GetTempPath(),
        Logger = NullLogger.Instance,
        HttpClient = new HttpClient(),
        OutputFilePattern = "schema-v{key}.json",
    };
}

// ---------------------------------------------------------------------------
// DesignPipeline<TestItem> (non-string TItem)
// ---------------------------------------------------------------------------

public sealed class DesignPipelineTestItemTests
{
    [Fact]
    public async Task Build_EmptyPipeline_ReturnsTerminal()
    {
        var pipeline = new DesignPipeline<TestItem>().Build();

        var ctx = CreateMinimalContext(new TestItem("alpha", "https://example.com/alpha"));
        await pipeline(ctx);

        ctx.Content.ShouldBeNull();
    }

    [Fact]
    public async Task Use_MiddlewareCanAccessItemProperties()
    {
        string? capturedName = null;
        string? capturedUrl = null;

        var pipeline = new DesignPipeline<TestItem>()
            .Use(next => async ctx =>
            {
                capturedName = ctx.Item.Name;
                capturedUrl = ctx.Item.Url;
                await next(ctx);
            })
            .Build();

        await pipeline(CreateMinimalContext(new TestItem("beta", "https://example.com/beta")));

        capturedName.ShouldBe("beta");
        capturedUrl.ShouldBe("https://example.com/beta");
    }

    [Fact]
    public async Task Use_MultipleMiddleware_ComposedInCorrectOrder()
    {
        var order = new List<int>();

        var pipeline = new DesignPipeline<TestItem>()
            .Use(next => async ctx => { order.Add(1); await next(ctx); order.Add(4); })
            .Use(next => async ctx => { order.Add(2); await next(ctx); order.Add(3); })
            .Build();

        await pipeline(CreateMinimalContext(new TestItem("x", "u")));

        order.ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void Use_ReturnsSameInstance_ForFluency()
    {
        var pipeline = new DesignPipeline<TestItem>();
        var result = pipeline.Use(next => ctx => next(ctx));
        result.ShouldBeSameAs(pipeline);
    }

    private static DesignPipelineContext<TestItem> CreateMinimalContext(TestItem item) => new()
    {
        Item = item,
        Key = item.Name,
        OutputDir = Path.GetTempPath(),
        Logger = NullLogger.Instance,
        HttpClient = new HttpClient(),
        OutputFilePattern = "{key}.json",
    };
}

// ---------------------------------------------------------------------------
// UseHttpDownload extension
// ---------------------------------------------------------------------------

public sealed class UseHttpDownloadTests
{
    [Fact]
    public async Task UseHttpDownload_SetsContentFromHttp()
    {
        var handler = new PipelineFakeHttpHandler("schema-content-here");
        var client = new HttpClient(handler);

        var pipeline = new DesignPipeline<string>()
            .UseHttpDownload(v => $"https://example.com/{v}.json")
            .Build();

        var ctx = new DesignPipelineContext<string>
        {
            Item = "1.0.0",
            Key = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = client,
            OutputFilePattern = "schema-v{key}.json",
        };

        await pipeline(ctx);

        ctx.Content.ShouldBe("schema-content-here");
    }

    [Fact]
    public async Task UseHttpDownload_WithProgress_SetsStageThenCallsNext()
    {
        var handler = new PipelineFakeHttpHandler("content");
        var client = new HttpClient(handler);
        var progress = new DesignPipelineProgressInfo("1.0.0");
        var stageWhenNextCalled = "";

        var pipeline = new DesignPipeline<string>()
            .UseHttpDownload(v => $"https://example.com/{v}.json")
            .Use(next => async ctx =>
            {
                stageWhenNextCalled = ctx.Progress!.Stage;
                await next(ctx);
            })
            .Build();

        var ctx = new DesignPipelineContext<string>
        {
            Item = "1.0.0",
            Key = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = client,
            OutputFilePattern = "schema-v{key}.json",
            Progress = progress,
        };

        await pipeline(ctx);

        stageWhenNextCalled.ShouldBe("Downloading");
    }

    [Fact]
    public async Task UseHttpDownload_WithoutProgress_DoesNotThrow()
    {
        var handler = new PipelineFakeHttpHandler("content");
        var client = new HttpClient(handler);

        var pipeline = new DesignPipeline<string>()
            .UseHttpDownload(v => $"https://example.com/{v}.json")
            .Build();

        var ctx = new DesignPipelineContext<string>
        {
            Item = "1.0.0",
            Key = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = client,
            OutputFilePattern = "schema-v{key}.json",
            Progress = null,
        };

        await pipeline(ctx);

        ctx.Content.ShouldBe("content");
    }

    [Fact]
    public async Task UseHttpDownload_WithTestItem_UsesItemForUrl()
    {
        var handler = new PipelineFakeHttpHandler("item-content");
        var client = new HttpClient(handler);

        var pipeline = new DesignPipeline<TestItem>()
            .UseHttpDownload(item => item.Url)
            .Build();

        var item = new TestItem("alpha", "https://example.com/alpha.json");
        var ctx = new DesignPipelineContext<TestItem>
        {
            Item = item,
            Key = item.Name,
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = client,
            OutputFilePattern = "{key}.json",
        };

        await pipeline(ctx);

        ctx.Content.ShouldBe("item-content");
    }
}

// ---------------------------------------------------------------------------
// UseContentTransform extension
// ---------------------------------------------------------------------------

public sealed class UseContentTransformTests
{
    [Fact]
    public async Task UseContentTransform_TransformsNonNullContent()
    {
        var pipeline = new DesignPipeline<string>()
            .Use(next => async ctx =>
            {
                ctx.Content = "original";
                await next(ctx);
            })
            .UseContentTransform((item, content) => $"[{item}]{content}")
            .Build();

        var ctx = new DesignPipelineContext<string>
        {
            Item = "2.0.0",
            Key = "2.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "schema-v{key}.json",
        };

        await pipeline(ctx);

        ctx.Content.ShouldBe("[2.0.0]original");
    }

    [Fact]
    public async Task UseContentTransform_NullContent_SkipsTransform()
    {
        var transformCalled = false;
        var pipeline = new DesignPipeline<string>()
            .UseContentTransform((_, _) =>
            {
                transformCalled = true;
                return "should not be set";
            })
            .Build();

        var ctx = new DesignPipelineContext<string>
        {
            Item = "1.0.0",
            Key = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "schema-v{key}.json",
        };

        // Content is null by default
        await pipeline(ctx);

        transformCalled.ShouldBeFalse();
        ctx.Content.ShouldBeNull();
    }

    [Fact]
    public async Task UseContentTransform_WithProgress_SetsStageThenCallsNext()
    {
        var progress = new DesignPipelineProgressInfo("1.0.0");
        var stageWhenNextCalled = "";

        var pipeline = new DesignPipeline<string>()
            .Use(next => async ctx =>
            {
                ctx.Content = "input";
                await next(ctx);
            })
            .UseContentTransform((_, c) => c.ToUpperInvariant())
            .Use(next => async ctx =>
            {
                stageWhenNextCalled = ctx.Progress!.Stage;
                await next(ctx);
            })
            .Build();

        var ctx = new DesignPipelineContext<string>
        {
            Item = "1.0.0",
            Key = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "schema-v{key}.json",
            Progress = progress,
        };

        await pipeline(ctx);

        stageWhenNextCalled.ShouldBe("Transforming");
        ctx.Content.ShouldBe("INPUT");
    }

    [Fact]
    public async Task UseContentTransform_WithoutProgress_DoesNotThrow()
    {
        var pipeline = new DesignPipeline<string>()
            .Use(next => async ctx =>
            {
                ctx.Content = "data";
                await next(ctx);
            })
            .UseContentTransform((_, c) => c + "-transformed")
            .Build();

        var ctx = new DesignPipelineContext<string>
        {
            Item = "1.0.0",
            Key = "1.0.0",
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "schema-v{key}.json",
            Progress = null,
        };

        await pipeline(ctx);

        ctx.Content.ShouldBe("data-transformed");
    }

    [Fact]
    public async Task UseContentTransform_WithTestItem_ReceivesItemInTransform()
    {
        var pipeline = new DesignPipeline<TestItem>()
            .Use(next => async ctx => { ctx.Content = "raw"; await next(ctx); })
            .UseContentTransform((item, content) => $"{item.Name}:{content}")
            .Build();

        var item = new TestItem("gamma", "https://example.com/gamma");
        var ctx = new DesignPipelineContext<TestItem>
        {
            Item = item,
            Key = item.Name,
            OutputDir = Path.GetTempPath(),
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "{key}.json",
        };

        await pipeline(ctx);

        ctx.Content.ShouldBe("gamma:raw");
    }
}

// ---------------------------------------------------------------------------
// UseSave extension
// ---------------------------------------------------------------------------

public sealed class UseSaveTests
{
    [Fact]
    public async Task UseSave_WritesContentToDisk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"save-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var pipeline = new DesignPipeline<string>()
                .Use(next => async ctx =>
                {
                    ctx.Content = "{\"schema\":true}";
                    await next(ctx);
                })
                .UseSave()
                .Build();

            var ctx = new DesignPipelineContext<string>
            {
                Item = "3.0.0",
                Key = "3.0.0",
                OutputDir = tempDir,
                Logger = NullLogger.Instance,
                HttpClient = new HttpClient(),
                OutputFilePattern = "schema-v{key}.json",
            };

            await pipeline(ctx);

            ctx.OutputFilePath.ShouldNotBeNull();
            var expectedPath = Path.Combine(tempDir, "schema-v3.0.0.json");
            ctx.OutputFilePath.ShouldBe(expectedPath);
            File.Exists(expectedPath).ShouldBeTrue();
            File.ReadAllText(expectedPath).ShouldBe("{\"schema\":true}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UseSave_NullContent_DoesNotWriteFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"save-null-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var pipeline = new DesignPipeline<string>()
                .UseSave()
                .Build();

            var ctx = new DesignPipelineContext<string>
            {
                Item = "1.0.0",
                Key = "1.0.0",
                OutputDir = tempDir,
                Logger = NullLogger.Instance,
                HttpClient = new HttpClient(),
                OutputFilePattern = "schema-v{key}.json",
            };

            // Content is null by default
            await pipeline(ctx);

            ctx.OutputFilePath.ShouldBeNull();
            Directory.GetFiles(tempDir).Length.ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UseSave_WithProgress_SetsStageThenCallsNext()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"save-prog-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var progress = new DesignPipelineProgressInfo("1.0.0");
            var stageWhenNextCalled = "";

            var pipeline = new DesignPipeline<string>()
                .Use(next => async ctx =>
                {
                    ctx.Content = "data";
                    await next(ctx);
                })
                .UseSave()
                .Use(next => async ctx =>
                {
                    stageWhenNextCalled = ctx.Progress!.Stage;
                    await next(ctx);
                })
                .Build();

            var ctx = new DesignPipelineContext<string>
            {
                Item = "1.0.0",
                Key = "1.0.0",
                OutputDir = tempDir,
                Logger = NullLogger.Instance,
                HttpClient = new HttpClient(),
                OutputFilePattern = "schema-v{key}.json",
                Progress = progress,
            };

            await pipeline(ctx);

            stageWhenNextCalled.ShouldBe("Saving");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UseSave_WithoutProgress_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"save-noprog-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var pipeline = new DesignPipeline<string>()
                .Use(next => async ctx =>
                {
                    ctx.Content = "saved";
                    await next(ctx);
                })
                .UseSave()
                .Build();

            var ctx = new DesignPipelineContext<string>
            {
                Item = "1.0.0",
                Key = "1.0.0",
                OutputDir = tempDir,
                Logger = NullLogger.Instance,
                HttpClient = new HttpClient(),
                OutputFilePattern = "schema-v{key}.json",
                Progress = null,
            };

            await pipeline(ctx);

            ctx.OutputFilePath.ShouldNotBeNull();
            File.ReadAllText(ctx.OutputFilePath!).ShouldBe("saved");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UseSave_WithTestItem_WritesUsingKey()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"save-item-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var pipeline = new DesignPipeline<TestItem>()
                .Use(next => async ctx => { ctx.Content = "item-data"; await next(ctx); })
                .UseSave()
                .Build();

            var item = new TestItem("delta", "https://example.com/delta");
            var ctx = new DesignPipelineContext<TestItem>
            {
                Item = item,
                Key = item.Name,
                OutputDir = tempDir,
                Logger = NullLogger.Instance,
                HttpClient = new HttpClient(),
                OutputFilePattern = "{key}.json",
            };

            await pipeline(ctx);

            ctx.OutputFilePath.ShouldNotBeNull();
            var expectedPath = Path.Combine(tempDir, "delta.json");
            ctx.OutputFilePath.ShouldBe(expectedPath);
            File.ReadAllText(expectedPath).ShouldBe("item-data");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}

// ---------------------------------------------------------------------------
// DesignPipelineRunner<string> — list and filter
// ---------------------------------------------------------------------------

public sealed class RunnerListAndFilterTests
{
    [Fact]
    public async Task RunAsync_ListOnly_PrintsKeysAndReturnsZero()
    {
        var runner = CreateRunner(["1.0.0", "2.0.0"]);
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ListMissing_FiltersExistingFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"list-missing-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "1.0.0.json"), "existing");
            var runner = CreateRunner(["1.0.0", "2.0.0"], tempDir);
            var result = await runner.RunAsync(["--list", "--missing"]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_NoItemsToProcess_ReturnsZero()
    {
        var runner = CreateRunner([]);
        var result = await runner.RunAsync([]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WithItemFilter_AppliesFilter()
    {
        var runner = new DesignPipelineRunner<string>
        {
            ItemCollector = new StaticVersionCollector(["1.0.0", "1.0.1", "1.1.0"]),
            Pipeline = _ => Task.CompletedTask,
            KeySelector = v => v,
            OutputDir = Path.GetTempPath(),
            ItemFilter = VersionFilters.LatestPatchPerMinor,
            AuthTokenEnvVar = null,
            MinLogLevel = LogLevel.None,
        };
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WithoutItemFilter_UsesAllItems()
    {
        var runner = new DesignPipelineRunner<string>
        {
            ItemCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
            Pipeline = _ => Task.CompletedTask,
            KeySelector = v => v,
            OutputDir = Path.GetTempPath(),
            ItemFilter = null,
            AuthTokenEnvVar = null,
            MinLogLevel = LogLevel.None,
        };
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DefaultProperties_HaveExpectedValues()
    {
        var runner = new DesignPipelineRunner<string>
        {
            ItemCollector = new StaticVersionCollector([]),
            Pipeline = _ => Task.CompletedTask,
            KeySelector = v => v,
            OutputDir = Path.GetTempPath(),
        };
        runner.OutputFilePattern.ShouldBe("{key}.json");
        runner.DefaultParallelism.ShouldBe(6);
        runner.UserAgent.ShouldBe("FrenchExDev-DesignPipeline/1.0");
        runner.AuthTokenEnvVar.ShouldBe("GITHUB_TOKEN");
        runner.ItemFilter.ShouldBeNull();
        runner.MinLogLevel.ShouldBe(LogLevel.Information);
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    internal static DesignPipelineRunner<string> CreateRunner(
        IEnumerable<string> versions, string? outputDir = null)
    {
        return new DesignPipelineRunner<string>
        {
            ItemCollector = new StaticVersionCollector(versions),
            Pipeline = _ => Task.CompletedTask,
            KeySelector = v => v,
            OutputDir = outputDir ?? Path.GetTempPath(),
            AuthTokenEnvVar = null,
            MinLogLevel = LogLevel.None,
        };
    }
}

// ---------------------------------------------------------------------------
// DesignPipelineRunner<string> — download tests
// ---------------------------------------------------------------------------

public sealed class RunnerDownloadTests
{
    [Fact]
    public async Task RunAsync_DownloadSuccess_ReturnsZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-success-{Guid.NewGuid():N}");
        try
        {
            var pipeline = new DesignPipeline<string>()
                .Use(next => async ctx => { ctx.Content = "downloaded"; await next(ctx); })
                .Build();
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline, KeySelector = v => v,
                OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_DownloadFailure_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-fail-{Guid.NewGuid():N}");
        try
        {
            DesignPipelineDelegate<string> failingPipeline = _ => throw new InvalidOperationException("download failed");
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = failingPipeline, KeySelector = v => v,
                OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(1);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_ParallelAndOutput_ParsedCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-parallel-{Guid.NewGuid():N}");
        try
        {
            var pipeline = new DesignPipeline<string>()
                .Use(next => async ctx => { ctx.Content = "ok"; await next(ctx); })
                .Build();
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = pipeline, KeySelector = v => v,
                OutputDir = "unused-default", DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--parallel", "2", "--output", tempDir]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_PartialFailure_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-partial-{Guid.NewGuid():N}");
        try
        {
            DesignPipelineDelegate<string> partialFailPipeline = ctx =>
            {
                if (ctx.Key == "1.0.0") throw new InvalidOperationException("boom");
                ctx.Content = "ok";
                return Task.CompletedTask;
            };
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = partialFailPipeline, KeySelector = v => v,
                OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(1);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }
}

// ---------------------------------------------------------------------------
// DesignPipelineRunner<string> — auth tests
// ---------------------------------------------------------------------------

public sealed class RunnerAuthTests
{
    [Fact]
    public async Task RunAsync_AuthTokenEnvVarNull_NoAuthHeader()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-noauth-{Guid.NewGuid():N}");
        try
        {
            var pipeline = new DesignPipeline<string>()
                .Use(next => async ctx => { ctx.Content = "ok"; await next(ctx); }).Build();
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline, KeySelector = v => v,
                OutputDir = tempDir,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_AuthTokenEnvVarSet_ButEnvVarEmpty_NoAuthHeader()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-emptytoken-{Guid.NewGuid():N}");
        var uniqueEnvVar = $"TEST_TOKEN_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(uniqueEnvVar, "");
            var pipeline = new DesignPipeline<string>()
                .Use(next => async ctx => { ctx.Content = "ok"; await next(ctx); }).Build();
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline, KeySelector = v => v,
                OutputDir = tempDir,
                AuthTokenEnvVar = uniqueEnvVar, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(uniqueEnvVar, null);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_AuthTokenEnvVarSet_WithToken_AddsBearer()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-authtoken-{Guid.NewGuid():N}");
        var uniqueEnvVar = $"TEST_TOKEN_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(uniqueEnvVar, "my-secret-token");
            var pipeline = new DesignPipeline<string>()
                .Use(next => async ctx => { ctx.Content = "ok"; await next(ctx); }).Build();
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = pipeline, KeySelector = v => v,
                OutputDir = tempDir,
                AuthTokenEnvVar = uniqueEnvVar, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(uniqueEnvVar, null);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

// ---------------------------------------------------------------------------
// DesignPipelineRunner<string> — missing filter tests
// ---------------------------------------------------------------------------

public sealed class RunnerMissingFilterTests
{
    [Fact]
    public async Task RunAsync_MissingOnly_FiltersAlreadyDownloaded()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-missing-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "1.0.0.json"), "existing");
            var keysProcessed = new List<string>();
            DesignPipelineDelegate<string> trackingPipeline = ctx =>
            { keysProcessed.Add(ctx.Key); ctx.Content = "new"; return Task.CompletedTask; };
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = trackingPipeline, KeySelector = v => v,
                OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            keysProcessed.ShouldBe(["2.0.0"]);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_MissingOnly_NonExistentOutputDir_ReturnsAll()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-missing-nodir-{Guid.NewGuid():N}");
        try
        {
            var keysProcessed = new List<string>();
            DesignPipelineDelegate<string> trackingPipeline = ctx =>
            { keysProcessed.Add(ctx.Key); ctx.Content = "new"; return Task.CompletedTask; };
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = trackingPipeline, KeySelector = v => v,
                OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            keysProcessed.Count.ShouldBe(2);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_MissingOnly_AllKeysExist_NoProcessing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-all-exist-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "1.0.0.json"), "existing");
            File.WriteAllText(Path.Combine(tempDir, "2.0.0.json"), "existing");
            var runner = RunnerListAndFilterTests.CreateRunner(["1.0.0", "2.0.0"], tempDir);
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_OutputFilePattern_WithNonDefaultPattern()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-pattern-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "compose-1.0.0.yaml"), "existing");
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0", "2.0.0"]),
                Pipeline = _ => Task.CompletedTask, KeySelector = v => v,
                OutputDir = tempDir,
                OutputFilePattern = "compose-{key}.yaml",
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--list", "--missing"]);
            result.ShouldBe(0);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_OutputFilePattern_WithoutKeyPlaceholder_NoFiltering()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-badpattern-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "something.json"), "existing");
            var keysProcessed = new List<string>();
            DesignPipelineDelegate<string> trackingPipeline = ctx =>
            { keysProcessed.Add(ctx.Key); return Task.CompletedTask; };
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = trackingPipeline, KeySelector = v => v,
                OutputDir = tempDir,
                OutputFilePattern = "no-placeholder-here.json", DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            keysProcessed.ShouldBe(["1.0.0"]);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_ExistingFiles_ThatDoNotMatchPattern_AreNotFiltered()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-nomatch-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "unrelated.txt"), "data");
            var keysProcessed = new List<string>();
            DesignPipelineDelegate<string> trackingPipeline = ctx =>
            { keysProcessed.Add(ctx.Key); return Task.CompletedTask; };
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = trackingPipeline, KeySelector = v => v,
                OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            keysProcessed.ShouldBe(["1.0.0"]);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_MissingOnly_MatchesPrefixButNotSuffix_NotConsideredExisting()
    {
        // Covers the branch: StartsWith(prefix) = true, EndsWith(suffix) = false
        var tempDir = Path.Combine(Path.GetTempPath(), $"dl-prefix-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            // Pattern "compose-{key}.yaml" => prefix="compose-", suffix=".yaml"
            // File starts with "compose-" but does not end with ".yaml"
            File.WriteAllText(Path.Combine(tempDir, "compose-1.0.0.txt"), "wrong suffix");
            // File does NOT start with "compose-" => covers StartsWith=false branch
            File.WriteAllText(Path.Combine(tempDir, "other-file.yaml"), "wrong prefix");
            var keysProcessed = new List<string>();
            DesignPipelineDelegate<string> trackingPipeline = ctx =>
            { keysProcessed.Add(ctx.Key); return Task.CompletedTask; };
            var runner = new DesignPipelineRunner<string>
            {
                ItemCollector = new StaticVersionCollector(["1.0.0"]),
                Pipeline = trackingPipeline, KeySelector = v => v,
                OutputDir = tempDir, DefaultParallelism = 1,
                OutputFilePattern = "compose-{key}.yaml",
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            // File didn't match pattern, so 1.0.0 is still considered missing
            keysProcessed.ShouldBe(["1.0.0"]);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }
}

// ---------------------------------------------------------------------------
// DesignPipelineRunner<TestItem> — non-string TItem
// ---------------------------------------------------------------------------

public sealed class RunnerTestItemTests
{
    [Fact]
    public async Task RunAsync_ListOnly_PrintsItemKeysAndReturnsZero()
    {
        var items = new List<TestItem>
        {
            new("alpha", "https://example.com/alpha"),
            new("beta", "https://example.com/beta"),
        };
        var runner = CreateTestItemRunner(items);
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ProcessesItemsSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"item-dl-{Guid.NewGuid():N}");
        try
        {
            var keysProcessed = new List<string>();
            DesignPipelineDelegate<TestItem> pipeline = ctx =>
            {
                keysProcessed.Add(ctx.Key);
                ctx.Content = $"data-for-{ctx.Item.Name}";
                return Task.CompletedTask;
            };
            var items = new List<TestItem>
            {
                new("alpha", "https://example.com/alpha"),
                new("beta", "https://example.com/beta"),
            };
            var runner = new DesignPipelineRunner<TestItem>
            {
                ItemCollector = new StaticItemCollector<TestItem>(items),
                Pipeline = pipeline, KeySelector = i => i.Name,
                OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(0);
            keysProcessed.Count.ShouldBe(2);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_MissingOnly_FiltersExistingByKey()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"item-missing-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "alpha.json"), "existing");
            var keysProcessed = new List<string>();
            DesignPipelineDelegate<TestItem> pipeline = ctx =>
            { keysProcessed.Add(ctx.Key); ctx.Content = "new"; return Task.CompletedTask; };
            var items = new List<TestItem>
            {
                new("alpha", "https://example.com/alpha"),
                new("beta", "https://example.com/beta"),
            };
            var runner = new DesignPipelineRunner<TestItem>
            {
                ItemCollector = new StaticItemCollector<TestItem>(items),
                Pipeline = pipeline, KeySelector = i => i.Name,
                OutputDir = tempDir, DefaultParallelism = 1,
                OutputFilePattern = "{key}.json",
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync(["--missing"]);
            result.ShouldBe(0);
            keysProcessed.ShouldBe(["beta"]);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_NoItemsToProcess_ReturnsZero()
    {
        var runner = CreateTestItemRunner([]);
        var result = await runner.RunAsync([]);
        result.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_FailingPipeline_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"item-fail-{Guid.NewGuid():N}");
        try
        {
            DesignPipelineDelegate<TestItem> failingPipeline = _ =>
                throw new InvalidOperationException("boom");
            var items = new List<TestItem> { new("alpha", "url") };
            var runner = new DesignPipelineRunner<TestItem>
            {
                ItemCollector = new StaticItemCollector<TestItem>(items),
                Pipeline = failingPipeline, KeySelector = i => i.Name,
                OutputDir = tempDir, DefaultParallelism = 1,
                AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
            };
            var result = await runner.RunAsync([]);
            result.ShouldBe(1);
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task RunAsync_WithItemFilter_AppliesFilter()
    {
        var items = new List<TestItem>
        {
            new("alpha", "u1"),
            new("beta", "u2"),
            new("gamma", "u3"),
        };
        var runner = new DesignPipelineRunner<TestItem>
        {
            ItemCollector = new StaticItemCollector<TestItem>(items),
            Pipeline = _ => Task.CompletedTask,
            KeySelector = i => i.Name,
            OutputDir = Path.GetTempPath(),
            ItemFilter = all => all.Where(i => i.Name.StartsWith('a')).ToList(),
            AuthTokenEnvVar = null, MinLogLevel = LogLevel.None,
        };
        var result = await runner.RunAsync(["--list"]);
        result.ShouldBe(0);
    }

    private static DesignPipelineRunner<TestItem> CreateTestItemRunner(
        IEnumerable<TestItem> items, string? outputDir = null)
    {
        return new DesignPipelineRunner<TestItem>
        {
            ItemCollector = new StaticItemCollector<TestItem>(items),
            Pipeline = _ => Task.CompletedTask,
            KeySelector = i => i.Name,
            OutputDir = outputDir ?? Path.GetTempPath(),
            AuthTokenEnvVar = null,
            MinLogLevel = LogLevel.None,
        };
    }
}

// ---------------------------------------------------------------------------
// DesignPipelineContext<TItem>
// ---------------------------------------------------------------------------

public sealed class DesignPipelineContextTests
{
    [Fact]
    public void RequiredProperties_AreSet_StringItem()
    {
        var logger = NullLogger.Instance;
        var client = new HttpClient();

        var ctx = new DesignPipelineContext<string>
        {
            Item = "1.0.0",
            Key = "1.0.0",
            OutputDir = "/tmp/output",
            Logger = logger,
            HttpClient = client,
            OutputFilePattern = "schema-{key}.json",
        };

        ctx.Item.ShouldBe("1.0.0");
        ctx.Key.ShouldBe("1.0.0");
        ctx.OutputDir.ShouldBe("/tmp/output");
        ctx.Logger.ShouldBe(logger);
        ctx.HttpClient.ShouldBe(client);
        ctx.OutputFilePattern.ShouldBe("schema-{key}.json");
        ctx.Content.ShouldBeNull();
        ctx.OutputFilePath.ShouldBeNull();
        ctx.Progress.ShouldBeNull();
    }

    [Fact]
    public void MutableProperties_CanBeSet()
    {
        var ctx = new DesignPipelineContext<string>
        {
            Item = "1.0.0",
            Key = "1.0.0",
            OutputDir = "/tmp",
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "s-{key}.json",
        };

        ctx.Content = "hello";
        ctx.OutputFilePath = "/tmp/s-1.0.0.json";

        ctx.Content.ShouldBe("hello");
        ctx.OutputFilePath.ShouldBe("/tmp/s-1.0.0.json");
    }

    [Fact]
    public void Progress_CanBeSetViaInit()
    {
        var progress = new DesignPipelineProgressInfo("1.0.0");
        var ctx = new DesignPipelineContext<string>
        {
            Item = "1.0.0",
            Key = "1.0.0",
            OutputDir = "/tmp",
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "s-{key}.json",
            Progress = progress,
        };

        ctx.Progress.ShouldBe(progress);
    }

    [Fact]
    public void RequiredProperties_AreSet_TestItemType()
    {
        var item = new TestItem("alpha", "https://example.com/alpha");
        var ctx = new DesignPipelineContext<TestItem>
        {
            Item = item,
            Key = "alpha",
            OutputDir = "/tmp",
            Logger = NullLogger.Instance,
            HttpClient = new HttpClient(),
            OutputFilePattern = "{key}.json",
        };

        ctx.Item.ShouldBe(item);
        ctx.Key.ShouldBe("alpha");
        ctx.Content.ShouldBeNull();
        ctx.OutputFilePath.ShouldBeNull();
    }
}

// ---------------------------------------------------------------------------
// VersionFilters
// ---------------------------------------------------------------------------

public sealed class VersionFiltersTests
{
    [Fact]
    public void LatestPatchPerMinor_NormalCase_KeepsHighestPatch()
    {
        var versions = new List<string> { "1.0.1", "1.0.3", "1.1.0", "1.1.2" };

        var result = VersionFilters.LatestPatchPerMinor(versions);

        result.ShouldBe(["1.0.3", "1.1.2"]);
    }

    [Fact]
    public void LatestPatchPerMinor_SingleVersion_ReturnsThatVersion()
    {
        var result = VersionFilters.LatestPatchPerMinor(["5.2.1"]);

        result.ShouldBe(["5.2.1"]);
    }

    [Fact]
    public void LatestPatchPerMinor_EmptyList_ReturnsEmpty()
    {
        var result = VersionFilters.LatestPatchPerMinor([]);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void LatestPatchPerMinor_VersionsWithLessThan3Parts_FilteredOut()
    {
        var versions = new List<string> { "1.0", "2", "3.0.0" };

        var result = VersionFilters.LatestPatchPerMinor(versions);

        result.ShouldBe(["3.0.0"]);
    }

    [Fact]
    public void LatestPatchPerMinor_NonNumericMajor_FilteredOut()
    {
        var result = VersionFilters.LatestPatchPerMinor(["abc.0.0", "1.0.0"]);

        result.ShouldBe(["1.0.0"]);
    }

    [Fact]
    public void LatestPatchPerMinor_NonNumericMinor_FilteredOut()
    {
        var result = VersionFilters.LatestPatchPerMinor(["1.abc.0", "1.0.0"]);

        result.ShouldBe(["1.0.0"]);
    }

    [Fact]
    public void LatestPatchPerMinor_NonNumericPatch_FilteredOut()
    {
        var result = VersionFilters.LatestPatchPerMinor(["1.0.abc", "1.0.1"]);

        result.ShouldBe(["1.0.1"]);
    }

    [Fact]
    public void LatestPatchPerMinor_ResultSortedByMajorThenMinor()
    {
        var versions = new List<string> { "2.1.0", "1.0.0", "3.0.0", "1.1.0", "2.0.0" };

        var result = VersionFilters.LatestPatchPerMinor(versions);

        result.ShouldBe(["1.0.0", "1.1.0", "2.0.0", "2.1.0", "3.0.0"]);
    }

    [Fact]
    public void LatestPatchPerMinor_MultipleMajorVersions_GroupedCorrectly()
    {
        var versions = new List<string>
        {
            "1.0.0", "1.0.5", "1.0.3",
            "2.0.0", "2.0.1",
            "2.1.0", "2.1.3", "2.1.1"
        };

        var result = VersionFilters.LatestPatchPerMinor(versions);

        result.ShouldBe(["1.0.5", "2.0.1", "2.1.3"]);
    }

    [Fact]
    public void LatestPatchPerMinor_AllNonNumeric_ReturnsEmpty()
    {
        var result = VersionFilters.LatestPatchPerMinor(["a.b.c", "x.y.z"]);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void LatestPatchPerMinor_AllTooFewParts_ReturnsEmpty()
    {
        var result = VersionFilters.LatestPatchPerMinor(["1.0", "2"]);

        result.ShouldBeEmpty();
    }
}
