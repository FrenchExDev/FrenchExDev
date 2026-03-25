using Shouldly;

namespace FrenchExDev.Net.Traefik.Bundle.Tests;

public class SerializerTests
{
    [Fact]
    public void DeserializeStatic_MinimalYaml()
    {
        var yaml = File.ReadAllText("Fixtures/static-minimal.yaml");
        var config = TraefikSerializer.DeserializeStatic(yaml);

        config.ShouldNotBeNull();
        config.EntryPoints.ShouldNotBeNull();
        config.EntryPoints.Count.ShouldBe(2);
        config.EntryPoints["web"].Address.ShouldBe(":80");
        config.EntryPoints["websecure"].Address.ShouldBe(":443");
    }

    [Fact]
    public void DeserializeStatic_ApiSection()
    {
        var yaml = File.ReadAllText("Fixtures/static-minimal.yaml");
        var config = TraefikSerializer.DeserializeStatic(yaml);

        config.Api.ShouldNotBeNull();
        config.Api.Dashboard.ShouldBe(true);
        config.Api.Insecure.ShouldBe(true);
    }

    [Fact]
    public void DeserializeStatic_ProvidersSection()
    {
        var yaml = File.ReadAllText("Fixtures/static-minimal.yaml");
        var config = TraefikSerializer.DeserializeStatic(yaml);

        config.Providers.ShouldNotBeNull();
        config.Providers.File.ShouldNotBeNull();
        config.Providers.File.Directory.ShouldBe("/etc/traefik/dynamic");
        config.Providers.File.Watch.ShouldBe(true);
    }

    [Fact]
    public void DeserializeDynamic_MinimalYaml()
    {
        var yaml = File.ReadAllText("Fixtures/dynamic-minimal.yaml");
        var config = TraefikSerializer.DeserializeDynamic(yaml);

        config.ShouldNotBeNull();
        config.Http.ShouldNotBeNull();
    }

    [Fact]
    public void DeserializeDynamic_HttpRouters()
    {
        var yaml = File.ReadAllText("Fixtures/dynamic-minimal.yaml");
        var config = TraefikSerializer.DeserializeDynamic(yaml);

        config.Http!.Routers.ShouldNotBeNull();
        config.Http.Routers.ShouldContainKey("my-router");

        var router = config.Http.Routers["my-router"];
        router.Rule.ShouldBe("Host(`example.com`)");
        router.Service.ShouldBe("my-service");
        router.EntryPoints.ShouldNotBeNull();
        router.EntryPoints.ShouldContain("websecure");
    }

    [Fact]
    public void Serialize_StaticConfig_ProducesCamelCaseYaml()
    {
        var config = new TraefikStaticConfig
        {
            EntryPoints = new Dictionary<string, TraefikStaticEntryPoint>
            {
                ["web"] = new TraefikStaticEntryPoint { Address = ":80" }
            },
            Api = new TraefikStaticAPI { Dashboard = true }
        };

        var yaml = TraefikSerializer.Serialize(config);

        yaml.ShouldContain("entryPoints:");
        yaml.ShouldContain("address: :80");
        yaml.ShouldContain("api:");
        yaml.ShouldContain("dashboard: true");
    }

    [Fact]
    public void Serialize_OmitsNullProperties()
    {
        var config = new TraefikStaticConfig
        {
            Api = new TraefikStaticAPI { Dashboard = true }
        };

        var yaml = TraefikSerializer.Serialize(config);

        yaml.ShouldNotContain("entryPoints:");
        yaml.ShouldNotContain("providers:");
        yaml.ShouldContain("dashboard: true");
    }

    [Fact]
    public void RoundTrip_StaticConfig()
    {
        var original = new TraefikStaticConfig
        {
            EntryPoints = new Dictionary<string, TraefikStaticEntryPoint>
            {
                ["web"] = new TraefikStaticEntryPoint { Address = ":80" },
                ["websecure"] = new TraefikStaticEntryPoint { Address = ":443" },
            },
            Api = new TraefikStaticAPI { Dashboard = true, Insecure = true }
        };

        var yaml = TraefikSerializer.Serialize(original);
        var deserialized = TraefikSerializer.DeserializeStatic(yaml);

        deserialized.EntryPoints.ShouldNotBeNull();
        deserialized.EntryPoints.Count.ShouldBe(2);
        deserialized.EntryPoints["web"].Address.ShouldBe(":80");
        deserialized.EntryPoints["websecure"].Address.ShouldBe(":443");
        deserialized.Api.ShouldNotBeNull();
        deserialized.Api.Dashboard.ShouldBe(true);
    }

    [Fact]
    public void RoundTrip_DynamicConfig()
    {
        var original = new TraefikDynamicConfig
        {
            Http = new TraefikDynamicHttp
            {
                Routers = new Dictionary<string, TraefikHttpRouter>
                {
                    ["app"] = new TraefikHttpRouter
                    {
                        Rule = "Host(`app.local`)",
                        Service = "app-svc",
                        EntryPoints = new List<string> { "web" }
                    }
                }
            }
        };

        var yaml = TraefikSerializer.Serialize(original);
        var deserialized = TraefikSerializer.DeserializeDynamic(yaml);

        deserialized.Http.ShouldNotBeNull();
        deserialized.Http.Routers.ShouldNotBeNull();
        deserialized.Http.Routers["app"].Rule.ShouldBe("Host(`app.local`)");
        deserialized.Http.Routers["app"].Service.ShouldBe("app-svc");
    }
}
