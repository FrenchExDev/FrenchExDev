using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Tests;

public class VosConfigMergerBranchTests
{
    [Fact]
    public void Resolve_NullProviders_UsesDefaults()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType() },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        var r = VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]);
        r.ProviderType.ShouldBe("virtualbox");
        r.LinkedClones.ShouldBeTrue();
        r.Gui.ShouldBeFalse();
        r.Memory.ShouldBe(1024);
        r.Cpus.ShouldBe(2);
    }

    [Fact]
    public void Resolve_LinkedClones_OneFalse()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { LinkedClones = false } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).LinkedClones.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_Gui_EitherTrue()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { Gui = true } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Gui.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_MachineProviderType_Overrides()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { Type = "hyperv" } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Provider = new VosProviderConfig { Type = "parallels" }, Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).ProviderType.ShouldBe("parallels");
    }

    [Fact]
    public void Resolve_VboxManage_MachineOverrides()
    {
        var machineVbox = new List<List<string>> { new() { "modifyvm", "test" } };
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { VboxManage = new() { new() { "other" } } } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Provider = new VosProviderConfig { VboxManage = machineVbox }, Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).VboxManage.ShouldBe(machineVbox);
    }

    [Fact]
    public void Resolve_NoNetwork_NullIpMac()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType() },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        var r = VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]);
        r.Ip.ShouldBeNull();
        r.Mac.ShouldBeNull();
    }

    [Fact]
    public void Resolve_Mac_InstanceOverrides()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Network = new VosNetworkConfig { Private = new VosPrivateNetwork { Mac = "AA" } } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i", Mac = "BB" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Mac.ShouldBe("BB");
    }

    [Fact]
    public void Resolve_SharedFolders_MachineOverrides()
    {
        var mf = new List<VosSharedFolder> { new() { HostPath = "/a", GuestPath = "/b" } };
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { SharedFolders = new() { new() { HostPath = "/x", GuestPath = "/y" } } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", SharedFolders = mf, Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).SharedFolders.ShouldBe(mf);
    }

    [Fact]
    public void Resolve_Provisioning_MachineOverrides()
    {
        var ms = new List<VosProvisioningStep> { new() { Key = "custom" } };
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provisioning = new() { new() { Key = "base" } } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Provisioning = ms, Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Provisioning.ShouldBe(ms);
    }

    [Fact]
    public void Resolve_VideoMemory_MachineOverrides()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { VideoMemory = 32 } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Provider = new VosProviderConfig { VideoMemory = 128 }, Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).VideoMemory.ShouldBe(128);
    }


    [Fact]
    public void Resolve_MachineProvider_Memory_NonDefault()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { Memory = 512, Cpus = 1, VideoMemory = 32 } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Provider = new VosProviderConfig { Memory = 8192, Cpus = 16, VideoMemory = 256 }, Instances = new() { new VosInstance { Name = "i" } } } }
        };
        var r = VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]);
        r.Memory.ShouldBe(8192);
        r.Cpus.ShouldBe(16);
        r.VideoMemory.ShouldBe(256);
    }

    [Fact]
    public void Resolve_TypeProvider_FallsThrough_WhenMachineIsDefault()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { Memory = 4096, Cpus = 8, VideoMemory = 128 } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        var r = VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]);
        r.Memory.ShouldBe(4096);
        r.Cpus.ShouldBe(8);
        r.VideoMemory.ShouldBe(128);
    }

    [Fact]
    public void Resolve_BothGuiFalse_ReturnsFalse()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { Gui = false } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Provider = new VosProviderConfig { Gui = false }, Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Gui.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_MachineGuiTrue_TypeFalse_ReturnsTrue()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { Gui = false } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Provider = new VosProviderConfig { Gui = true }, Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Gui.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_VboxManage_TypeFallback_WhenMachineEmpty()
    {
        var typeVbox = new List<List<string>> { new() { "cmd" } };
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { VboxManage = typeVbox } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).VboxManage.ShouldBe(typeVbox);
    }

    [Fact]
    public void Resolve_InstanceCpus_OverridesProvider()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { Cpus = 4 } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i", Cpus = 32 } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Cpus.ShouldBe(32);
    }

    [Fact]
    public void Resolve_Mac_Null_FallsToMachineType()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Network = new VosNetworkConfig { Private = new VosPrivateNetwork { Mac = "AA" } } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Mac.ShouldBe("AA");
    }
}
