using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.VosFile;

namespace FrenchExDev.Net.Vos.VosFile.Tests;

public class VosFileReaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"vosfile-{Guid.NewGuid():N}");
    private readonly VosFileReader _reader = new();

    public VosFileReaderTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }

    private string WriteTempYaml(string fileName, string yaml)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, yaml);
        return path;
    }

    [Fact]
    public async Task ReadAsync_returns_failure_when_config_file_does_not_exist()
    {
        var result = await _reader.ReadAsync(Path.Combine(_tempDir, "nope.yaml"));
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadAsync_deserializes_valid_yaml_with_machine_types_and_machines()
    {
        var path = WriteTempYaml("config-vos.yaml", """
            machine_types:
              alpine:
                box: alpine/3.21
            machines:
              main:
                machine_type_name: alpine
                instances:
                  - name: main-01
            """);

        var result = await _reader.ReadAsync(path);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.MachineTypes.ShouldContainKey("alpine");
        result.Value!.Machines.ShouldContainKey("main");
        result.Value!.Machines["main"].Instances.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ReadAsync_returns_empty_config_for_empty_yaml()
    {
        var path = WriteTempYaml("config-vos.yaml", "");
        var result = await _reader.ReadAsync(path);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.MachineTypes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadLayersAsync_returns_base_only_when_no_local_override()
    {
        var path = WriteTempYaml("config-vos.yaml", """
            machine_types:
              alpine:
                box: alpine/3.21
            """);

        var result = await _reader.ReadLayersAsync(path);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Local.ShouldBeNull();
        result.Value!.Resolved.MachineTypes.ShouldContainKey("alpine");
    }

    [Fact]
    public async Task ReadLayersAsync_deep_merges_local_override_on_top_of_base()
    {
        WriteTempYaml("config-vos.yaml", """
            machine_types:
              alpine:
                box: alpine/3.21
            """);

        var localDir = Path.Combine(_tempDir, "local");
        Directory.CreateDirectory(localDir);
        File.WriteAllText(Path.Combine(localDir, "config-vos-local.yaml"), """
            machine_types:
              docker:
                box: ubuntu/jammy64
            """);

        var result = await _reader.ReadLayersAsync(Path.Combine(_tempDir, "config-vos.yaml"));
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Local.ShouldNotBeNull();
        result.Value!.Resolved.MachineTypes.ShouldContainKey("alpine");
        result.Value!.Resolved.MachineTypes.ShouldContainKey("docker");
    }

    [Fact]
    public async Task ReadLayersAsync_local_override_replaces_same_key()
    {
        WriteTempYaml("config-vos.yaml", """
            machine_types:
              alpine:
                box: alpine/3.20
            """);

        var localDir = Path.Combine(_tempDir, "local");
        Directory.CreateDirectory(localDir);
        File.WriteAllText(Path.Combine(localDir, "config-vos-local.yaml"), """
            machine_types:
              alpine:
                box: alpine/3.21
            """);

        var result = await _reader.ReadLayersAsync(Path.Combine(_tempDir, "config-vos.yaml"));
        result.Value!.Resolved.MachineTypes["alpine"].Box.ShouldBe("alpine/3.21");
    }

    [Fact]
    public async Task ReadLayersAsync_local_override_machines_merge_with_base()
    {
        WriteTempYaml("config-vos.yaml", """
            machine_types:
              alpine:
                box: alpine/3.21
            machines:
              web:
                machine_type_name: alpine
                instances:
                  - name: web-01
            """);

        var localDir = Path.Combine(_tempDir, "local");
        Directory.CreateDirectory(localDir);
        File.WriteAllText(Path.Combine(localDir, "config-vos-local.yaml"), """
            machines:
              web:
                machine_type_name: alpine
                instances:
                  - name: web-local
            """);

        var result = await _reader.ReadLayersAsync(Path.Combine(_tempDir, "config-vos.yaml"));
        result.IsSuccess.ShouldBeTrue();
        // Local overrides the "web" machine entirely
        result.Value!.Resolved.Machines["web"].Instances[0].Name.ShouldBe("web-local");
    }

    [Fact]
    public async Task ReadLayersAsync_local_override_merges_backend()
    {
        WriteTempYaml("config-vos.yaml", """
            backend: vagrant
            machine_types:
              t:
                box: b
            """);

        var localDir = Path.Combine(_tempDir, "local");
        Directory.CreateDirectory(localDir);
        File.WriteAllText(Path.Combine(localDir, "config-vos-local.yaml"), """
            backend: docker
            """);

        var result = await _reader.ReadLayersAsync(Path.Combine(_tempDir, "config-vos.yaml"));
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Resolved.Backend.ShouldBe("docker");
        // Base machine type should still be present
        result.Value!.Resolved.MachineTypes.ShouldContainKey("t");
    }

    [Fact]
    public async Task ReadAsync_returns_failure_message_containing_path()
    {
        var bogusPath = Path.Combine(_tempDir, "missing.yaml");
        var result = await _reader.ReadAsync(bogusPath);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadLayersAsync_returns_failure_when_config_file_does_not_exist()
    {
        var bogusPath = Path.Combine(_tempDir, "missing.yaml");
        var result = await _reader.ReadLayersAsync(bogusPath);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadLayersAsync_local_override_empty_yaml_deserializes_to_null_is_skipped()
    {
        WriteTempYaml("config-vos.yaml", """
            machine_types:
              alpine:
                box: alpine/3.21
            """);

        var localDir = Path.Combine(_tempDir, "local");
        Directory.CreateDirectory(localDir);
        // Empty YAML file — YamlDotNet Deserialize returns null
        File.WriteAllText(Path.Combine(localDir, "config-vos-local.yaml"), "");

        var result = await _reader.ReadLayersAsync(Path.Combine(_tempDir, "config-vos.yaml"));
        result.IsSuccess.ShouldBeTrue();
        // localConfig is null, so resolved == base
        result.Value!.Local.ShouldBeNull();
        result.Value!.Resolved.MachineTypes.ShouldContainKey("alpine");
    }

    [Fact]
    public async Task ReadLayersAsync_local_override_with_only_format_merges_format()
    {
        WriteTempYaml("config-vos.yaml", """
            format: yaml
            machine_types:
              alpine:
                box: alpine/3.21
            """);

        var localDir = Path.Combine(_tempDir, "local");
        Directory.CreateDirectory(localDir);
        File.WriteAllText(Path.Combine(localDir, "config-vos-local.yaml"), """
            format: json
            """);

        var result = await _reader.ReadLayersAsync(Path.Combine(_tempDir, "config-vos.yaml"));
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Local.ShouldNotBeNull();
        result.Value!.Resolved.Format.ShouldBe("json");
        result.Value!.Resolved.MachineTypes.ShouldContainKey("alpine");
    }

    [Fact]
    public async Task ReadAsync_base_yaml_deserializing_to_null_returns_empty_config()
    {
        // YAML comment only — Deserialize returns null, coalesced to new VosConfig()
        var path = WriteTempYaml("config-vos.yaml", "# empty config");
        var result = await _reader.ReadAsync(path);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.MachineTypes.ShouldBeEmpty();
        result.Value!.Machines.ShouldBeEmpty();
    }
}
