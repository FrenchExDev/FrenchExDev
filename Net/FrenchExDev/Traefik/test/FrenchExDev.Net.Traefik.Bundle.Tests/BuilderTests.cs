using Shouldly;

namespace FrenchExDev.Net.Traefik.Bundle.Tests;

public class BuilderTests
{
    [Fact]
    public async Task TraefikStaticConfigBuilder_BasicBuild()
    {
        var result = await new TraefikStaticConfigBuilder()
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task TraefikStaticConfigBuilder_WithEntryPoints()
    {
        var result = await new TraefikStaticConfigBuilder()
            .WithEntryPoints(new Dictionary<string, TraefikStaticEntryPoint>
            {
                ["web"] = new TraefikStaticEntryPoint { Address = ":80" }
            })
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var config = result.ValueOrThrow().Resolved();
        config.EntryPoints.ShouldNotBeNull();
        config.EntryPoints["web"].Address.ShouldBe(":80");
    }

    [Fact]
    public async Task TraefikDynamicConfigBuilder_BasicBuild()
    {
        var result = await new TraefikDynamicConfigBuilder()
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task TraefikHttpRouterBuilder_Build()
    {
        var result = await new TraefikHttpRouterBuilder()
            .WithRule("Host(`example.com`)")
            .WithService("my-service")
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var router = result.ValueOrThrow().Resolved();
        router.Rule.ShouldBe("Host(`example.com`)");
        router.Service.ShouldBe("my-service");
    }

    [Fact]
    public async Task TraefikHttpMiddlewareBuilder_WithAddPrefix()
    {
        var result = await new TraefikHttpMiddlewareBuilder()
            .WithAddPrefix(new TraefikAddPrefixMiddleware { Prefix = "/api" })
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var mw = result.ValueOrThrow().Resolved();
        mw.AddPrefix.ShouldNotBeNull();
        mw.AddPrefix.Prefix.ShouldBe("/api");
    }

    [Fact]
    public async Task TraefikStaticEntryPointBuilder_Build()
    {
        var result = await new TraefikStaticEntryPointBuilder()
            .WithAddress(":443")
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var ep = result.ValueOrThrow().Resolved();
        ep.Address.ShouldBe(":443");
    }
}
