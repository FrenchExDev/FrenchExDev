using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.Infra.FileSystem;

namespace FrenchExDev.Net.Vos.Tests;

public class VosConfigSerializerTests
{
    [Fact]
    public async Task RoundTrip_SerializeDeserialize()
    {
        var config = new VosConfig
        {
            Backend = "vagrant",
            MachineTypes = new()
            {
                ["alpine"] = new VosMachineType
                {
                    Box = "test/box",
                    Provider = new VosProviderConfig { Memory = 2048, Cpus = 4 },
                    Plugins = new() { "vagrant-hostmanager" }
                }
            },
            Machines = new()
            {
                ["web"] = new VosMachine
                {
                    MachineTypeName = "alpine",
                    Instances = new() { new VosInstance { Name = "web-01" } }
                }
            }
        };

        var path = Path.Combine(Path.GetTempPath(), $"vos-config-{Guid.NewGuid():N}.yaml");
        try
        {
            var serializer = new VosConfigSerializer();
            await serializer.SerializeAsync(config, path);

            File.Exists(path).ShouldBeTrue();
            var yaml = await File.ReadAllTextAsync(path);
            yaml.ShouldContain("backend");

            var loaded = await serializer.DeserializeAsync(path);
            loaded.Backend.ShouldBe("vagrant");
            loaded.MachineTypes.ShouldContainKey("alpine");
            loaded.Machines.ShouldContainKey("web");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

public class VosInfraTests
{
    private static ResolvedInstance TestInstance => new()
    {
        Name = "test-01",
        Hostname = "test-01.local",
        Memory = 1024,
        Cpus = 2,
        VideoMemory = 64,
        ProviderType = "virtualbox"
    };

    [Fact]
    public async Task VagrantBackend_AllActions_ReturnResults()
    {
        var backend = new Infra.Vagrant.VagrantBackend();

        (await backend.HaltAsync(TestInstance, false, default)).Success.ShouldBeTrue();
        (await backend.DestroyAsync(TestInstance, true, default)).Success.ShouldBeTrue();
        (await backend.ReloadAsync(TestInstance, default)).Success.ShouldBeTrue();
        (await backend.ProvisionAsync(TestInstance, default)).Success.ShouldBeTrue();
        (await backend.StatusAsync(TestInstance, default)).Success.ShouldBeTrue();
        (await backend.SshAsync(TestInstance, default)).Success.ShouldBeTrue();
        (await backend.SshCommandAsync(TestInstance, "ls", default)).Success.ShouldBeTrue();
        (await backend.SuspendAsync(TestInstance, default)).Success.ShouldBeTrue();
        (await backend.ResumeAsync(TestInstance, default)).Success.ShouldBeTrue();
        (await backend.SnapshotSaveAsync(TestInstance, "s1", default)).Success.ShouldBeTrue();
        (await backend.SnapshotRestoreAsync(TestInstance, "s1", default)).Success.ShouldBeTrue();
    }

    [Fact]
    public async Task PodmanBackend_AllUnsupportedActions_ReturnFailure()
    {
        var backend = new Infra.Podman.PodmanMachineBackend();

        (await backend.HaltAsync(TestInstance, false, default)).Success.ShouldBeTrue();
        (await backend.DestroyAsync(TestInstance, true, default)).Success.ShouldBeTrue();
        (await backend.ReloadAsync(TestInstance, default)).Success.ShouldBeTrue();
        (await backend.StatusAsync(TestInstance, default)).Success.ShouldBeTrue();
        (await backend.SshAsync(TestInstance, default)).Success.ShouldBeTrue();
        (await backend.SshCommandAsync(TestInstance, "ls", default)).Success.ShouldBeTrue();
        (await backend.ResumeAsync(TestInstance, default)).Success.ShouldBeFalse();
        (await backend.SnapshotRestoreAsync(TestInstance, "s1", default)).Success.ShouldBeFalse();
    }

    [Fact]
    public void VagrantBackend_Name()
    {
        new Infra.Vagrant.VagrantBackend().Name.ShouldBe("vagrant");
    }

    [Fact]
    public void PodmanBackend_Name()
    {
        new Infra.Podman.PodmanMachineBackend().Name.ShouldBe("podman");
    }
}
