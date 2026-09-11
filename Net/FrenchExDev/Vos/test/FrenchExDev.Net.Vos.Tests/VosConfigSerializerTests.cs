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
    // VagrantBackend is now a real implementation — integration tests
    // require vagrant installed. Unit tests are in VosBackendTests.
}
