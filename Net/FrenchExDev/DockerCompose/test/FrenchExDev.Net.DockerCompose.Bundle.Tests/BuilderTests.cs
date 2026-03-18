using FrenchExDev.Net.DockerCompose.Bundle.Model;
using Shouldly;

namespace FrenchExDev.Net.DockerCompose.Bundle.Tests;

public class BuilderTests
{
    [Fact]
    public async Task ComposeFileBuilder_BuildAsync_CreatesFile()
    {
        var result = await new ComposeFileBuilder()
            .WithName("test-app")
            .WithServices(new Dictionary<string, Service>
            {
                ["web"] = new Service { Image = "nginx:latest" }
            })
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var file = result.ValueOrThrow().Resolved();
        file.Name.ShouldBe("test-app");
        file.Services.ShouldContainKey("web");
        file.Services["web"].Image.ShouldBe("nginx:latest");
    }

    [Fact]
    public async Task ServiceBuilder_BuildAsync_CreatesService()
    {
        var result = await new ServiceBuilder()
            .WithImage("postgres:16")
            .WithRestart("unless-stopped")
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var svc = result.ValueOrThrow().Resolved();
        svc.Image.ShouldBe("postgres:16");
        svc.Restart.ShouldBe("unless-stopped");
    }

    [Fact]
    public async Task NetworkBuilder_BuildAsync_CreatesNetwork()
    {
        var result = await new NetworkBuilder()
            .WithName("frontend")
            .WithDriver("bridge")
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var net = result.ValueOrThrow().Resolved();
        net.Name.ShouldBe("frontend");
        net.Driver.ShouldBe("bridge");
    }

    [Fact]
    public async Task VolumeBuilder_BuildAsync_CreatesVolume()
    {
        var result = await new VolumeBuilder()
            .WithName("data")
            .WithDriver("local")
            .BuildAsync();

        result.IsSuccess.ShouldBeTrue();
        var vol = result.ValueOrThrow().Resolved();
        vol.Name.ShouldBe("data");
        vol.Driver.ShouldBe("local");
    }
}
