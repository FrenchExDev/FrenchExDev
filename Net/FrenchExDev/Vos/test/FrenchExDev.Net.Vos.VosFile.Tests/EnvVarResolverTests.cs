using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.VosFile;

namespace FrenchExDev.Net.Vos.VosFile.Tests;

public class EnvVarResolverTests
{
    [Fact]
    public void Resolve_substitutes_env_var_in_machine_type_variables()
    {
        Environment.SetEnvironmentVariable("TEST_VOL_VAR", "resolved_value");
        try
        {
            var config = new VosConfig
            {
                MachineTypes = new()
                {
                    ["t"] = new VosMachineType
                    {
                        Box = "b",
                        Variables = new() { ["MY_VAR"] = "${TEST_VOL_VAR}" }
                    }
                }
            };

            var resolved = EnvVarResolver.Resolve(config);
            resolved.MachineTypes["t"].Variables["MY_VAR"].ShouldBe("resolved_value");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_VOL_VAR", null);
        }
    }

    [Fact]
    public void Resolve_keeps_original_when_env_var_not_set()
    {
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["t"] = new VosMachineType
                {
                    Box = "b",
                    Variables = new() { ["MISSING"] = "${NONEXISTENT_VAR_XYZ}" }
                }
            }
        };

        var resolved = EnvVarResolver.Resolve(config);
        resolved.MachineTypes["t"].Variables["MISSING"].ShouldBe("${NONEXISTENT_VAR_XYZ}");
    }

    [Fact]
    public void Resolve_substitutes_env_var_in_provisioning_env()
    {
        Environment.SetEnvironmentVariable("TEST_PROV_TOKEN", "secret123");
        try
        {
            var config = new VosConfig
            {
                MachineTypes = new()
                {
                    ["t"] = new VosMachineType
                    {
                        Box = "b",
                        Provisioning = [new VosProvisioningStep { Key = "k", Env = new() { ["TOKEN"] = "${TEST_PROV_TOKEN}" } }]
                    }
                }
            };

            var resolved = EnvVarResolver.Resolve(config);
            resolved.MachineTypes["t"].Provisioning[0].Env["TOKEN"].ShouldBe("secret123");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_PROV_TOKEN", null);
        }
    }

    [Fact]
    public void Resolve_handles_multiple_vars_in_single_string()
    {
        Environment.SetEnvironmentVariable("TEST_A", "hello");
        Environment.SetEnvironmentVariable("TEST_B", "world");
        try
        {
            var config = new VosConfig
            {
                MachineTypes = new()
                {
                    ["t"] = new VosMachineType
                    {
                        Box = "b",
                        Variables = new() { ["GREETING"] = "${TEST_A} ${TEST_B}" }
                    }
                }
            };

            var resolved = EnvVarResolver.Resolve(config);
            resolved.MachineTypes["t"].Variables["GREETING"].ShouldBe("hello world");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_A", null);
            Environment.SetEnvironmentVariable("TEST_B", null);
        }
    }

    [Fact]
    public void Resolve_returns_config_unchanged_when_no_machine_types()
    {
        var config = new VosConfig();
        var resolved = EnvVarResolver.Resolve(config);
        resolved.MachineTypes.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_handles_machine_type_with_empty_variables()
    {
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["t"] = new VosMachineType { Box = "b", Variables = new() }
            }
        };

        var resolved = EnvVarResolver.Resolve(config);
        resolved.MachineTypes["t"].Variables.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_handles_machine_type_with_empty_provisioning_list()
    {
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["t"] = new VosMachineType { Box = "b", Provisioning = [] }
            }
        };

        var resolved = EnvVarResolver.Resolve(config);
        resolved.MachineTypes["t"].Provisioning.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_handles_provisioning_step_with_empty_env()
    {
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["t"] = new VosMachineType
                {
                    Box = "b",
                    Provisioning = [new VosProvisioningStep { Key = "k", Env = new() }]
                }
            }
        };

        var resolved = EnvVarResolver.Resolve(config);
        resolved.MachineTypes["t"].Provisioning[0].Env.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_does_not_modify_plain_strings_without_env_pattern()
    {
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["t"] = new VosMachineType
                {
                    Box = "b",
                    Variables = new() { ["PLAIN"] = "no-vars-here" }
                }
            }
        };

        var resolved = EnvVarResolver.Resolve(config);
        resolved.MachineTypes["t"].Variables["PLAIN"].ShouldBe("no-vars-here");
    }
}
