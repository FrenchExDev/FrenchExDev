using FrenchExDev.Net.Vos.Lib.Tests.Fakes;
using FrenchExDev.Net.Vos.Lib.Validation;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class VosValidationRuleTests
{
    private static VosConfig ValidConfig() => new()
    {
        MachineTypes = new() { ["alpine"] = new VosMachineType { Box = "alpine/3.21" } },
        Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01", Ip = "192.168.56.10" }] } }
    };

    [Fact]
    public void MachineTypeReferenceRule_passes_for_valid_config()
    {
        new MachineTypeReferenceRule().Validate(ValidConfig()).ShouldBeEmpty();
    }

    [Fact]
    public void MachineTypeReferenceRule_fails_for_missing_type()
    {
        var config = new VosConfig
        {
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "nope" } }
        };
        new MachineTypeReferenceRule().Validate(config).ShouldNotBeEmpty();
    }

    [Fact]
    public void UniqueInstanceNameRule_passes_for_unique_names()
    {
        new UniqueInstanceNameRule().Validate(ValidConfig()).ShouldBeEmpty();
    }

    [Fact]
    public void UniqueInstanceNameRule_fails_for_duplicate_names()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["a"] = new VosMachine { MachineTypeName = "x", Instances = [new VosInstance { Name = "dup" }] },
                ["b"] = new VosMachine { MachineTypeName = "x", Instances = [new VosInstance { Name = "dup" }] }
            }
        };
        new UniqueInstanceNameRule().Validate(config).ShouldNotBeEmpty();
    }

    [Fact]
    public void InstanceIpFormatRule_passes_for_valid_ip()
    {
        new InstanceIpFormatRule().Validate(ValidConfig()).ShouldBeEmpty();
    }

    [Fact]
    public void InstanceIpFormatRule_fails_for_invalid_ip()
    {
        var config = new VosConfig
        {
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "x", Instances = [new VosInstance { Name = "a", Ip = "notanip" }] } }
        };
        new InstanceIpFormatRule().Validate(config).ShouldNotBeEmpty();
    }

    [Fact]
    public void InstanceNameNotEmptyRule_fails_for_empty_name()
    {
        var config = new VosConfig
        {
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "x", Instances = [new VosInstance { Name = "" }] } }
        };
        new InstanceNameNotEmptyRule().Validate(config).ShouldNotBeEmpty();
    }

    [Fact]
    public void NoIpConflictRule_passes_when_no_conflicts()
    {
        new NoIpConflictRule().Validate(ValidConfig()).ShouldBeEmpty();
    }

    [Fact]
    public void NoIpConflictRule_detects_duplicate_ips()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["a"] = new VosMachine { MachineTypeName = "x", Instances = [new VosInstance { Name = "a1", Ip = "10.0.0.1" }] },
                ["b"] = new VosMachine { MachineTypeName = "x", Instances = [new VosInstance { Name = "b1", Ip = "10.0.0.1" }] }
            }
        };
        new NoIpConflictRule().Validate(config).ShouldNotBeEmpty();
    }

    [Fact]
    public void MachineTypeHasBoxRule_passes_when_box_set()
    {
        new MachineTypeHasBoxRule().Validate(ValidConfig()).ShouldBeEmpty();
    }

    [Fact]
    public void MachineTypeHasBoxRule_fails_when_no_box()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["empty"] = new VosMachineType() },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "empty" } }
        };
        new MachineTypeHasBoxRule().Validate(config).ShouldNotBeEmpty();
    }

    // ── ProvisioningScriptExistsRule ─────────────────────────────────

    [Fact]
    public void ProvisioningScriptExistsRule_passes_when_all_scripts_exist()
    {
        var fs = new FakeFileSystem();
        var baseDir = "/project";
        fs.AddFile(Path.Combine(baseDir, "provisioning", "setup.sh"));

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
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine" } }
        };

        new ProvisioningScriptExistsRule(fs, baseDir).Validate(config).ShouldBeEmpty();
    }

    [Fact]
    public void ProvisioningScriptExistsRule_fails_when_script_missing()
    {
        var fs = new FakeFileSystem();
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["alpine"] = new VosMachineType
                {
                    Box = "alpine/3.21",
                    IsEnabled = true,
                    Provisioning = [new VosProvisioningStep { Key = "missing", Enabled = true }]
                }
            },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine" } }
        };

        new ProvisioningScriptExistsRule(fs, "/project").Validate(config).ShouldNotBeEmpty();
    }

    [Fact]
    public void ProvisioningScriptExistsRule_skips_disabled_machine_types()
    {
        var fs = new FakeFileSystem();
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["disabled"] = new VosMachineType
                {
                    Box = "alpine/3.21",
                    IsEnabled = false,
                    Provisioning = [new VosProvisioningStep { Key = "wont-check", Enabled = true }]
                }
            },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "disabled" } }
        };

        new ProvisioningScriptExistsRule(fs, "/project").Validate(config).ShouldBeEmpty();
    }

    [Fact]
    public void ProvisioningScriptExistsRule_skips_disabled_steps()
    {
        var fs = new FakeFileSystem();
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
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine" } }
        };

        new ProvisioningScriptExistsRule(fs, "/project").Validate(config).ShouldBeEmpty();
    }

    [Fact]
    public void ProvisioningScriptExistsRule_uses_versioned_path()
    {
        var fs = new FakeFileSystem();
        var baseDir = "/project";
        fs.AddFile(Path.Combine(baseDir, "provisioning", "1.0", "setup.sh"));

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
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine" } }
        };

        new ProvisioningScriptExistsRule(fs, baseDir).Validate(config).ShouldBeEmpty();
    }

    [Fact]
    public void ProvisioningScriptExistsRule_uses_custom_extension()
    {
        var fs = new FakeFileSystem();
        var baseDir = "/project";
        fs.AddFile(Path.Combine(baseDir, "provisioning", "setup.ps1"));

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
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "win" } }
        };

        new ProvisioningScriptExistsRule(fs, baseDir).Validate(config).ShouldBeEmpty();
    }

    [Fact]
    public void ProvisioningScriptExistsRule_uses_custom_provisioning_path()
    {
        var fs = new FakeFileSystem();
        var baseDir = "/project";
        fs.AddFile(Path.Combine(baseDir, "scripts", "setup.sh"));

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
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine" } }
        };

        new ProvisioningScriptExistsRule(fs, baseDir).Validate(config).ShouldBeEmpty();
    }
}
