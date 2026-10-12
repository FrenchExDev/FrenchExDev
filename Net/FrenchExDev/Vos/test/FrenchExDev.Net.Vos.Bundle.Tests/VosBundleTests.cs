using FrenchExDev.Net.Vos.Bundle;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Bundle.Tests;

public class VosBundleTests
{
    [Fact]
    public void New_bundle_has_empty_config_and_no_files()
    {
        var bundle = new VosBundle();
        bundle.Config.ShouldNotBeNull();
        bundle.Config.MachineTypes.ShouldBeEmpty();
        bundle.Files.ShouldBeEmpty();
    }

    [Fact]
    public void AddProvisioningScript_adds_file_to_provisioning_directory()
    {
        var bundle = new VosBundle();
        bundle.AddProvisioningScript("install-docker", "1.0", "#!/bin/sh\napk add docker");

        bundle.Files.Count.ShouldBe(1);
        bundle.ProvisioningScripts.Count().ShouldBe(1);
        var file = bundle.Files.Values.First();
        file.Directory.ShouldBe("provisioning/1.0");
        file.FileName.ShouldBe("install-docker.sh");
        file.Extension.ShouldBe("sh");
        file.Content.ShouldContain("apk add docker");
    }

    [Fact]
    public void AddProvisioningScript_with_custom_extension()
    {
        var bundle = new VosBundle();
        bundle.AddProvisioningScript("setup", "2.0", "Write-Host 'hi'", extension: "ps1");

        var file = bundle.Files.Values.First();
        file.FileName.ShouldBe("setup.ps1");
        file.Extension.ShouldBe("ps1");
    }

    [Fact]
    public void AddFile_adds_to_shared_files()
    {
        var bundle = new VosBundle();
        bundle.AddFile("files", "daemon.json", "json", "{\"storage-driver\":\"overlay2\"}");

        bundle.SharedFiles.Count().ShouldBe(1);
        bundle.Files.Values.First().Content.ShouldContain("overlay2");
    }

    [Fact]
    public void Apply_executes_contributors_in_order()
    {
        var bundle = new VosBundle();
        bundle.Apply(
            new TestContributor("first", "alpine/3.21"),
            new TestContributor("second", "ubuntu/jammy64"));

        bundle.Config.MachineTypes.Count.ShouldBe(2);
        bundle.Config.MachineTypes.ShouldContainKey("first");
        bundle.Config.MachineTypes.ShouldContainKey("second");
    }

    [Fact]
    public void LocalOverrides_is_null_by_default()
    {
        new VosBundle().LocalOverrides.ShouldBeNull();
    }

    [Fact]
    public void LocalOverrides_can_be_set()
    {
        var bundle = new VosBundle
        {
            LocalOverrides = new VosConfig { Backend = "podman" }
        };
        bundle.LocalOverrides!.Backend.ShouldBe("podman");
    }

    private sealed class TestContributor(string name, string box) : IVosBundleContributor
    {
        public void Contribute(VosBundle bundle)
        {
            bundle.Config.MachineTypes[name] = new VosMachineType { Box = box };
        }
    }
}
