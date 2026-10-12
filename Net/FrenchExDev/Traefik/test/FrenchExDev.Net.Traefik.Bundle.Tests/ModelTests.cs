using Shouldly;

namespace FrenchExDev.Net.Traefik.Bundle.Tests;

public class ModelTests
{
    [Fact]
    public void TraefikStaticConfig_CanBeInstantiated()
    {
        var config = new TraefikStaticConfig();
        config.ShouldNotBeNull();
        config.EntryPoints.ShouldBeNull();
        config.Providers.ShouldBeNull();
    }

    [Fact]
    public void TraefikStaticConfig_EntryPoints_CanBePopulated()
    {
        var config = new TraefikStaticConfig
        {
            EntryPoints = new Dictionary<string, TraefikStaticEntryPoint>
            {
                ["web"] = new TraefikStaticEntryPoint { Address = ":80" },
                ["websecure"] = new TraefikStaticEntryPoint { Address = ":443" },
            }
        };

        config.EntryPoints.ShouldNotBeNull();
        config.EntryPoints.Count.ShouldBe(2);
        config.EntryPoints["web"].Address.ShouldBe(":80");
    }

    [Fact]
    public void TraefikDynamicConfig_CanBeInstantiated()
    {
        var config = new TraefikDynamicConfig();
        config.ShouldNotBeNull();
        config.Http.ShouldBeNull();
        config.Tcp.ShouldBeNull();
        config.Udp.ShouldBeNull();
        config.Tls.ShouldBeNull();
    }

    [Fact]
    public void TraefikDynamicConfig_Http_CanBePopulated()
    {
        var config = new TraefikDynamicConfig
        {
            Http = new TraefikDynamicHttp
            {
                Routers = new Dictionary<string, TraefikHttpRouter>
                {
                    ["my-router"] = new TraefikHttpRouter
                    {
                        Rule = "Host(`example.com`)",
                        Service = "my-service",
                    }
                }
            }
        };

        config.Http.ShouldNotBeNull();
        config.Http.Routers.ShouldNotBeNull();
        config.Http.Routers["my-router"].Rule.ShouldBe("Host(`example.com`)");
    }

    [Fact]
    public void TraefikHttpMiddleware_DiscriminatedUnion_Works()
    {
        var mw = new TraefikHttpMiddleware
        {
            AddPrefix = new TraefikAddPrefixMiddleware { Prefix = "/api" }
        };

        mw.AddPrefix.ShouldNotBeNull();
        mw.AddPrefix.Prefix.ShouldBe("/api");
        mw.BasicAuth.ShouldBeNull();
    }

    [Fact]
    public void TraefikHttpService_DiscriminatedUnion_Works()
    {
        var svc = new TraefikHttpService
        {
            LoadBalancer = new TraefikHttpLoadBalancerService()
        };

        svc.LoadBalancer.ShouldNotBeNull();
        svc.Weighted.ShouldBeNull();
    }
}
