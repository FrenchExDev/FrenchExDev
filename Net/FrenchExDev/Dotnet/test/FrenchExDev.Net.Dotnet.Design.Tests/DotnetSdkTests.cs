using System.Net;
using FrenchExDev.Net.Dotnet.Design;
using FrenchExDev.Net.Wrapper.Versioning;
using Shouldly;

namespace FrenchExDev.Net.Dotnet.Design.Tests;

public sealed class DotnetSdkTests
{
    [Theory]
    [InlineData("v8.0.100", "8.0.100")]
    [InlineData("10.0.401", "10.0.401")]
    [InlineData("v11.0.0", null)]
    [InlineData("v8.0.12", null)]
    [InlineData("v11.0.100-rc.1.26425.128", null)]
    [InlineData("release/8.0.100", null)]
    [InlineData("8.0.100; echo invalid", null)]
    public void Tags_SelectStableSdkFeatureBands(string tag, string? expected) =>
        DotnetSdk.VersionFromTag(tag).ShouldBe(expected);

    [Fact]
    public async Task GitHubProvider_UsesSdkTagsAndExcludesRuntimeAndPrereleaseTags()
    {
        using var handler = new TagHandler();
        using var client = new HttpClient(handler);
        var collector = new GitHubTagsVersionCollector("dotnet", "sdk", client, DotnetSdk.VersionFromTag);
        var versions = await collector.CollectVersionsAsync();
        versions.ShouldBe(new[] { "8.0.100", "10.0.401" });
        handler.RequestUri.ShouldBe("https://api.github.com/repos/dotnet/sdk/tags?per_page=100");
    }

    [Theory]
    [InlineData("v8.0.100")]
    [InlineData("8.0.100; touch /tmp/unexpected")]
    public void Installer_RejectsNonCanonicalVersionBeforeProducingShellCode(string version) =>
        Should.Throw<ArgumentException>(() => DotnetSdk.InstallScript(version));

    private sealed class TagHandler : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    [{"name":"v8.0.100"},{"name":"v11.0.0"},{"name":"v11.0.100-rc.1.26425.128"},{"name":"v10.0.401"}]
                    """),
            });
        }
    }
}
