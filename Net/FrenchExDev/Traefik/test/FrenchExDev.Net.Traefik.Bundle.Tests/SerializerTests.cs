using Shouldly;

namespace FrenchExDev.Net.Traefik.Bundle.Tests;

public class SerializerTests
{
    // ── JSON output ──────────────────────────────────────────────────────

    [Fact]
    public void Json_RoundTrip_PreservesShape()
    {
        var cfg = new TraefikStaticConfig
        {
            EntryPoints = new Dictionary<string, TraefikStaticEntryPoint>
            {
                ["web"] = new() { Address = ":80" },
                ["websecure"] = new() { Address = ":443" }
            }
        };

        var json = TraefikSerializer.SerializeJson(cfg);
        json.ShouldContain("\"entryPoints\"");
        json.ShouldContain("\":80\"");

        var roundtripped = TraefikSerializer.DeserializeJson<TraefikStaticConfig>(json);
        roundtripped.EntryPoints.ShouldNotBeNull();
        roundtripped.EntryPoints["web"].Address.ShouldBe(":80");
        roundtripped.EntryPoints["websecure"].Address.ShouldBe(":443");
    }

    // ── Schema-validating Try* API ────────────────────────────────────────

    [Fact]
    public void TryDeserializeStatic_MinimalYaml_Succeeds()
    {
        var yaml = File.ReadAllText("Fixtures/static-minimal.yaml");
        var result = TraefikSerializer.TryDeserializeStatic(yaml);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.ValidationResult?.ErrorMessage : null);
        result.Value.ShouldNotBeNull();
        result.Value.EntryPoints.ShouldNotBeNull();
    }

    [Fact]
    public void TryDeserializeStatic_GarbageYaml_Fails()
    {
        var result = TraefikSerializer.TryDeserializeStatic("entryPoints: not-an-object");

        result.IsSuccess.ShouldBeFalse();
        result.ValidationResult.ShouldNotBeNull();
    }

    [Fact]
    public void TryDeserializeStatic_TypoInKey_Fails()
    {
        // 'dashbaord' is a typo for 'dashboard'. With IgnoreUnmatchedProperties
        // the typed deserializer would silently drop it. The schema-first
        // validation path catches it because the embedded schema sets
        // additionalProperties: false on the api section.
        const string yaml = """
            api:
              dashbaord: true
              insecure: true
            """;

        var result = TraefikSerializer.TryDeserializeStatic(yaml);

        result.IsSuccess.ShouldBeFalse();
        var msg = result.ValidationResult!.ErrorMessage ?? "";
        msg.ShouldContain("dashbaord");
    }

    [Fact]
    public void TryDeserializeStatic_WrongType_Fails()
    {
        // The schema requires api.dashboard: bool. Passing a string here
        // would silently round-trip as null in the typed deserializer
        // (because YamlDotNet can't coerce). With YAML→JsonNode→schema
        // we see a real schema error.
        const string yaml = """
            api:
              dashboard: "not-a-bool"
            """;

        var result = TraefikSerializer.TryDeserializeStatic(yaml);

        result.IsSuccess.ShouldBeFalse();
        result.ValidationResult.ShouldNotBeNull();
    }

    [Fact]
    public void TryDeserializeStatic_BoolPrimitive_Survives()
    {
        // Sanity: primitive types must round-trip through the YamlToJson
        // converter so the schema sees real bools, not strings.
        const string yaml = """
            api:
              dashboard: true
              insecure: false
            """;

        var result = TraefikSerializer.TryDeserializeStatic(yaml);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.ValidationResult?.ErrorMessage : null);
        result.Value!.Api!.Dashboard.ShouldBe(true);
        result.Value.Api.Insecure.ShouldBe(false);
    }

    // ── Async / file I/O ───────────────────────────────────────────────────

    [Fact]
    public async Task ReadStaticFromFileAsync_MinimalFixture_Succeeds()
    {
        var result = await TraefikSerializer.ReadStaticFromFileAsync("Fixtures/static-minimal.yaml");

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.ValidationResult?.ErrorMessage : null);
        result.Value!.EntryPoints.ShouldNotBeNull();
    }

    [Fact]
    public async Task WriteStaticToFileAsync_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"traefik-{Guid.NewGuid():N}.yaml");
        try
        {
            var cfg = new TraefikStaticConfig
            {
                EntryPoints = new Dictionary<string, TraefikStaticEntryPoint>
                {
                    ["web"] = new() { Address = ":80" }
                }
            };

            var write = await TraefikSerializer.WriteStaticToFileAsync(path, cfg);
            write.IsSuccess.ShouldBeTrue();
            File.Exists(path).ShouldBeTrue();
            File.Exists(path + ".tmp").ShouldBeFalse();

            var read = await TraefikSerializer.ReadStaticFromFileAsync(path);
            read.IsSuccess.ShouldBeTrue();
            read.Value!.EntryPoints!["web"].Address.ShouldBe(":80");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void TrySerializeStatic_ValidConfig_Succeeds()
    {
        var cfg = new TraefikStaticConfig
        {
            EntryPoints = new Dictionary<string, TraefikStaticEntryPoint>
            {
                ["web"] = new TraefikStaticEntryPoint { Address = ":80" }
            }
        };

        var result = TraefikSerializer.TrySerializeStatic(cfg);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.ValidationResult?.ErrorMessage : null);
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("entryPoints");
    }

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
