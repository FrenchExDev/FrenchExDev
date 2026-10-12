using FrenchExDev.Net.Packer.Alpine;
using FrenchExDev.Net.Packer.Bundle;

namespace FrenchExDev.Net.Packer.Alpine.Tests;

public class AlpineBaseContributorTests
{
    [Fact]
    public void Contribute_AddsRequiredPlugins()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        var config = bundle.Config.Build();
        config.RequiredPlugins.ShouldContainKey("virtualbox");
        config.RequiredVersion.ShouldNotBeNull();
    }

    [Fact]
    public void Contribute_AddsVariables()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        bundle.Variables.Count.ShouldBeGreaterThan(5);
        bundle.Variables.ShouldContain(v => v.Name == "alpine_version");
        bundle.Variables.ShouldContain(v => v.Name == "cpus");
        bundle.Variables.ShouldContain(v => v.Name == "ssh_password" && v.Sensitive == true);
    }

    [Fact]
    public void Contribute_AddsLocals()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        bundle.Locals.Count.ShouldBeGreaterThan(0);
        bundle.Locals.ShouldContain(l => l.Name == "vm_name");
        bundle.Locals.ShouldContain(l => l.Name == "iso_url");
    }

    [Fact]
    public void Contribute_AddsVirtualBoxIsoSource()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        bundle.Sources.Count.ShouldBe(1);
        bundle.Sources[0].Type.ShouldBe("virtualbox-iso");
        bundle.Sources[0].Name.ShouldBe("alpine");
        bundle.Sources[0].Arguments.ShouldContainKey("vm_name");
        bundle.Sources[0].Arguments.ShouldContainKey("boot_command");
        bundle.Sources[0].Arguments.ShouldContainKey("vboxmanage");
    }

    [Fact]
    public void Contribute_AddsBuildWithSourceAndProvisioner()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        var build = bundle.Build.Build();
        build.Sources.ShouldContain("source.virtualbox-iso.alpine");
        build.Provisioners.Count.ShouldBeGreaterThan(0);
        build.Provisioners[0].Type.ShouldBe("shell");
    }

    [Fact]
    public void Contribute_Adds11Scripts()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        bundle.Scripts.Count().ShouldBe(11);
        bundle.Scripts.ShouldContain(s => s.Name == "00base");
        bundle.Scripts.ShouldContain(s => s.Name == "01alpine");
        bundle.Scripts.ShouldContain(s => s.Name == "03vagrant");
        bundle.Scripts.ShouldContain(s => s.Name == "99minimize");
        bundle.Scripts.ShouldContain(s => s.Name == "99reboot");
    }

    [Fact]
    public void Contribute_ScriptsAreSorted()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        var scriptNames = bundle.Scripts.Select(s => s.Name).ToList();
        scriptNames.ShouldBe(scriptNames.OrderBy(n => n).ToList());
    }

    [Fact]
    public void Contribute_AddsAnswerFile()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        bundle.HttpFiles.ShouldContain(f => f.Name == "answers");
        var answers = bundle.HttpFiles.First(f => f.Name == "answers");
        answers.Content.ShouldContain("KEYMAPOPTS");
        answers.Content.ShouldContain("DISKOPTS");
    }

    [Fact]
    public void Contribute_AddsSshKey()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        bundle.HttpFiles.ShouldContain(f => f.Name == "ssh.keys");
    }

    [Fact]
    public void Contribute_AddsVagrantFiles()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        bundle.VagrantFiles.ShouldContain(f => f.Name == "metadata");
        bundle.VagrantFiles.ShouldContain(f => f.Name == "info");
    }

    [Fact]
    public void Contribute_SetsVagrantfileConfig()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        bundle.Vagrantfile.Cpus.ShouldBe(4);
        bundle.Vagrantfile.Memory.ShouldBe(256);
        bundle.Vagrantfile.Provider.ShouldBe("virtualbox");
    }

    [Fact]
    public void Contribute_AddsEnvTemplate()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        bundle.EnvTemplate.Variables.ShouldContain(v => v.Key == "ALPINE_VERSION");
        bundle.EnvTemplate.Variables.ShouldContain(v => v.Key == "BOX_VERSION");
    }

    [Fact]
    public void Contribute_WithCustomConfig_UsesCustomValues()
    {
        var config = new AlpinePackerConfig { AlpineVersion = "3.20", Cpus = 8, Memory = 512 };
        var bundle = new PackerBundle();
        new AlpineBaseContributor(config).Contribute(bundle);

        bundle.Variables.First(v => v.Name == "alpine_version").Default.ShouldBe("3.20");
        bundle.Variables.First(v => v.Name == "cpus").Default.ShouldBe(8);
        bundle.Vagrantfile.Cpus.ShouldBe(8);
        bundle.Vagrantfile.Memory.ShouldBe(512);
    }

    [Fact]
    public async Task FullPipeline_WritesToDisk()
    {
        var bundle = new PackerBundle();
        new AlpineBaseContributor().Contribute(bundle);

        var outputDir = Path.Combine(Path.GetTempPath(), $"alpine-test-{Guid.NewGuid():N}");
        try
        {
            await new PackerBundleWriter().WriteAsync(bundle, outputDir);

            File.Exists(Path.Combine(outputDir, "packer.pkr.hcl")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "variables.pkr.hcl")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "sources.pkr.hcl")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "build.pkr.hcl")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "scripts", "00base.sh")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "http", "answers")).ShouldBeTrue();

            var sourcesHcl = File.ReadAllText(Path.Combine(outputDir, "sources.pkr.hcl"));
            sourcesHcl.ShouldContain("source \"virtualbox-iso\" \"alpine\"");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }
}

public class AlpineAnswerFileGeneratorTests
{
    [Fact]
    public void Generate_ContainsAllFields()
    {
        var generator = new AlpineAnswerFileGenerator();
        var content = generator.Generate(new AlpineAnswerFileConfig());

        content.ShouldContain("KEYMAPOPTS");
        content.ShouldContain("HOSTNAMEOPTS");
        content.ShouldContain("INTERFACESOPTS");
        content.ShouldContain("TIMEZONEOPTS");
        content.ShouldContain("SSHDOPTS");
        content.ShouldContain("DISKOPTS");
        content.ShouldContain("ERASE_DISKS");
    }

    [Fact]
    public void Generate_UsesCustomValues()
    {
        var config = new AlpineAnswerFileConfig { HostName = "myhost", TimeZone = "Europe/Paris" };
        var content = new AlpineAnswerFileGenerator().Generate(config);

        content.ShouldContain("HOSTNAMEOPTS=myhost");
        content.ShouldContain("Europe/Paris");
    }
}

public class AlpinePackerConfigTests
{
    [Fact]
    public void IsoDownloadUrl_ComputedCorrectly()
    {
        var config = new AlpinePackerConfig { AlpineVersion = "3.21", Arch = "x86_64", Flavor = "virt" };
        config.IsoDownloadUrl.ShouldContain("alpine/v3.21/releases/x86_64/alpine-virt-3.21.0-x86_64.iso");
    }

    [Fact]
    public void VmName_ComputedCorrectly()
    {
        var config = new AlpinePackerConfig { AlpineVersion = "3.21", Flavor = "virt" };
        config.VmName.ShouldBe("alpine-3.21-virt");
    }
}
