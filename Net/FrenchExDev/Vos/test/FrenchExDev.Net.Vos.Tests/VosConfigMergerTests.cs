using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Tests;

public class VosConfigMergerTests
{
    private static VosConfig CreateTestConfig()
    {
        return new VosConfig
        {
            Backend = "vagrant",
            MachineTypes = new()
            {
                ["alpine-docker"] = new VosMachineType
                {
                    Box = "frenchexdev/alpine-docker",
                    Provider = new VosProviderConfig { Memory = 2048, Cpus = 2 },
                    Network = new VosNetworkConfig { Private = new VosPrivateNetwork { Ip = "192.168.56.10" } },
                    Variables = new() { ["ROLE"] = "docker" },
                    Plugins = new() { "vagrant-hostmanager" }
                }
            },
            Machines = new()
            {
                ["docker-host"] = new VosMachine
                {
                    MachineTypeName = "alpine-docker",
                    Instances = new()
                    {
                        new VosInstance { Name = "docker-01", Hostname = "docker-01.local", Ip = "192.168.56.11" },
                        new VosInstance { Name = "docker-02", Ip = "192.168.56.12", Memory = 4096 }
                    }
                }
            }
        };
    }

    [Fact]
    public void Resolve_InheritsFromMachineType()
    {
        var config = CreateTestConfig();
        var resolved = VosConfigMerger.Resolve(config, "docker-host", config.Machines["docker-host"].Instances[0]);

        resolved.Name.ShouldBe("docker-01");
        resolved.Hostname.ShouldBe("docker-01.local");
        resolved.Box.ShouldBe("frenchexdev/alpine-docker");
        resolved.Memory.ShouldBe(2048);
        resolved.Cpus.ShouldBe(2);
        resolved.Ip.ShouldBe("192.168.56.11");
        resolved.Plugins.ShouldContain("vagrant-hostmanager");
    }

    [Fact]
    public void Resolve_InstanceOverridesMemory()
    {
        var config = CreateTestConfig();
        var resolved = VosConfigMerger.Resolve(config, "docker-host", config.Machines["docker-host"].Instances[1]);

        resolved.Name.ShouldBe("docker-02");
        resolved.Memory.ShouldBe(4096); // overridden by instance
        resolved.Cpus.ShouldBe(2);     // inherited from machine type
    }

    [Fact]
    public void Resolve_InstanceIpOverridesMachineTypeIp()
    {
        var config = CreateTestConfig();
        var resolved = VosConfigMerger.Resolve(config, "docker-host", config.Machines["docker-host"].Instances[1]);

        resolved.Ip.ShouldBe("192.168.56.12"); // instance overrides
    }

    [Fact]
    public void Resolve_HostnameDefaultsToName()
    {
        var config = CreateTestConfig();
        var resolved = VosConfigMerger.Resolve(config, "docker-host", config.Machines["docker-host"].Instances[1]);

        resolved.Hostname.ShouldBe("docker-02"); // no hostname set, defaults to name
    }

    [Fact]
    public void Resolve_MergesVariables()
    {
        var config = CreateTestConfig();
        config.Machines["docker-host"].Variables = new() { ["EXTRA"] = "value" };

        var resolved = VosConfigMerger.Resolve(config, "docker-host", config.Machines["docker-host"].Instances[0]);

        resolved.Variables["ROLE"].ShouldBe("docker");  // from machine type
        resolved.Variables["EXTRA"].ShouldBe("value");  // from machine
    }

    [Fact]
    public void Resolve_MachineOverridesBox()
    {
        var config = CreateTestConfig();
        config.Machines["docker-host"].Box = "custom/box";

        var resolved = VosConfigMerger.Resolve(config, "docker-host", config.Machines["docker-host"].Instances[0]);
        resolved.Box.ShouldBe("custom/box");
    }

    [Fact]
    public void ResolveAll_ReturnsAllEnabledInstances()
    {
        var config = CreateTestConfig();
        var all = VosConfigMerger.ResolveAll(config);

        all.Count.ShouldBe(2);
        all[0].Instance.Name.ShouldBe("docker-01");
        all[1].Instance.Name.ShouldBe("docker-02");
    }

    [Fact]
    public void ResolveAll_SkipsDisabledMachines()
    {
        var config = CreateTestConfig();
        config.Machines["docker-host"].IsEnabled = false;

        var all = VosConfigMerger.ResolveAll(config);
        all.Count.ShouldBe(0);
    }

    [Fact]
    public void Resolve_ThrowsForMissingMachine()
    {
        var config = CreateTestConfig();
        Should.Throw<InvalidOperationException>(() =>
            VosConfigMerger.Resolve(config, "nonexistent", new VosInstance { Name = "x" }));
    }

    [Fact]
    public void Resolve_WithNullProviders_UsesDefaults()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["bare"] = new VosMachineType() },
            Machines = new()
            {
                ["m1"] = new VosMachine
                {
                    MachineTypeName = "bare",
                    Instances = new() { new VosInstance { Name = "i1" } }
                }
            }
        };

        var resolved = VosConfigMerger.Resolve(config, "m1", config.Machines["m1"].Instances[0]);
        resolved.ProviderType.ShouldBe("virtualbox");
        resolved.Memory.ShouldBe(1024);
        resolved.Cpus.ShouldBe(2);
    }

    [Fact]
    public void Resolve_MachineProviderOverridesTypeProvider()
    {
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["t1"] = new VosMachineType
                {
                    Provider = new VosProviderConfig { Memory = 1024, Cpus = 1, Type = "hyperv" }
                }
            },
            Machines = new()
            {
                ["m1"] = new VosMachine
                {
                    MachineTypeName = "t1",
                    Provider = new VosProviderConfig { Memory = 4096, Cpus = 8, Type = "parallels", Gui = true, VideoMemory = 128 },
                    Instances = new() { new VosInstance { Name = "i1" } }
                }
            }
        };

        var resolved = VosConfigMerger.Resolve(config, "m1", config.Machines["m1"].Instances[0]);
        resolved.Memory.ShouldBe(4096);
        resolved.Cpus.ShouldBe(8);
        resolved.ProviderType.ShouldBe("parallels");
        resolved.Gui.ShouldBeTrue();
        resolved.VideoMemory.ShouldBe(128);
    }

    [Fact]
    public void Resolve_VboxManage_MachineOverridesType()
    {
        var machineVbox = new List<List<string>> { new() { "modifyvm", "test" } };
        var config = new VosConfig
        {
            MachineTypes = new()
            {
                ["t1"] = new VosMachineType
                {
                    Provider = new VosProviderConfig { VboxManage = new() { new() { "other" } } }
                }
            },
            Machines = new()
            {
                ["m1"] = new VosMachine
                {
                    MachineTypeName = "t1",
                    Provider = new VosProviderConfig { VboxManage = machineVbox },
                    Instances = new() { new VosInstance { Name = "i1" } }
                }
            }
        };

        var resolved = VosConfigMerger.Resolve(config, "m1", config.Machines["m1"].Instances[0]);
        resolved.VboxManage.ShouldBe(machineVbox);
    }

    [Fact]
    public void Resolve_ThrowsForMissingMachineType()
    {
        var config = CreateTestConfig();
        config.Machines["bad"] = new VosMachine { MachineTypeName = "nonexistent", Instances = new() { new VosInstance { Name = "x" } } };

        Should.Throw<InvalidOperationException>(() =>
            VosConfigMerger.Resolve(config, "bad", config.Machines["bad"].Instances[0]));
    }
}
