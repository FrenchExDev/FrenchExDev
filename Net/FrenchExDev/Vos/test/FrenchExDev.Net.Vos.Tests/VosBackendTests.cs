using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.Infra.Vagrant;
using FrenchExDev.Net.Vos.Infra.Podman;

namespace FrenchExDev.Net.Vos.Tests;

public class VosBackendTests
{
    private static ResolvedInstance TestInstance => new()
    {
        Name = "test-01",
        Hostname = "test-01.local",
        Box = "test/box",
        Memory = 1024,
        Cpus = 2,
        VideoMemory = 64,
        ProviderType = "virtualbox"
    };

    [Fact]
    public async Task VagrantBackend_Up_Succeeds()
    {
        var result = await new VagrantBackend().UpAsync(TestInstance, default);
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("vagrant up");
    }

    [Fact]
    public void VagrantBackend_SupportsAllActions()
    {
        var backend = new VagrantBackend();
        backend.SupportedActions.ShouldContain("up");
        backend.SupportedActions.ShouldContain("suspend");
        backend.SupportedActions.ShouldContain("snapshot-save");
    }

    [Fact]
    public async Task PodmanBackend_Up_Succeeds()
    {
        var result = await new PodmanMachineBackend().UpAsync(TestInstance, default);
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("podman machine start");
    }

    [Fact]
    public async Task PodmanBackend_Provision_ReturnsUnsupported()
    {
        var result = await new PodmanMachineBackend().ProvisionAsync(TestInstance, default);
        result.Success.ShouldBeFalse();
        result.Error!.ShouldContain("not supported");
    }

    [Fact]
    public async Task PodmanBackend_Suspend_ReturnsUnsupported()
    {
        var result = await new PodmanMachineBackend().SuspendAsync(TestInstance, default);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task PodmanBackend_Snapshot_ReturnsUnsupported()
    {
        var result = await new PodmanMachineBackend().SnapshotSaveAsync(TestInstance, "snap1", default);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void PodmanBackend_SupportedActions_DoesNotIncludeSuspend()
    {
        var backend = new PodmanMachineBackend();
        backend.SupportedActions.ShouldNotContain("suspend");
        backend.SupportedActions.ShouldNotContain("snapshot-save");
        backend.SupportedActions.ShouldContain("up");
        backend.SupportedActions.ShouldContain("ssh");
    }
}
