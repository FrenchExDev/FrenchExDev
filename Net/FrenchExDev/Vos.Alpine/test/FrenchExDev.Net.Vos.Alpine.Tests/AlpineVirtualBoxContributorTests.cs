using FrenchExDev.Net.Vos.Alpine;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Alpine.Tests;

public class AlpineVirtualBoxContributorTests
{
    [Fact]
    public void Contribute_SetsBoxName()
    {
        var mt = new VosMachineType();
        new AlpineVirtualBoxContributor().Contribute(mt);
        mt.Box.ShouldNotBeNull();
        mt.Box.ShouldContain("alpine");
    }

    [Fact]
    public void Contribute_SetsVirtualBoxProvider()
    {
        var mt = new VosMachineType();
        new AlpineVirtualBoxContributor().Contribute(mt);
        mt.Provider.ShouldNotBeNull();
        mt.Provider!.Type.ShouldBe("virtualbox");
        mt.Provider.Memory.ShouldBe(2048);
        mt.Provider.Cpus.ShouldBe(2);
        mt.Provider.LinkedClones.ShouldBeTrue();
    }

    [Fact]
    public void Contribute_AddsVBoxManageCommands()
    {
        var mt = new VosMachineType();
        new AlpineVirtualBoxContributor().Contribute(mt);
        mt.Provider!.VboxManage.Count.ShouldBeGreaterThan(5);
        mt.Provider.VboxManage.ShouldContain(c => c.Contains("--nested-hw-virt"));
        mt.Provider.VboxManage.ShouldContain(c => c.Contains("--hostiocache"));
        mt.Provider.VboxManage.ShouldContain(c => c.Contains("--nicpromisc2"));
    }

    [Fact]
    public void Contribute_AddsPlugins()
    {
        var mt = new VosMachineType();
        new AlpineVirtualBoxContributor().Contribute(mt);
        mt.Plugins.ShouldContain("vagrant-hostmanager");
        mt.Plugins.ShouldContain("vagrant-vbguest");
    }

    [Fact]
    public void Contribute_SetsAlpineVersionVariable()
    {
        var mt = new VosMachineType();
        new AlpineVirtualBoxContributor("3.20").Contribute(mt);
        mt.Variables["ALPINE_VERSION"].ShouldBe("3.20");
    }

    [Fact]
    public void Contribute_DoesNotOverrideExistingBox()
    {
        var mt = new VosMachineType { Box = "custom/box" };
        new AlpineVirtualBoxContributor().Contribute(mt);
        mt.Box.ShouldBe("custom/box");
    }

    [Fact]
    public void Contribute_SetsAlpineMemoryDefaults()
    {
        var mt = new VosMachineType { Provider = new VosProviderConfig { Memory = 4096 } };
        new AlpineVirtualBoxContributor().Contribute(mt);
        mt.Provider!.Memory.ShouldBe(2048); // contributor sets Alpine defaults
    }
}
