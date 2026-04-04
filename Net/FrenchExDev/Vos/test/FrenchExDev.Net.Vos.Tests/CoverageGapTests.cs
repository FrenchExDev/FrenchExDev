using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Tests;

// ═══════════════════════════════════════════════════════════════════
// VosConfigValidator — full branch coverage for all 5 rules
// ═══════════════════════════════════════════════════════════════════

public class VosConfigValidatorTests
{
    [Fact]
    public void Validate_ValidConfig_ReturnsNoErrors()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "ubuntu/jammy64" } },
            Machines = new()
            {
                ["web"] = new VosMachine
                {
                    MachineTypeName = "base",
                    Instances = new() { new VosInstance { Name = "web-01", Ip = "10.0.0.1" } }
                }
            }
        };

        VosConfigValidator.Validate(config).ShouldBeEmpty();
    }

    // Rule 1: machine references non-existent machine type
    [Fact]
    public void Validate_MachineReferencesMissingType_ReturnsError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "box" } },
            Machines = new()
            {
                ["web"] = new VosMachine
                {
                    MachineTypeName = "nonexistent",
                    Instances = new() { new VosInstance { Name = "web-01" } }
                }
            }
        };

        var errors = VosConfigValidator.Validate(config);
        errors.Count.ShouldBe(1);
        errors[0].ShouldContain("nonexistent");
        errors[0].ShouldContain("does not exist");
    }

    [Fact]
    public void Validate_MachineReferencesExistingType_NoError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "box" } },
            Machines = new()
            {
                ["web"] = new VosMachine
                {
                    MachineTypeName = "base",
                    Instances = new() { new VosInstance { Name = "web-01" } }
                }
            }
        };

        VosConfigValidator.Validate(config).ShouldBeEmpty();
    }

    // Rule 2: duplicate instance names
    [Fact]
    public void Validate_DuplicateInstanceNames_ReturnsError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "box" } },
            Machines = new()
            {
                ["m1"] = new VosMachine
                {
                    MachineTypeName = "base",
                    Instances = new() { new VosInstance { Name = "dup" } }
                },
                ["m2"] = new VosMachine
                {
                    MachineTypeName = "base",
                    Instances = new() { new VosInstance { Name = "dup" } }
                }
            }
        };

        var errors = VosConfigValidator.Validate(config);
        errors.ShouldContain(e => e.Contains("Duplicate instance name 'dup'"));
    }

    [Fact]
    public void Validate_UniqueInstanceNames_NoError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "box" } },
            Machines = new()
            {
                ["m1"] = new VosMachine
                {
                    MachineTypeName = "base",
                    Instances = new() { new VosInstance { Name = "a" }, new VosInstance { Name = "b" } }
                }
            }
        };

        VosConfigValidator.Validate(config).ShouldBeEmpty();
    }

    // Rule 3: enabled machine type must have a box (or box override on machine)
    [Fact]
    public void Validate_EnabledTypeNoBox_NoOverride_ReturnsError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["bare"] = new VosMachineType { IsEnabled = true } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "bare",
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        var errors = VosConfigValidator.Validate(config);
        errors.ShouldContain(e => e.Contains("has no box defined"));
    }

    [Fact]
    public void Validate_EnabledTypeNoBox_WithMachineBoxOverride_NoError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["bare"] = new VosMachineType { IsEnabled = true } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "bare",
                    Box = "override/box",
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        VosConfigValidator.Validate(config).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DisabledTypeNoBox_SkipsCheck()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["bare"] = new VosMachineType { IsEnabled = false } },
            Machines = new()
        };

        VosConfigValidator.Validate(config).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_EnabledTypeWithBox_NoError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { IsEnabled = true, Box = "ubuntu" } },
            Machines = new()
        };

        VosConfigValidator.Validate(config).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_EnabledTypeWhitespaceBox_NoOverride_ReturnsError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["bare"] = new VosMachineType { IsEnabled = true, Box = "   " } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "bare",
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        var errors = VosConfigValidator.Validate(config);
        errors.ShouldContain(e => e.Contains("has no box defined"));
    }

    // Rule 4: invalid IP addresses
    [Fact]
    public void Validate_InvalidIp_ReturnsError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "box" } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "base",
                    Instances = new() { new VosInstance { Name = "i", Ip = "not-an-ip" } }
                }
            }
        };

        var errors = VosConfigValidator.Validate(config);
        errors.ShouldContain(e => e.Contains("invalid IP"));
    }

    [Fact]
    public void Validate_ValidIp_NoError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "box" } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "base",
                    Instances = new() { new VosInstance { Name = "i", Ip = "192.168.1.100" } }
                }
            }
        };

        VosConfigValidator.Validate(config).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NullIp_Skipped()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "box" } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "base",
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        VosConfigValidator.Validate(config).ShouldBeEmpty();
    }

    // Rule 5: empty instance names
    [Fact]
    public void Validate_EmptyInstanceName_ReturnsError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "box" } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "base",
                    Instances = new() { new VosInstance { Name = "" } }
                }
            }
        };

        var errors = VosConfigValidator.Validate(config);
        errors.ShouldContain(e => e.Contains("empty name"));
    }

    [Fact]
    public void Validate_WhitespaceInstanceName_ReturnsError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "box" } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "base",
                    Instances = new() { new VosInstance { Name = "   " } }
                }
            }
        };

        var errors = VosConfigValidator.Validate(config);
        errors.ShouldContain(e => e.Contains("empty name"));
    }

    // Multiple errors at once
    [Fact]
    public void Validate_MultipleErrors_ReturnsAll()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["bare"] = new VosMachineType { IsEnabled = true } },
            Machines = new()
            {
                ["m1"] = new VosMachine
                {
                    MachineTypeName = "missing-type",
                    Instances = new()
                    {
                        new VosInstance { Name = "" },
                        new VosInstance { Name = "a", Ip = "bad-ip" }
                    }
                },
                ["m2"] = new VosMachine
                {
                    MachineTypeName = "bare",
                    Instances = new() { new VosInstance { Name = "a" } } // dup with m1.a
                }
            }
        };

        var errors = VosConfigValidator.Validate(config);
        errors.Count.ShouldBeGreaterThan(2);
    }

    // Edge: enabled type with no machines referencing it (no box override possible)
    [Fact]
    public void Validate_EnabledTypeNoBox_NoMachinesReferencing_ReturnsError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["orphan"] = new VosMachineType { IsEnabled = true } },
            Machines = new()
        };

        var errors = VosConfigValidator.Validate(config);
        errors.ShouldContain(e => e.Contains("has no box defined"));
    }

    // Edge: machine with empty box override (whitespace) should not count as override
    [Fact]
    public void Validate_EnabledTypeNoBox_MachineWhitespaceBoxOverride_ReturnsError()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["bare"] = new VosMachineType { IsEnabled = true } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "bare",
                    Box = "   ",
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        var errors = VosConfigValidator.Validate(config);
        errors.ShouldContain(e => e.Contains("has no box defined"));
    }

    // Empty config
    [Fact]
    public void Validate_EmptyConfig_NoErrors()
    {
        VosConfigValidator.Validate(new VosConfig()).ShouldBeEmpty();
    }
}

// ═══════════════════════════════════════════════════════════════════
// VosConfigMerger — remaining branch gaps
// ═══════════════════════════════════════════════════════════════════

public class VosConfigMergerCoverageTests
{
    [Fact]
    public void Resolve_NullMachineProvider_NullTypeProvider_BothFallbackToDefaults()
    {
        // Both providers null — exercises fallback = new VosProviderConfig() path for both
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = null } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Provider = null,
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        var r = VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]);
        r.ProviderType.ShouldBe("virtualbox");
        r.Memory.ShouldBe(1024);
        r.Cpus.ShouldBe(2);
        r.VideoMemory.ShouldBe(64);
        r.Gui.ShouldBeFalse();
        r.LinkedClones.ShouldBeTrue();
        r.VboxManage.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_NullMachineProvisioning_FallsBackToMachineType()
    {
        var typeSteps = new List<VosProvisioningStep> { new() { Key = "base-step" } };
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provisioning = typeSteps } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Provisioning = null, // null → falls to machineType.Provisioning
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0])
            .Provisioning.ShouldBe(typeSteps);
    }

    [Fact]
    public void Resolve_NullMachineSharedFolders_FallsBackToMachineType()
    {
        var typeFolders = new List<VosSharedFolder> { new() { HostPath = "/h", GuestPath = "/g" } };
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { SharedFolders = typeFolders } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    SharedFolders = null,
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0])
            .SharedFolders.ShouldBe(typeFolders);
    }

    [Fact]
    public void Resolve_NullInstanceIp_FallsToMachineTypeNetworkIp()
    {
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["t"] = new VosMachineType
                {
                    Network = new VosNetworkConfig { Private = new VosPrivateNetwork { Ip = "10.0.0.1" } }
                }
            },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new() { new VosInstance { Name = "i" } } // no Ip
                }
            }
        };

        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0])
            .Ip.ShouldBe("10.0.0.1");
    }

    [Fact]
    public void Resolve_InstanceIp_OverridesNetworkIp()
    {
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["t"] = new VosMachineType
                {
                    Network = new VosNetworkConfig { Private = new VosPrivateNetwork { Ip = "10.0.0.1" } }
                }
            },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new() { new VosInstance { Name = "i", Ip = "10.0.0.99" } }
                }
            }
        };

        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0])
            .Ip.ShouldBe("10.0.0.99");
    }

    [Fact]
    public void Resolve_NullInstanceMemory_FallsToProviderMemory()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { Memory = 8192 } } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new() { new VosInstance { Name = "i" } } // Memory = null
                }
            }
        };

        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0])
            .Memory.ShouldBe(8192);
    }

    [Fact]
    public void Resolve_NullInstanceCpus_FallsToProviderCpus()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { Cpus = 16 } } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new() { new VosInstance { Name = "i" } } // Cpus = null
                }
            }
        };

        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0])
            .Cpus.ShouldBe(16);
    }

    [Fact]
    public void Resolve_MachineVariablesOverrideMachineTypeVariables()
    {
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["t"] = new VosMachineType { Variables = new() { ["A"] = "type", ["B"] = "type" } }
            },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Variables = new() { ["B"] = "machine", ["C"] = "machine" },
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        var vars = VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Variables;
        vars["A"].ShouldBe("type");      // from base
        vars["B"].ShouldBe("machine");   // overridden
        vars["C"].ShouldBe("machine");   // only in override
    }

    [Fact]
    public void Resolve_EmptyVboxManage_FallsToType()
    {
        var typeVbox = new List<List<string>> { new() { "modifyvm" } };
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { VboxManage = typeVbox } } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Provider = new VosProviderConfig { VboxManage = new() }, // empty, not null
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0])
            .VboxManage.ShouldBe(typeVbox);
    }

    [Fact]
    public void Resolve_EmptyPlugins_FromMachineType()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Plugins = new() } },
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new() { new VosInstance { Name = "i" } }
                }
            }
        };

        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0])
            .Plugins.ShouldBeEmpty();
    }
}

// ═══════════════════════════════════════════════════════════════════
// NetworkGenerator — remaining branch gaps
// ═══════════════════════════════════════════════════════════════════

public class NetworkGeneratorCoverageTests
{
    [Fact]
    public void Generate_Slash25_MaxHost126()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new() { new VosInstance { Name = "a" } }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.Generate(config, "10.0.0.0/25", startAt: 1);
        config.Machines["m"].Instances[0].Ip.ShouldBe("10.0.0.1");
    }

    [Fact]
    public void Generate_Slash25_ExhaustsAt126()
    {
        // /25 = 126 usable hosts (1-126)
        var instances = Enumerable.Range(0, 127).Select(i => new VosInstance { Name = $"vm-{i}" }).ToList();
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Instances = instances }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        Should.Throw<InvalidOperationException>(() => NetworkGenerator.Generate(config, "10.0.0.0/25", startAt: 1));
    }

    [Fact]
    public void Generate_Slash26_MaxHost62()
    {
        var instances = Enumerable.Range(0, 63).Select(i => new VosInstance { Name = $"vm-{i}" }).ToList();
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Instances = instances }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        Should.Throw<InvalidOperationException>(() => NetworkGenerator.Generate(config, "10.0.0.0/26", startAt: 1));
    }

    [Fact]
    public void Generate_SubnetWithoutSlash_DefaultsTo24()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new() { new VosInstance { Name = "a" } }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.Generate(config, "172.16.0.0", startAt: 5);
        config.Machines["m"].Instances[0].Ip.ShouldBe("172.16.0.5");
    }

    [Fact]
    public void Generate_SkipsAlreadyAssignedIps_InSequence()
    {
        // Pre-assign IPs at the exact startAt positions to force skipping
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new()
                    {
                        new VosInstance { Name = "pre1", Ip = "192.168.56.10" }, // occupies startAt
                        new VosInstance { Name = "pre2", Ip = "192.168.56.11" }, // occupies startAt+1
                        new VosInstance { Name = "new1" }, // should get .12
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.Generate(config);
        config.Machines["m"].Instances[2].Ip.ShouldBe("192.168.56.12");
    }

    [Fact]
    public void Generate_ExistingHostname_Preserved()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new()
                    {
                        new VosInstance { Name = "a", Hostname = "custom.host" }
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.Generate(config);
        config.Machines["m"].Instances[0].Hostname.ShouldBe("custom.host");
    }

    [Fact]
    public void Generate_NullHostname_GetsLocalSuffix()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new()
                    {
                        new VosInstance { Name = "myvm" }
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.Generate(config);
        config.Machines["m"].Instances[0].Hostname.ShouldBe("myvm.local");
    }

    [Fact]
    public void Show_SkipsDisabledMachines()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["enabled"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new() { new VosInstance { Name = "a", Ip = "1.1.1.1" } }
                },
                ["disabled"] = new VosMachine
                {
                    MachineTypeName = "t",
                    IsEnabled = false,
                    Instances = new() { new VosInstance { Name = "b", Ip = "2.2.2.2" } }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        var results = NetworkGenerator.Show(config);
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("a");
    }

    [Fact]
    public void ValidateNoConflicts_SkipsDisabledMachines()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["enabled"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new() { new VosInstance { Name = "a", Ip = "10.0.0.1" } }
                },
                ["disabled"] = new VosMachine
                {
                    MachineTypeName = "t",
                    IsEnabled = false,
                    Instances = new() { new VosInstance { Name = "b", Ip = "10.0.0.1" } } // same IP but disabled
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.ValidateNoConflicts(config).ShouldBeEmpty();
    }

    [Fact]
    public void ValidateNoConflicts_SkipsNullAndEmptyIps()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new()
                    {
                        new VosInstance { Name = "a" },           // null IP
                        new VosInstance { Name = "b", Ip = "" },  // empty IP
                        new VosInstance { Name = "c", Ip = "10.0.0.1" }
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.ValidateNoConflicts(config).ShouldBeEmpty();
    }

    [Fact]
    public void Generate_StartAtBoundary_ExactlyFills()
    {
        // /26 = 62 hosts, startAt: 1 means we can assign hosts 1..62
        var instances = Enumerable.Range(0, 62).Select(i => new VosInstance { Name = $"vm-{i}" }).ToList();
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine { MachineTypeName = "t", Instances = instances }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        var assigned = NetworkGenerator.Generate(config, "10.0.0.0/26", startAt: 1);
        assigned.ShouldBe(62);
        config.Machines["m"].Instances[61].Ip.ShouldBe("10.0.0.62");
    }

    [Fact]
    public void Generate_InstanceWithExistingIp_NotReassigned()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new()
                    {
                        new VosInstance { Name = "fixed", Ip = "1.2.3.4" },
                        new VosInstance { Name = "auto" }
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        var assigned = NetworkGenerator.Generate(config);
        assigned.ShouldBe(1); // only "auto" was assigned
        config.Machines["m"].Instances[0].Ip.ShouldBe("1.2.3.4"); // unchanged
    }
}

// ═══════════════════════════════════════════════════════════════════
// VosConfigManager — remaining error paths and edge cases
// ═══════════════════════════════════════════════════════════════════

public class VosConfigManagerCoverageTests
{
    private static VosConfigManager NewManager()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["base"] = new VosMachineType { Box = "ubuntu/jammy64" } },
            Machines = new()
        };
        return new VosConfigManager(config);
    }

    // ── SetMachineTypeProperty error path ──────────────────────────

    [Fact]
    public void SetMachineTypeProperty_NotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() =>
            mgr.SetMachineTypeProperty("nonexistent", _ => { }));
    }

    // ── RemoveMachine error path ───────────────────────────────────

    [Fact]
    public void RemoveMachine_NotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.RemoveMachine("nonexistent"));
    }

    // ── EnableMachine / DisableMachine error paths ─────────────────

    [Fact]
    public void EnableMachine_NotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.EnableMachine("nonexistent"));
    }

    [Fact]
    public void DisableMachine_NotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.DisableMachine("nonexistent"));
    }

    // ── AddInstance error paths ────────────────────────────────────

    [Fact]
    public void AddInstance_MachineNotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.AddInstance("nonexistent", "i1"));
    }

    // ── RemoveInstance error paths ─────────────────────────────────

    [Fact]
    public void RemoveInstance_MachineNotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.RemoveInstance("nonexistent", "i1"));
    }

    [Fact]
    public void RemoveInstance_InstanceNotFound_Throws()
    {
        var mgr = NewManager();
        mgr.AddMachine("m", "base");
        Should.Throw<InvalidOperationException>(() => mgr.RemoveInstance("m", "nonexistent"));
    }

    // ── VBoxManage CRUD edge cases ─────────────────────────────────

    [Fact]
    public void AddVboxManageCommand_NullProvider_CreatesProvider()
    {
        var mgr = NewManager();
        mgr.Config.MachineTypes["base"].Provider = null;
        mgr.AddVboxManageCommand("base", new() { "modifyvm", "test" });
        mgr.Config.MachineTypes["base"].Provider.ShouldNotBeNull();
        mgr.Config.MachineTypes["base"].Provider!.VboxManage.Count.ShouldBe(1);
    }

    [Fact]
    public void AddVboxManageCommand_MachineTypeNotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() =>
            mgr.AddVboxManageCommand("nonexistent", new() { "cmd" }));
    }

    [Fact]
    public void RemoveVboxManageCommand_NegativeIndex_Throws()
    {
        var mgr = NewManager();
        mgr.AddVboxManageCommand("base", new() { "cmd" });
        Should.Throw<InvalidOperationException>(() => mgr.RemoveVboxManageCommand("base", -1));
    }

    [Fact]
    public void RemoveVboxManageCommand_IndexOutOfRange_Throws()
    {
        var mgr = NewManager();
        mgr.AddVboxManageCommand("base", new() { "cmd" });
        Should.Throw<InvalidOperationException>(() => mgr.RemoveVboxManageCommand("base", 5));
    }

    [Fact]
    public void RemoveVboxManageCommand_NullProvider_Throws()
    {
        var mgr = NewManager();
        mgr.Config.MachineTypes["base"].Provider = null;
        Should.Throw<InvalidOperationException>(() => mgr.RemoveVboxManageCommand("base", 0));
    }

    [Fact]
    public void RemoveVboxManageCommand_MachineTypeNotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.RemoveVboxManageCommand("nonexistent", 0));
    }

    [Fact]
    public void RemoveVboxManageCommand_AtBoundaryIndex_Removes()
    {
        var mgr = NewManager();
        mgr.AddVboxManageCommand("base", new() { "cmd1" });
        mgr.AddVboxManageCommand("base", new() { "cmd2" });
        mgr.RemoveVboxManageCommand("base", 1); // last valid index
        mgr.ListVboxManageCommands("base").Count.ShouldBe(1);
        mgr.ListVboxManageCommands("base")[0][0].ShouldBe("cmd1");
    }

    [Fact]
    public void ClearVboxManageCommands_NullProvider_NoOp()
    {
        var mgr = NewManager();
        mgr.Config.MachineTypes["base"].Provider = null;
        // Should not throw — just a no-op
        mgr.ClearVboxManageCommands("base");
    }

    [Fact]
    public void ClearVboxManageCommands_EmptyList_NoOp()
    {
        var mgr = NewManager();
        // Provider exists but VboxManage is already empty
        mgr.ClearVboxManageCommands("base");
        mgr.ListVboxManageCommands("base").Count.ShouldBe(0);
    }

    [Fact]
    public void ClearVboxManageCommands_MachineTypeNotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.ClearVboxManageCommands("nonexistent"));
    }

    [Fact]
    public void ListVboxManageCommands_NullProvider_ReturnsEmpty()
    {
        var mgr = NewManager();
        mgr.Config.MachineTypes["base"].Provider = null;
        mgr.ListVboxManageCommands("base").Count.ShouldBe(0);
    }

    [Fact]
    public void ListVboxManageCommands_MachineTypeNotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.ListVboxManageCommands("nonexistent"));
    }

    // ── Shared folders error paths ─────────────────────────────────

    [Fact]
    public void AddSharedFolder_MachineTypeNotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.AddSharedFolder("nonexistent", "/h", "/g"));
    }

    [Fact]
    public void AddSharedFolder_WithNullType()
    {
        var mgr = NewManager();
        mgr.AddSharedFolder("base", "/host", "/guest");
        var sf = mgr.Config.MachineTypes["base"].SharedFolders.Last();
        sf.Type.ShouldBeNull();
        sf.HostPath.ShouldBe("/host");
        sf.GuestPath.ShouldBe("/guest");
    }

    // ── Plugins error paths ────────────────────────────────────────

    [Fact]
    public void AddPlugin_MachineTypeNotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.AddPlugin("nonexistent", "plugin"));
    }

    [Fact]
    public void AddPlugin_NewPlugin_Added()
    {
        var mgr = NewManager();
        mgr.AddPlugin("base", "new-plugin");
        mgr.Config.MachineTypes["base"].Plugins.ShouldContain("new-plugin");
    }

    [Fact]
    public void AddPlugin_Duplicate_NotAdded()
    {
        var mgr = NewManager();
        mgr.AddPlugin("base", "plug");
        mgr.AddPlugin("base", "plug");
        mgr.Config.MachineTypes["base"].Plugins.Count(p => p == "plug").ShouldBe(1);
    }

    // ── Provisioning error paths ───────────────────────────────────

    [Fact]
    public void AddProvisioningStep_MachineTypeNotFound_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.AddProvisioningStep("nonexistent", "step"));
    }

    [Fact]
    public void AddProvisioningStep_DefaultPrivileged_True()
    {
        var mgr = NewManager();
        mgr.AddProvisioningStep("base", "docker");
        var step = mgr.Config.MachineTypes["base"].Provisioning[0];
        step.Key.ShouldBe("docker");
        step.Privileged.ShouldBeTrue();
        step.Version.ShouldBeNull();
    }

    [Fact]
    public void AddProvisioningStep_NotPrivileged()
    {
        var mgr = NewManager();
        mgr.AddProvisioningStep("base", "user-step", version: "2.0", privileged: false);
        var step = mgr.Config.MachineTypes["base"].Provisioning[0];
        step.Key.ShouldBe("user-step");
        step.Privileged.ShouldBeFalse();
        step.Version.ShouldBe("2.0");
    }

    // ── Config property ────────────────────────────────────────────

    [Fact]
    public void Config_ReturnsUnderlyingConfig()
    {
        var config = new VosConfig();
        var mgr = new VosConfigManager(config);
        mgr.Config.ShouldBeSameAs(config);
    }
}
