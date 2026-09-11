using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.VosFile;

namespace FrenchExDev.Net.Vos.VosFile.Tests;

public class VosFileWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"voswrite-{Guid.NewGuid():N}");
    private readonly VosFileWriter _writer = new();
    private readonly VosFileReader _reader = new();

    public VosFileWriterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }

    [Fact]
    public async Task WriteAsync_creates_yaml_file_that_can_be_read_back()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["alpine"] = new VosMachineType { Box = "alpine/3.21" } }
        };
        var path = Path.Combine(_tempDir, "config-vos.yaml");

        await _writer.WriteAsync(config, path);

        File.Exists(path).ShouldBeTrue();
        var readBack = await _reader.ReadAsync(path);
        readBack.IsSuccess.ShouldBeTrue();
        readBack.Value!.MachineTypes["alpine"].Box.ShouldBe("alpine/3.21");
    }

    [Fact]
    public async Task WriteAsync_omits_null_values()
    {
        var config = new VosConfig();
        var path = Path.Combine(_tempDir, "config-vos.yaml");

        await _writer.WriteAsync(config, path);

        var content = await File.ReadAllTextAsync(path);
        content.ShouldNotContain("null");
    }

    [Fact]
    public async Task WriteLocalAsync_creates_local_directory_and_gitignore()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var localOverrides = new VosConfig
        {
            MachineTypes = new() { ["docker"] = new VosMachineType { Box = "ubuntu/jammy64" } }
        };

        await _writer.WriteLocalAsync(localOverrides, configPath);

        var localDir = Path.Combine(_tempDir, "local");
        Directory.Exists(localDir).ShouldBeTrue();
        File.Exists(Path.Combine(localDir, ".gitignore")).ShouldBeTrue();
        File.Exists(Path.Combine(localDir, "config-vos-local.yaml")).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteLocalAsync_gitignore_contains_star()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        await _writer.WriteLocalAsync(new VosConfig(), configPath);

        var gitignore = await File.ReadAllTextAsync(Path.Combine(_tempDir, "local", ".gitignore"));
        gitignore.ShouldContain("*");
    }

    [Fact]
    public async Task WriteAsync_roundtrips_with_machines_and_instances()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["alpine"] = new VosMachineType { Box = "alpine/3.21", Provider = new VosProviderConfig { Memory = 4096, Cpus = 4 } } },
            Machines = new() { ["web"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "web-01", Ip = "10.0.0.1" }] } }
        };
        var path = Path.Combine(_tempDir, "config-vos.yaml");

        await _writer.WriteAsync(config, path);
        var readBack = await _reader.ReadAsync(path);

        readBack.Value!.Machines["web"].Instances[0].Name.ShouldBe("web-01");
        readBack.Value!.Machines["web"].Instances[0].Ip.ShouldBe("10.0.0.1");
        readBack.Value!.MachineTypes["alpine"].Provider!.Memory.ShouldBe(4096);
    }

    [Fact]
    public async Task WriteLocalAsync_does_not_recreate_gitignore_when_local_dir_already_exists()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var localDir = Path.Combine(_tempDir, "local");
        Directory.CreateDirectory(localDir);
        var gitignorePath = Path.Combine(localDir, ".gitignore");
        await File.WriteAllTextAsync(gitignorePath, "custom-content\n");

        await _writer.WriteLocalAsync(new VosConfig(), configPath);

        // The gitignore should NOT have been overwritten because the local dir already existed
        var gitignore = await File.ReadAllTextAsync(gitignorePath);
        gitignore.ShouldContain("custom-content");
    }

    [Fact]
    public async Task WriteLocalAsync_overwrites_existing_local_config()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");

        await _writer.WriteLocalAsync(new VosConfig { Backend = "vagrant" }, configPath);
        await _writer.WriteLocalAsync(new VosConfig { Backend = "docker" }, configPath);

        var localPath = Path.Combine(_tempDir, "local", "config-vos-local.yaml");
        var readBack = await _reader.ReadAsync(localPath);
        readBack.IsSuccess.ShouldBeTrue();
        readBack.Value!.Backend.ShouldBe("docker");
    }
}
