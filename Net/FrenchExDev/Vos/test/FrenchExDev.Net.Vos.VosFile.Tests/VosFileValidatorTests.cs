using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.VosFile;

namespace FrenchExDev.Net.Vos.VosFile.Tests;

public class VosFileValidatorTests
{
    private readonly VosFileValidator _validator = new();

    [Fact]
    public void Validate_returns_success_for_valid_config()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["alpine"] = new VosMachineType { Box = "alpine/3.21" } },
            Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01" }] } }
        };

        var result = _validator.Validate(config);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_failure_when_machine_references_missing_type()
    {
        var config = new VosConfig
        {
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "nope" } }
        };

        var result = _validator.Validate(config);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_failure_for_duplicate_instance_names()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = "b" } },
            Machines = new()
            {
                ["a"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "dup" }] },
                ["b"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "dup" }] }
            }
        };

        var result = _validator.Validate(config);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_failure_for_empty_instance_name()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = "b" } },
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "" }] }
            }
        };

        var result = _validator.Validate(config);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_failure_for_whitespace_only_instance_name()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = "b" } },
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "   " }] }
            }
        };

        var result = _validator.Validate(config);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_failure_for_invalid_ip()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = "b" } },
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "i1", Ip = "not-an-ip" }] }
            }
        };

        var result = _validator.Validate(config);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_success_for_valid_ip()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = "b" } },
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "i1", Ip = "192.168.1.10" }] }
            }
        };

        var result = _validator.Validate(config);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_failure_when_enabled_type_has_no_box_and_no_machine_override()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = null, IsEnabled = true } },
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "i1" }] }
            }
        };

        var result = _validator.Validate(config);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_success_when_enabled_type_has_no_box_but_machine_overrides_it()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = null, IsEnabled = true } },
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Box = "override/box", Instances = [new VosInstance { Name = "i1" }] }
            }
        };

        var result = _validator.Validate(config);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_success_when_disabled_type_has_no_box()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = null, IsEnabled = false } },
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "i1" }] }
            }
        };

        var result = _validator.Validate(config);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_success_for_empty_config()
    {
        var config = new VosConfig();
        var result = _validator.Validate(config);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Validate_allows_null_ip_on_instance()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = "b" } },
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "i1", Ip = null }] }
            }
        };

        var result = _validator.Validate(config);
        result.IsSuccess.ShouldBeTrue();
    }
}
