using Shouldly;

namespace FrenchExDev.Net.DockerCompose.Bundle.Tests;

public class BuilderTests
{
    [Fact]
    public async Task ComposeFileBuilder_BuildAsync_CreatesFile()
    {
        var result = await new ComposeFileBuilder()
            .WithName("test-app")
            .WithServices(new Dictionary<string, ComposeService>
            {
                ["web"] = new ComposeService { Image = "nginx:latest" }
            })
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var file = result.ValueOrThrow().Resolved();
        file.Name.ShouldBe("test-app");
        file.Services.ShouldNotBeNull();
        file.Services!.ShouldContainKey("web");
        file.Services["web"].Image.ShouldBe("nginx:latest");
    }

    [Fact]
    public async Task ComposeServiceBuilder_BuildAsync_CreatesService()
    {
        var result = await new ComposeServiceBuilder()
            .WithImage("postgres:16")
            .WithRestart("unless-stopped")
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var svc = result.ValueOrThrow().Resolved();
        svc.Image.ShouldBe("postgres:16");
        svc.Restart.ShouldBe("unless-stopped");
    }

    [Fact]
    public async Task ComposeNetworkBuilder_BuildAsync_CreatesNetwork()
    {
        var result = await new ComposeNetworkBuilder()
            .WithName("frontend")
            .WithDriver("bridge")
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var net = result.ValueOrThrow().Resolved();
        net.Name.ShouldBe("frontend");
        net.Driver.ShouldBe("bridge");
    }

    [Fact]
    public async Task ComposeVolumeBuilder_BuildAsync_CreatesVolume()
    {
        var result = await new ComposeVolumeBuilder()
            .WithName("data")
            .WithDriver("local")
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var vol = result.ValueOrThrow().Resolved();
        vol.Name.ShouldBe("data");
        vol.Driver.ShouldBe("local");
    }

    [Fact]
    public async Task ComposeFileBuilder_WithNestedBuilders_CreatesFile()
    {
        var result = await new ComposeFileBuilder()
            .WithName("nested-app")
            .WithService("web", svc => svc
                .WithImage("nginx:latest")
                .WithRestart("always"))
            .WithNetwork("frontend", net => net
                .WithDriver("bridge"))
            .WithVolume("data", vol => vol
                .WithDriver("local"))
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var file = result.ValueOrThrow().Resolved();
        file.Name.ShouldBe("nested-app");
        file.Services.ShouldNotBeNull();
        file.Services!["web"].Image.ShouldBe("nginx:latest");
        file.Networks.ShouldNotBeNull();
        file.Networks!["frontend"]!.Driver.ShouldBe("bridge");
        file.Volumes.ShouldNotBeNull();
        file.Volumes!["data"]!.Driver.ShouldBe("local");
    }

    [Fact]
    public async Task ComposeSecretBuilder_BuildAsync_CreatesSecret()
    {
        var result = await new ComposeSecretBuilder()
            .WithName("db_password")
            .WithFile("./secret.txt")
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var secret = result.ValueOrThrow().Resolved();
        secret.Name.ShouldBe("db_password");
        secret.File.ShouldBe("./secret.txt");
    }

    [Fact]
    public async Task ComposeConfigBuilder_BuildAsync_CreatesConfig()
    {
        var result = await new ComposeConfigBuilder()
            .WithName("app_config")
            .WithFile("./config.yaml")
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var config = result.ValueOrThrow().Resolved();
        config.Name.ShouldBe("app_config");
        config.File.ShouldBe("./config.yaml");
    }
}
