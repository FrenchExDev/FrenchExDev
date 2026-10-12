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

    // ── Discriminated union: exactly-one-branch invariant ──────────────

    [Fact]
    public async Task TraefikHttpMiddlewareBuilder_NoBranchSet_Fails()
    {
        var result = await new TraefikHttpMiddlewareBuilder().BuildAsync();

        result.IsSuccess.ShouldBeFalse();
        var msg = result.ValidationResult!.ErrorMessage ?? "";
        msg.ShouldContain("exactly one branch");
        msg.ShouldContain("found 0");
    }

    [Fact]
    public async Task TraefikHttpMiddlewareBuilder_TwoBranchesSet_Fails()
    {
        var result = await new TraefikHttpMiddlewareBuilder()
            .WithAddPrefix(new TraefikAddPrefixMiddleware { Prefix = "/api" })
            .WithBasicAuth(new TraefikBasicAuthMiddleware { Realm = "secure" })
            .BuildAsync();

        result.IsSuccess.ShouldBeFalse();
        var msg = result.ValidationResult!.ErrorMessage ?? "";
        msg.ShouldContain("exactly one branch");
        msg.ShouldContain("found 2");
    }

    // ── Multi-version pipeline (P3.2) ─────────────────────────────────

    [Fact]
    public void TraefikSchemaVersions_ContainsBothLoadedVersions()
    {
        TraefikSchemaVersions.Available.ShouldContain("3");
        TraefikSchemaVersions.Available.ShouldContain("3.1");
        TraefikSchemaVersions.Latest.ShouldBe("3.1");
        TraefikSchemaVersions.Oldest.ShouldBe("3");
    }

    [Fact]
    public void HttpRouter_Observability_HasSinceVersionAttribute()
    {
        // The synthetic v3.1 schema added httpRouter.observability. The merge
        // stage should stamp it with [SinceVersion("3.1")] while leaving v3
        // properties unmarked.
        var observabilityProp = typeof(TraefikHttpRouter).GetProperty("Observability");
        observabilityProp.ShouldNotBeNull();
        var since = observabilityProp.GetCustomAttributes(typeof(SinceVersionAttribute), false);
        since.Length.ShouldBe(1);
        ((SinceVersionAttribute)since[0]).Version.ShouldBe("3.1");

        var ruleProp = typeof(TraefikHttpRouter).GetProperty("Rule");
        ruleProp.ShouldNotBeNull();
        ruleProp.GetCustomAttributes(typeof(SinceVersionAttribute), false).ShouldBeEmpty();
    }

    [Fact]
    public async Task TraefikHttpMiddlewareBuilder_ExactlyOneBranchSet_Succeeds()
    {
        var result = await new TraefikHttpMiddlewareBuilder()
            .WithStripPrefix(new TraefikStripPrefixMiddleware())
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var mw = result.ValueOrThrow().Resolved();
        mw.StripPrefix.ShouldNotBeNull();
        mw.AddPrefix.ShouldBeNull();
    }
}
