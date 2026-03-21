using FrenchExDev.Net.Vos.Alpine.DockerHost;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Alpine.DockerHost.Tests;

public class DockerHostContributorTests
{
    [Fact]
    public void Contribute_InheritsAlpineBase()
    {
        var mt = new VosMachineType();
        new DockerHostContributor().Contribute(mt);

        mt.Box.ShouldNotBeNull();
        mt.Provider.ShouldNotBeNull();
        mt.Provider!.Type.ShouldBe("virtualbox");
        mt.Plugins.ShouldContain("vagrant-hostmanager");
    }

    [Fact]
    public void Contribute_AddsDockerProvisioning()
    {
        var mt = new VosMachineType();
        new DockerHostContributor().Contribute(mt);

        mt.Provisioning.ShouldContain(p => p.Key == "docker");
        var docker = mt.Provisioning.First(p => p.Key == "docker");
        docker.Enabled.ShouldBeTrue();
        docker.Env.ShouldContainKey("DOCKER_COMPOSE_VERSION");
    }

    [Fact]
    public void Contribute_AddsSharedFolders()
    {
        var mt = new VosMachineType();
        new DockerHostContributor().Contribute(mt);

        mt.SharedFolders.Count.ShouldBeGreaterThanOrEqualTo(2);
        mt.SharedFolders.ShouldContain(sf => sf.GuestPath == "/opt/docker-compose");
        mt.SharedFolders.ShouldContain(sf => sf.GuestPath == "/data");
    }

    [Fact]
    public void Contribute_AddsDockerVariables()
    {
        var mt = new VosMachineType();
        new DockerHostContributor().Contribute(mt);

        mt.Variables.ShouldContainKey("DOCKER_HOST_TYPE");
        mt.Variables.ShouldContainKey("DOCKER_BRIDGE");
        mt.Variables["DOCKER_HOST_TYPE"].ShouldBe("alpine");
    }

    [Fact]
    public void MachineTypeName_IsDockerHost()
    {
        new DockerHostContributor().MachineTypeName.ShouldBe("docker-host");
    }
}
