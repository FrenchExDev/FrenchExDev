using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Tests;

public class VosConfigManagerTests
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

    // ── Machine Type ────────────────────────────────────────────────

    [Fact]
    public void AddMachineType_Adds()
    {
        var mgr = NewManager();
        mgr.AddMachineType("web", "nginx/box", 4096, 4);
        mgr.Config.MachineTypes.ShouldContainKey("web");
        mgr.Config.MachineTypes["web"].Box.ShouldBe("nginx/box");
        mgr.Config.MachineTypes["web"].Provider!.Memory.ShouldBe(4096);
    }

    [Fact]
    public void AddMachineType_Duplicate_Throws()
    {
        var mgr = NewManager();
        Should.Throw<InvalidOperationException>(() => mgr.AddMachineType("base", "box"));
    }

    [Fact]
    public void RemoveMachineType_Removes()
    {
        var mgr = NewManager();
        mgr.AddMachineType("temp", "box");
        mgr.RemoveMachineType("temp");
        mgr.Config.MachineTypes.ShouldNotContainKey("temp");
    }

    [Fact]
    public void RemoveMachineType_NotFound_Throws()
    {
        Should.Throw<InvalidOperationException>(() => NewManager().RemoveMachineType("nope"));
    }

    [Fact]
    public void RemoveMachineType_Referenced_Throws()
    {
        var mgr = NewManager();
        mgr.AddMachine("m", "base");
        Should.Throw<InvalidOperationException>(() => mgr.RemoveMachineType("base"));
    }

    [Fact]
    public void SetMachineTypeProperty_ModifiesInPlace()
    {
        var mgr = NewManager();
        mgr.SetMachineTypeProperty("base", mt => mt.Provider = new VosProviderConfig { Memory = 8192 });
        mgr.Config.MachineTypes["base"].Provider!.Memory.ShouldBe(8192);
    }

    // ── Machine ─────────────────────────────────────────────────────

    [Fact]
    public void AddMachine_CreatesInstances()
    {
        var mgr = NewManager();
        mgr.AddMachine("web", "base", 3);
        mgr.Config.Machines["web"].Instances.Count.ShouldBe(3);
        mgr.Config.Machines["web"].Instances[0].Name.ShouldBe("web-01");
        mgr.Config.Machines["web"].Instances[2].Name.ShouldBe("web-03");
    }

    [Fact]
    public void AddMachine_BadType_Throws()
    {
        Should.Throw<InvalidOperationException>(() => NewManager().AddMachine("x", "nonexistent"));
    }

    [Fact]
    public void AddMachine_Duplicate_Throws()
    {
        var mgr = NewManager();
        mgr.AddMachine("m", "base");
        Should.Throw<InvalidOperationException>(() => mgr.AddMachine("m", "base"));
    }

    [Fact]
    public void RemoveMachine_Removes()
    {
        var mgr = NewManager();
        mgr.AddMachine("m", "base");
        mgr.RemoveMachine("m");
        mgr.Config.Machines.ShouldNotContainKey("m");
    }

    [Fact]
    public void EnableDisableMachine()
    {
        var mgr = NewManager();
        mgr.AddMachine("m", "base");
        mgr.DisableMachine("m");
        mgr.Config.Machines["m"].IsEnabled.ShouldBeFalse();
        mgr.EnableMachine("m");
        mgr.Config.Machines["m"].IsEnabled.ShouldBeTrue();
    }

    // ── Instance ────────────────────────────────────────────────────

    [Fact]
    public void AddInstance_Adds()
    {
        var mgr = NewManager();
        mgr.AddMachine("m", "base");
        mgr.AddInstance("m", "custom-01", "10.0.0.1", 4096);
        mgr.Config.Machines["m"].Instances.Count.ShouldBe(2);
        mgr.Config.Machines["m"].Instances.Last().Ip.ShouldBe("10.0.0.1");
    }

    [Fact]
    public void AddInstance_Duplicate_Throws()
    {
        var mgr = NewManager();
        mgr.AddMachine("m", "base");
        Should.Throw<InvalidOperationException>(() => mgr.AddInstance("m", "m-01"));
    }

    [Fact]
    public void RemoveInstance_Removes()
    {
        var mgr = NewManager();
        mgr.AddMachine("m", "base");
        mgr.RemoveInstance("m", "m-01");
        mgr.Config.Machines["m"].Instances.Count.ShouldBe(0);
    }

    // ── VBoxManage ──────────────────────────────────────────────────

    [Fact]
    public void AddVboxManageCommand_Adds()
    {
        var mgr = NewManager();
        mgr.AddVboxManageCommand("base", new() { "modifyvm", "{{ .Name }}", "--memory", "256" });
        mgr.ListVboxManageCommands("base").Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveVboxManageCommand_Removes()
    {
        var mgr = NewManager();
        mgr.AddVboxManageCommand("base", new() { "cmd1" });
        mgr.AddVboxManageCommand("base", new() { "cmd2" });
        mgr.RemoveVboxManageCommand("base", 0);
        mgr.ListVboxManageCommands("base").Count.ShouldBe(1);
        mgr.ListVboxManageCommands("base")[0][0].ShouldBe("cmd2");
    }

    [Fact]
    public void ClearVboxManageCommands_Clears()
    {
        var mgr = NewManager();
        mgr.AddVboxManageCommand("base", new() { "cmd" });
        mgr.ClearVboxManageCommands("base");
        mgr.ListVboxManageCommands("base").Count.ShouldBe(0);
    }

    // ── Shared Folders / Plugins / Provisioning ─────────────────────

    [Fact]
    public void AddSharedFolder()
    {
        var mgr = NewManager();
        mgr.AddSharedFolder("base", "/host", "/guest", "nfs");
        mgr.Config.MachineTypes["base"].SharedFolders.Count.ShouldBe(1);
    }

    [Fact]
    public void AddPlugin()
    {
        var mgr = NewManager();
        mgr.AddPlugin("base", "vagrant-hostmanager");
        mgr.Config.MachineTypes["base"].Plugins.ShouldContain("vagrant-hostmanager");
        // Duplicate should not add
        mgr.AddPlugin("base", "vagrant-hostmanager");
        mgr.Config.MachineTypes["base"].Plugins.Count(p => p == "vagrant-hostmanager").ShouldBe(1);
    }

    [Fact]
    public void AddProvisioningStep()
    {
        var mgr = NewManager();
        mgr.AddProvisioningStep("base", "docker", "1.0");
        mgr.Config.MachineTypes["base"].Provisioning.Count.ShouldBe(1);
        mgr.Config.MachineTypes["base"].Provisioning[0].Key.ShouldBe("docker");
    }
}

public class NetworkGeneratorTests
{
    [Fact]
    public void Generate_AssignsIPs()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["web"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new()
                    {
                        new VosInstance { Name = "web-01" },
                        new VosInstance { Name = "web-02" },
                        new VosInstance { Name = "web-03" }
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        var assigned = NetworkGenerator.Generate(config, "192.168.56.0/24");
        assigned.ShouldBe(3);
        config.Machines["web"].Instances[0].Ip.ShouldBe("192.168.56.10");
        config.Machines["web"].Instances[1].Ip.ShouldBe("192.168.56.11");
        config.Machines["web"].Instances[2].Ip.ShouldBe("192.168.56.12");
    }

    [Fact]
    public void Generate_SetsHostnames()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = new() { new VosInstance { Name = "vm-01" } }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.Generate(config);
        config.Machines["m"].Instances[0].Hostname.ShouldBe("vm-01.local");
    }

    [Fact]
    public void Generate_SkipsExistingIPs()
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
                        new VosInstance { Name = "a", Ip = "10.0.0.99" },
                        new VosInstance { Name = "b" }
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        var assigned = NetworkGenerator.Generate(config);
        assigned.ShouldBe(1);
        config.Machines["m"].Instances[0].Ip.ShouldBe("10.0.0.99"); // preserved
        config.Machines["m"].Instances[1].Ip.ShouldBe("192.168.56.10"); // first available
    }

    [Fact]
    public void Generate_SkipsConflictingIPs()
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
                        new VosInstance { Name = "a", Ip = "192.168.56.10" }, // manually set to .10
                        new VosInstance { Name = "b" },                       // should skip .10, get .11
                        new VosInstance { Name = "c" }                        // should get .12
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.Generate(config);
        config.Machines["m"].Instances[0].Ip.ShouldBe("192.168.56.10");
        config.Machines["m"].Instances[1].Ip.ShouldBe("192.168.56.11"); // .10 was taken
        config.Machines["m"].Instances[2].Ip.ShouldBe("192.168.56.12");
    }

    [Fact]
    public void Generate_RespectsStartAt()
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

        NetworkGenerator.Generate(config, startAt: 100);
        config.Machines["m"].Instances[0].Ip.ShouldBe("192.168.56.100");
    }

    [Fact]
    public void ValidateNoConflicts_FindsDuplicates()
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
                        new VosInstance { Name = "a", Ip = "10.0.0.1" },
                        new VosInstance { Name = "b", Ip = "10.0.0.1" }
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        var conflicts = NetworkGenerator.ValidateNoConflicts(config);
        conflicts.Count.ShouldBe(1);
        conflicts[0].ShouldContain("10.0.0.1");
    }

    [Fact]
    public void ValidateNoConflicts_NoConflicts()
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
                        new VosInstance { Name = "a", Ip = "10.0.0.1" },
                        new VosInstance { Name = "b", Ip = "10.0.0.2" }
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.ValidateNoConflicts(config).Count.ShouldBe(0);
    }

    [Fact]
    public void Generate_SubnetExhausted_Throws()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = Enumerable.Range(0, 250).Select(i => new VosInstance { Name = $"vm-{i}" }).ToList()
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        Should.Throw<InvalidOperationException>(() => NetworkGenerator.Generate(config, "192.168.56.0/24", startAt: 10));
    }

    [Fact]
    public void Generate_SmallSubnet_RespectsMaxHost()
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

        // /26 = 62 hosts max
        NetworkGenerator.Generate(config, "192.168.56.0/26", startAt: 2);
        config.Machines["m"].Instances[0].Ip.ShouldBe("192.168.56.2");
    }

    [Fact]
    public void Generate_SkipsDisabledMachines()
    {
        var config = new VosConfig
        {
            Machines = new()
            {
                ["m"] = new VosMachine
                {
                    MachineTypeName = "t",
                    IsEnabled = false,
                    Instances = new() { new VosInstance { Name = "a" } }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        NetworkGenerator.Generate(config).ShouldBe(0);
    }

    [Fact]
    public void Show_ReturnsAllInstances()
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
                        new VosInstance { Name = "a", Ip = "10.0.0.1", Hostname = "a.local" },
                        new VosInstance { Name = "b" }
                    }
                }
            },
            MachineTypes = new() { ["t"] = new VosMachineType() }
        };

        var results = NetworkGenerator.Show(config);
        results.Count.ShouldBe(2);
        results[0].Name.ShouldBe("a");
        results[0].Ip.ShouldBe("10.0.0.1");
    }

    [Fact]
    public void Generate_InvalidSubnet_Throws()
    {
        Should.Throw<ArgumentException>(() => NetworkGenerator.Generate(new VosConfig(), "not-an-ip"));
    }
}
