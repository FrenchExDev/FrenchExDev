using Shouldly;

namespace FrenchExDev.Net.Traefik.Bundle.Tests;

/// <summary>
/// Round-trips a hand-written, realistic Traefik dynamic config (HTTP + TCP
/// routers, multiple middleware types, weighted load balancing, TLS) through
/// the typed model and back. Catches edge cases the synthetic minimal
/// fixtures don't reach.
/// </summary>
public sealed class RealisticRoundTripTests
{
    private const string SamplePath = "Samples/realistic-dynamic.yaml";

    [Fact]
    public void DeserializeDynamic_RealisticSample_ParsesCoreShape()
    {
        var yaml = File.ReadAllText(SamplePath);
        var config = TraefikSerializer.DeserializeDynamic(yaml);

        config.ShouldNotBeNull();
        config.Http.ShouldNotBeNull();
        config.Http.Routers.ShouldNotBeNull();
        config.Http.Routers.ShouldContainKey("api-router");
        config.Http.Routers.ShouldContainKey("web-router");
        config.Http.Services.ShouldNotBeNull();
        config.Http.Services.ShouldContainKey("api-backend");
        config.Http.Middlewares.ShouldNotBeNull();
        config.Http.Middlewares.ShouldContainKey("api-auth");

        config.Tcp.ShouldNotBeNull();
        config.Tcp.Routers.ShouldNotBeNull();
        config.Tcp.Routers.ShouldContainKey("postgres-router");
    }

    [Fact]
    public void DeserializeDynamic_RealisticSample_PreservesRouterMiddlewareChain()
    {
        var yaml = File.ReadAllText(SamplePath);
        var config = TraefikSerializer.DeserializeDynamic(yaml);

        var apiRouter = config.Http!.Routers!["api-router"];
        apiRouter.Middlewares.ShouldNotBeNull();
        apiRouter.Middlewares.Count.ShouldBe(3);
        apiRouter.Middlewares.ShouldContain("api-auth");
        apiRouter.Middlewares.ShouldContain("api-strip-prefix");
        apiRouter.Middlewares.ShouldContain("api-rate-limit");
    }

    [Fact]
    public void DeserializeDynamic_RealisticSample_PreservesDiscriminatedMiddlewareBranches()
    {
        var yaml = File.ReadAllText(SamplePath);
        var config = TraefikSerializer.DeserializeDynamic(yaml);

        var auth = config.Http!.Middlewares!["api-auth"];
        auth.BasicAuth.ShouldNotBeNull();
        auth.StripPrefix.ShouldBeNull();

        var stripPrefix = config.Http.Middlewares["api-strip-prefix"];
        stripPrefix.StripPrefix.ShouldNotBeNull();
        stripPrefix.BasicAuth.ShouldBeNull();
    }

    [Fact]
    public void RoundTrip_RealisticSample_StableShape()
    {
        var yaml = File.ReadAllText(SamplePath);
        var first = TraefikSerializer.DeserializeDynamic(yaml);
        var serialized = TraefikSerializer.Serialize(first);
        var second = TraefikSerializer.DeserializeDynamic(serialized);

        // Structural checks (we can't compare the whole DOM cheaply because
        // YAML→object→YAML re-orders keys; but we can pin the parts we care
        // about).
        second.Http!.Routers!.Keys.ShouldBe(first.Http!.Routers!.Keys, ignoreOrder: true);
        second.Http.Services!.Keys.ShouldBe(first.Http.Services!.Keys, ignoreOrder: true);
        second.Http.Middlewares!.Keys.ShouldBe(first.Http.Middlewares!.Keys, ignoreOrder: true);
        second.Tcp!.Routers!.Keys.ShouldBe(first.Tcp!.Routers!.Keys, ignoreOrder: true);
    }
}
