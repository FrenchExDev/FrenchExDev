using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.Infra.FileSystem;

namespace FrenchExDev.Net.Vos.Tests;

public class FinalBranchTests
{
    [Fact]
    public void ResolveAll_EmptyMachines()
    {
        var config = new VosConfig { MachineTypes = new() { ["t"] = new VosMachineType() } };
        VosConfigMerger.ResolveAll(config).Count.ShouldBe(0);
    }

    [Fact]
    public void ResolveAll_MultipleInstances()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType() },
            Machines = new()
            {
                ["m1"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "a" }, new VosInstance { Name = "b" } } },
                ["m2"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "c" } } }
            }
        };
        VosConfigMerger.ResolveAll(config).Count.ShouldBe(3);
    }

    [Fact]
    public void MergeDictionaries_BothNull_ReturnsEmpty()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType() },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Variables.Count.ShouldBe(0);
    }

    [Fact]
    public void MergeDictionaries_OnlyBase()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Variables = new() { ["A"] = "1" } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        var r = VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]);
        r.Variables["A"].ShouldBe("1");
    }

    [Fact]
    public void MergeDictionaries_OnlyOverride()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType() },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Variables = new() { ["B"] = "2" }, Instances = new() { new VosInstance { Name = "i" } } } }
        };
        var r = VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]);
        r.Variables["B"].ShouldBe("2");
    }

    [Fact]
    public async Task Serializer_EmptyConfig_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vos-empty-{Guid.NewGuid():N}.yaml");
        try
        {
            var ser = new VosConfigSerializer();
            await ser.SerializeAsync(new VosConfig(), path);
            var loaded = await ser.DeserializeAsync(path);
            loaded.Backend.ShouldBe("vagrant");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void VosActionResult_Properties()
    {
        var r = new VosActionResult(true, "output", "error");
        r.Success.ShouldBeTrue();
        r.Output.ShouldBe("output");
        r.Error.ShouldBe("error");
    }

    [Fact]
    public void VosError_Types()
    {
        var cf = new VosError.CommandFailed("msg", 1);
        cf.Message.ShouldBe("msg");
        cf.ExitCode.ShouldBe(1);

        var ua = new VosError.UnsupportedAction("suspend", "podman");
        ua.Action.ShouldBe("suspend");
        ua.Backend.ShouldBe("podman");

        var ce = new VosError.ConfigError("bad");
        ce.Message.ShouldBe("bad");
    }

    [Fact]
    public void VosConfig_Defaults()
    {
        var c = new VosConfig();
        c.Backend.ShouldBe("vagrant");
        c.MachineTypes.Count.ShouldBe(0);
        c.Machines.Count.ShouldBe(0);
    }

    [Fact]
    public void VosMachineType_Defaults()
    {
        var mt = new VosMachineType();
        mt.IsEnabled.ShouldBeTrue();
        mt.Provisioning.Count.ShouldBe(0);
        mt.Variables.Count.ShouldBe(0);
        mt.SharedFolders.Count.ShouldBe(0);
        mt.Plugins.Count.ShouldBe(0);
    }

    [Fact]
    public void VosMachine_Defaults()
    {
        var m = new VosMachine { MachineTypeName = "t" };
        m.IsEnabled.ShouldBeTrue();
        m.Instances.Count.ShouldBe(0);
    }

    [Fact]
    public void VosProviderConfig_Defaults()
    {
        var p = new VosProviderConfig();
        p.Type.ShouldBe("virtualbox");
        p.Memory.ShouldBe(1024);
        p.Cpus.ShouldBe(2);
    }

    [Fact]
    public void VosProvisioningStep_Defaults()
    {
        var s = new VosProvisioningStep { Key = "test" };
        s.Enabled.ShouldBeTrue();
        s.Privileged.ShouldBeTrue();
        s.ReloadBefore.ShouldBeFalse();
        s.ReloadAfter.ShouldBeFalse();
    }

    [Fact]
    public void VosSharedFolder_Properties()
    {
        var sf = new VosSharedFolder { HostPath = "/a", GuestPath = "/b", Type = "nfs", Disabled = true };
        sf.HostPath.ShouldBe("/a");
        sf.Disabled.ShouldBeTrue();
    }

    [Fact]
    public void VosNetworkConfig_Properties()
    {
        var n = new VosNetworkConfig { Private = new VosPrivateNetwork { Ip = "10.0.0.1" }, Public = new VosPublicNetwork { Bridge = "br0" } };
        n.Private!.Ip.ShouldBe("10.0.0.1");
        n.Public!.Bridge.ShouldBe("br0");
    }

    [Fact]
    public void IMachineTypeContributor_Contract()
    {
        var contributor = new TestContributor();
        contributor.MachineTypeName.ShouldBe("test");
        var mt = new VosMachineType();
        contributor.Contribute(mt);
        mt.Variables["contributed"].ShouldBe("true");
    }

    [Fact]
    public void Resolve_Network_NotNull_PrivateNull()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Network = new VosNetworkConfig() } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        var r = VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]);
        r.Ip.ShouldBeNull();
    }

    [Fact]
    public void Resolve_Network_PrivateNotNull_NoIp()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Network = new VosNetworkConfig { Private = new VosPrivateNetwork() } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Ip.ShouldBeNull();
    }

    [Fact]
    public void Resolve_LinkedClones_MachineFalse_TypeTrue()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Provider = new VosProviderConfig { LinkedClones = true } } },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Provider = new VosProviderConfig { LinkedClones = false }, Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).LinkedClones.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_BothBoxNull()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType() },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Box.ShouldBeNull();
    }

    [Fact]
    public void Resolve_Hostname_Explicit()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType() },
            Machines = new() { ["m"] = new VosMachine { MachineTypeName = "t", Instances = new() { new VosInstance { Name = "i", Hostname = "h" } } } }
        };
        VosConfigMerger.Resolve(config, "m", config.Machines["m"].Instances[0]).Hostname.ShouldBe("h");
    }

    private sealed class TestContributor : IMachineTypeContributor
    {
        public string MachineTypeName => "test";
        public void Contribute(VosMachineType machineType) => machineType.Variables["contributed"] = "true";
    }
}
