using FrenchExDev.Net.Vos.Bundle;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Bundle.Tests;

public class VosBundleWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"vosbundle-{Guid.NewGuid():N}");
    private readonly VosBundleWriter _writer = new();

    public void Dispose() { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }

    [Fact]
    public async Task WriteAsync_creates_config_yaml()
    {
        var bundle = new VosBundle();
        bundle.Config.MachineTypes["alpine"] = new VosMachineType { Box = "alpine/3.21" };

        await _writer.WriteAsync(bundle, _tempDir);

        File.Exists(Path.Combine(_tempDir, "config-vos.yaml")).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "config-vos.yaml"));
        content.ShouldContain("alpine");
    }

    [Fact]
    public async Task WriteAsync_creates_vagrantfile()
    {
        await _writer.WriteAsync(new VosBundle(), _tempDir);
        File.Exists(Path.Combine(_tempDir, "Vagrantfile")).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_materializes_provisioning_scripts()
    {
        var bundle = new VosBundle();
        bundle.AddProvisioningScript("install-docker", "1.0", "#!/bin/sh\napk add docker");

        await _writer.WriteAsync(bundle, _tempDir);

        var scriptPath = Path.Combine(_tempDir, "provisioning", "1.0", "install-docker.sh");
        File.Exists(scriptPath).ShouldBeTrue();
        (await File.ReadAllTextAsync(scriptPath)).ShouldContain("apk add docker");
    }

    [Fact]
    public async Task WriteAsync_materializes_shared_files()
    {
        var bundle = new VosBundle();
        bundle.AddFile("files", "daemon.json", "json", "{}");

        await _writer.WriteAsync(bundle, _tempDir);

        File.Exists(Path.Combine(_tempDir, "files", "daemon.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_creates_local_override_when_set()
    {
        var bundle = new VosBundle
        {
            LocalOverrides = new VosConfig { Backend = "podman" }
        };

        await _writer.WriteAsync(bundle, _tempDir);

        File.Exists(Path.Combine(_tempDir, "local", "config-vos-local.yaml")).ShouldBeTrue();
        File.Exists(Path.Combine(_tempDir, "local", ".gitignore")).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_does_not_create_local_when_not_set()
    {
        await _writer.WriteAsync(new VosBundle(), _tempDir);
        Directory.Exists(Path.Combine(_tempDir, "local")).ShouldBeFalse();
    }
}
