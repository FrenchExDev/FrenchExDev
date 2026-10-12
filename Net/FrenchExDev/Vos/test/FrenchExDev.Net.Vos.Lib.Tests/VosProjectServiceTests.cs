using FrenchExDev.Net.Vos.Lib.Services;
using FrenchExDev.Net.Vos.Lib.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class VosProjectServiceTests : IDisposable
{
    private readonly FakeVosFileReader _reader = new();
    private readonly FakeVosFileWriter _writer = new();
    private readonly VosEventEmitter _emitter = new();
    private readonly TestVosEventCollector _events = new();
    private readonly VosProjectService _svc;
    private readonly string _tempDir;

    public VosProjectServiceTests()
    {
        _events.Subscribe(_emitter);
        _svc = new VosProjectService(_reader, _writer, _emitter, NullLogger<VosProjectService>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), "vos-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private VosConfig CreateConfig() => new()
    {
        MachineTypes = new() { ["alpine"] = new VosMachineType { Box = "alpine/3.21", Provider = new VosProviderConfig { Memory = 2048, Cpus = 2 } } },
        Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01" }] } }
    };

    // ── InitAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task InitAsync_creates_config_and_emits_events()
    {
        var result = await _svc.InitAsync(_tempDir);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MachineTypes.ShouldContainKey("default");
        result.Value!.Machines.ShouldContainKey("default");
        _writer.Written.ShouldContainKey(Path.Combine(_tempDir, "config-vos.yaml"));
        _events.Has<ProjectInitializing>().ShouldBeTrue();
        _events.Has<FileCreated>().ShouldBeTrue();
        _events.Has<ProjectInitialized>().ShouldBeTrue();
    }

    // ── ShowConfigAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ShowConfigAsync_returns_resolved_instances()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.ShowConfigAsync("c.yaml");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value![0].MachineName.ShouldBe("main");
        result.Value![0].Instance.Name.ShouldBe("main-01");
        _events.Has<ConfigShowStarted>().ShouldBeTrue();
        _events.Has<ConfigShowCompleted>().ShouldBeTrue();
    }

    [Fact]
    public async Task ShowConfigAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.ShowConfigAsync("nope.yaml");

        result.IsFailure.ShouldBeTrue();
    }

    // ── ValidateAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_passes_for_valid_config()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.ValidateAsync("c.yaml");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        _events.Has<ValidationStarted>().ShouldBeTrue();
        _events.Single<ValidationCompleted>().ErrorCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_returns_failure_for_invalid_config()
    {
        var config = new VosConfig
        {
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "nope", Instances = [new VosInstance { Name = "a" }] } }
        };
        _reader.SetConfig("c.yaml", config);
        var result = await _svc.ValidateAsync("c.yaml");

        result.IsFailure.ShouldBeTrue();
        _events.Has<ValidationError>().ShouldBeTrue();
        _events.Single<ValidationCompleted>().ErrorCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ValidateAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.ValidateAsync("nope.yaml");
        result.IsFailure.ShouldBeTrue();
    }

    // ── ResolveAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_returns_all_instances()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.ResolveAsync("c.yaml");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        _events.Has<ResolveStarted>().ShouldBeTrue();
        _events.Has<InstanceResolved>().ShouldBeTrue();
        _events.Has<ResolveCompleted>().ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveAsync_filters_by_instance_name()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.ResolveAsync("c.yaml", instanceName: "main-01", all: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value![0].Instance.Name.ShouldBe("main-01");
    }

    [Fact]
    public async Task ResolveAsync_returns_empty_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.ResolveAsync("c.yaml", instanceName: "unknown", all: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ResolveAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.ResolveAsync("nope.yaml");
        result.IsFailure.ShouldBeTrue();
    }

    // ── GetVersion ───────────────────────────────────────────────────

    [Fact]
    public void GetVersion_returns_version_string()
    {
        _svc.GetVersion().ShouldNotBeNullOrWhiteSpace();
        _svc.GetVersion().ShouldContain("vos");
    }

    // ── CreateProvisioningScriptAsync ────────────────────────────────

    [Fact]
    public async Task CreateProvisioningScriptAsync_creates_script_with_defaults()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var result = await _svc.CreateProvisioningScriptAsync(configPath, "setup");

        result.IsSuccess.ShouldBeTrue();
        var expected = Path.Combine(_tempDir, "provisioning", "setup.sh");
        File.Exists(expected).ShouldBeTrue();
        var content = File.ReadAllText(expected);
        content.ShouldContain("#!/bin/sh");
        content.ShouldContain("setup");
        _events.Has<ProvisioningScriptCreating>().ShouldBeTrue();
        _events.Has<ProvisioningScriptCreated>().ShouldBeTrue();
    }

    [Fact]
    public async Task CreateProvisioningScriptAsync_creates_versioned_script()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var result = await _svc.CreateProvisioningScriptAsync(configPath, "install", version: "1.0");

        result.IsSuccess.ShouldBeTrue();
        var expected = Path.Combine(_tempDir, "provisioning", "1.0", "install.sh");
        File.Exists(expected).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateProvisioningScriptAsync_uses_custom_extension()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var result = await _svc.CreateProvisioningScriptAsync(configPath, "setup", extension: "ps1");

        result.IsSuccess.ShouldBeTrue();
        var expected = Path.Combine(_tempDir, "provisioning", "setup.ps1");
        File.Exists(expected).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateProvisioningScriptAsync_uses_template_when_provided()
    {
        var templatePath = Path.Combine(_tempDir, "template.sh");
        File.WriteAllText(templatePath, "#!/bin/bash\necho template");
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");

        var result = await _svc.CreateProvisioningScriptAsync(configPath, "setup", templatePath: templatePath);

        result.IsSuccess.ShouldBeTrue();
        var expected = Path.Combine(_tempDir, "provisioning", "setup.sh");
        File.ReadAllText(expected).ShouldBe("#!/bin/bash\necho template");
    }

    [Fact]
    public async Task CreateProvisioningScriptAsync_falls_back_when_template_not_found()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var result = await _svc.CreateProvisioningScriptAsync(configPath, "setup", templatePath: "/nonexistent/template.sh");

        result.IsSuccess.ShouldBeTrue();
        var expected = Path.Combine(_tempDir, "provisioning", "setup.sh");
        File.ReadAllText(expected).ShouldContain("#!/bin/sh");
    }

    // ── ValidateProvisioningScriptsAsync ─────────────────────────────

    [Fact]
    public async Task ValidateProvisioningScriptsAsync_returns_empty_when_all_scripts_exist()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var provDir = Path.Combine(_tempDir, "provisioning");
        Directory.CreateDirectory(provDir);
        File.WriteAllText(Path.Combine(provDir, "setup.sh"), "#!/bin/sh");

        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["alpine"] = new VosMachineType
                {
                    Box = "alpine/3.21",
                    IsEnabled = true,
                    Provisioning = [new VosProvisioningStep { Key = "setup", Enabled = true }]
                }
            },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01" }] } }
        };
        _reader.SetConfig(configPath, config);

        var result = await _svc.ValidateProvisioningScriptsAsync(configPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
        _events.Has<ProvisioningValidating>().ShouldBeTrue();
        _events.Single<ProvisioningValidated>().ErrorCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateProvisioningScriptsAsync_reports_missing_scripts()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["alpine"] = new VosMachineType
                {
                    Box = "alpine/3.21",
                    IsEnabled = true,
                    Provisioning = [new VosProvisioningStep { Key = "missing-script", Enabled = true }]
                }
            },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01" }] } }
        };
        _reader.SetConfig(configPath, config);

        var result = await _svc.ValidateProvisioningScriptsAsync(configPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        _events.Has<ProvisioningScriptMissing>().ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateProvisioningScriptsAsync_skips_disabled_machine_types()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["disabled"] = new VosMachineType
                {
                    Box = "alpine/3.21",
                    IsEnabled = false,
                    Provisioning = [new VosProvisioningStep { Key = "wont-run", Enabled = true }]
                }
            },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "disabled", Instances = [new VosInstance { Name = "main-01" }] } }
        };
        _reader.SetConfig(configPath, config);

        var result = await _svc.ValidateProvisioningScriptsAsync(configPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateProvisioningScriptsAsync_skips_disabled_steps()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["alpine"] = new VosMachineType
                {
                    Box = "alpine/3.21",
                    IsEnabled = true,
                    Provisioning = [new VosProvisioningStep { Key = "disabled-step", Enabled = false }]
                }
            },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01" }] } }
        };
        _reader.SetConfig(configPath, config);

        var result = await _svc.ValidateProvisioningScriptsAsync(configPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateProvisioningScriptsAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.ValidateProvisioningScriptsAsync("nope.yaml");
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateProvisioningScriptsAsync_uses_custom_provisioning_path()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var provDir = Path.Combine(_tempDir, "scripts");
        Directory.CreateDirectory(provDir);
        File.WriteAllText(Path.Combine(provDir, "setup.sh"), "#!/bin/sh");

        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["alpine"] = new VosMachineType
                {
                    Box = "alpine/3.21",
                    IsEnabled = true,
                    ProvisioningPath = "scripts",
                    Provisioning = [new VosProvisioningStep { Key = "setup", Enabled = true }]
                }
            },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01" }] } }
        };
        _reader.SetConfig(configPath, config);

        var result = await _svc.ValidateProvisioningScriptsAsync(configPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateProvisioningScriptsAsync_checks_versioned_step_path()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["alpine"] = new VosMachineType
                {
                    Box = "alpine/3.21",
                    IsEnabled = true,
                    Provisioning = [new VosProvisioningStep { Key = "setup", Version = "1.0", Enabled = true }]
                }
            },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01" }] } }
        };
        _reader.SetConfig(configPath, config);

        var result = await _svc.ValidateProvisioningScriptsAsync(configPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1); // Missing because version/1.0/setup.sh doesn't exist
    }

    [Fact]
    public async Task ValidateProvisioningScriptsAsync_uses_custom_extension()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["win"] = new VosMachineType
                {
                    Box = "win/10",
                    IsEnabled = true,
                    Provisioning = [new VosProvisioningStep { Key = "setup", Extension = "ps1", Enabled = true }]
                }
            },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "win", Instances = [new VosInstance { Name = "main-01" }] } }
        };
        _reader.SetConfig(configPath, config);

        var result = await _svc.ValidateProvisioningScriptsAsync(configPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1); // Missing because setup.ps1 doesn't exist
    }

    // ── ResolveAsync — additional branch coverage ───────────────────

    [Fact]
    public async Task ResolveAsync_with_all_true_and_instance_name_returns_all()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.ResolveAsync("c.yaml", instanceName: "main-01", all: true);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1); // all=true overrides instanceName filter
    }

    [Fact]
    public async Task ResolveAsync_with_all_false_and_null_instance_returns_all()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.ResolveAsync("c.yaml", instanceName: null, all: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1); // instanceName null makes the condition true
    }

    // ── CreateProvisioningScriptAsync — templatePath not null but file missing ──

    [Fact]
    public async Task CreateProvisioningScriptAsync_with_null_templatePath_uses_default()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var result = await _svc.CreateProvisioningScriptAsync(configPath, "init", templatePath: null);

        result.IsSuccess.ShouldBeTrue();
        var expected = Path.Combine(_tempDir, "provisioning", "init.sh");
        File.ReadAllText(expected).ShouldContain("#!/bin/sh");
    }

    // ── ValidateProvisioningScriptsAsync — null ProvisioningPath uses default ──

    [Fact]
    public async Task ValidateProvisioningScriptsAsync_null_provisioning_path_uses_default()
    {
        var configPath = Path.Combine(_tempDir, "config-vos.yaml");
        var provDir = Path.Combine(_tempDir, "provisioning");
        Directory.CreateDirectory(provDir);
        File.WriteAllText(Path.Combine(provDir, "boot.sh"), "#!/bin/sh");

        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["alpine"] = new VosMachineType
                {
                    Box = "alpine/3.21",
                    IsEnabled = true,
                    ProvisioningPath = null, // explicitly null — falls back to "provisioning"
                    Provisioning = [new VosProvisioningStep { Key = "boot", Enabled = true, Extension = null, Version = null }]
                }
            },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01" }] } }
        };
        _reader.SetConfig(configPath, config);

        var result = await _svc.ValidateProvisioningScriptsAsync(configPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task CreateProvisioningScriptAsync_with_root_config_path_uses_dot_as_dir()
    {
        // Path.GetDirectoryName("config.yaml") returns "" on some platforms, ?? "." covers that
        var result = await _svc.CreateProvisioningScriptAsync("config.yaml", "test-root");
        result.IsSuccess.ShouldBeTrue();
        // Script created relative to "." since no directory in config path
    }
}
