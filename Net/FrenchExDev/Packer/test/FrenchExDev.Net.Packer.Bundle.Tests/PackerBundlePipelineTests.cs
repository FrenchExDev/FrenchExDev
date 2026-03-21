using FrenchExDev.Net.Packer.Bundle;
using FrenchExDev.Net.Packer.Bundle.Hcl2;

namespace FrenchExDev.Net.Packer.Bundle.Tests;

public class PackerBundlePipelineTests
{
    [Fact]
    public async Task FullPipeline_AlpineDockerBundle_EmitsCorrectHcl2Files()
    {
        // Arrange — simulate an Alpine+Docker contributor pipeline
        var bundle = new PackerBundle();

        // Config
        bundle.Config
            .WithRequiredVersion(">= 1.7.0")
            .WithRequiredPlugin("virtualbox", ">= 1.1.0", "github.com/hashicorp/virtualbox");

        // Variables
        bundle.Variables.Add(new PackerVariable
        {
            Name = "alpine_version",
            Type = "string",
            Default = "3.21",
            Description = "Alpine Linux version"
        });
        bundle.Variables.Add(new PackerVariable
        {
            Name = "cpus",
            Type = "number",
            Default = 4
        });

        // Locals
        bundle.Locals.Add(new PackerLocal
        {
            Name = "vm_name",
            Expression = "\"alpine-${var.alpine_version}-docker\""
        });

        // Source
        bundle.Sources.Add(new PackerSource
        {
            Type = "virtualbox-iso",
            Name = "alpine",
            Arguments = new Dictionary<string, object?>
            {
                ["vm_name"] = Hcl.Local("vm_name"),
                ["disk_size"] = 20480,
                ["headless"] = false,
                ["ssh_username"] = "root",
                ["boot_command"] = new List<string> { "<wait10>", "root<enter>" }
            }
        });

        // Build
        bundle.Build
            .WithName("alpine-docker")
            .WithSource("source.virtualbox-iso.alpine")
            .WithProvisioner(new PackerProvisioner
            {
                Type = "shell",
                Arguments = new Dictionary<string, object?>
                {
                    ["scripts"] = new List<string> { "scripts/00base.sh", "scripts/06docker.sh" }
                }
            })
            .WithPostProcessor(new PackerPostProcessor
            {
                Type = "vagrant",
                Arguments = new Dictionary<string, object?>
                {
                    ["compression_level"] = 9,
                    ["output"] = "output/alpine.box"
                }
            });

        // Companion files
        bundle.AddScript("00base", "#!/bin/sh\necho 'Hello Alpine'");
        bundle.AddScript("06docker", "#!/bin/sh\napk add docker");

        // Act — write to temp directory
        var outputDir = Path.Combine(Path.GetTempPath(), $"packer-bundle-test-{Guid.NewGuid():N}");
        try
        {
            var writer = new PackerBundleWriter();
            await writer.WriteAsync(bundle, outputDir);

            // Assert — files exist
            File.Exists(Path.Combine(outputDir, "packer.pkr.hcl")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "variables.pkr.hcl")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "locals.pkr.hcl")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "sources.pkr.hcl")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "build.pkr.hcl")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "scripts", "00base.sh")).ShouldBeTrue();
            File.Exists(Path.Combine(outputDir, "scripts", "06docker.sh")).ShouldBeTrue();

            // Assert — HCL content
            var packerHcl = File.ReadAllText(Path.Combine(outputDir, "packer.pkr.hcl"));
            packerHcl.ShouldContain("required_version");
            packerHcl.ShouldContain("required_plugins");
            packerHcl.ShouldContain("virtualbox");

            var varsHcl = File.ReadAllText(Path.Combine(outputDir, "variables.pkr.hcl"));
            varsHcl.ShouldContain("variable \"alpine_version\"");
            varsHcl.ShouldContain("type = string");
            varsHcl.ShouldContain("default = \"3.21\"");

            var localsHcl = File.ReadAllText(Path.Combine(outputDir, "locals.pkr.hcl"));
            localsHcl.ShouldContain("locals {");
            localsHcl.ShouldContain("vm_name = ");

            var sourcesHcl = File.ReadAllText(Path.Combine(outputDir, "sources.pkr.hcl"));
            sourcesHcl.ShouldContain("source \"virtualbox-iso\" \"alpine\"");
            sourcesHcl.ShouldContain("disk_size = 20480");
            sourcesHcl.ShouldContain("vm_name = local.vm_name");

            var buildHcl = File.ReadAllText(Path.Combine(outputDir, "build.pkr.hcl"));
            buildHcl.ShouldContain("build {");
            buildHcl.ShouldContain("name = \"alpine-docker\"");
            buildHcl.ShouldContain("source.virtualbox-iso.alpine");
            buildHcl.ShouldContain("provisioner \"shell\"");
            buildHcl.ShouldContain("post-processor \"vagrant\"");

            // Assert — script content
            var script = File.ReadAllText(Path.Combine(outputDir, "scripts", "06docker.sh"));
            script.ShouldContain("apk add docker");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void BundleFile_RelativePath_ComputedCorrectly()
    {
        var file = new BundleFile
        {
            Name = "06docker",
            Extension = ".sh",
            Directory = "scripts",
            Content = "#!/bin/sh"
        };

        file.RelativePath.ShouldBe("scripts/06docker.sh");
    }

    [Fact]
    public void BundleFile_RootDirectory_NoSlashPrefix()
    {
        var file = new BundleFile
        {
            Name = ".env",
            Extension = "",
            Directory = "",
            Content = "KEY=value"
        };

        file.RelativePath.ShouldBe(".env");
    }

    [Fact]
    public void PackerBundle_Scripts_FiltersCorrectly()
    {
        var bundle = new PackerBundle();
        bundle.AddScript("00base", "#!/bin/sh");
        bundle.AddFile("http", "answers", "", "KEYMAPOPTS=us us");

        bundle.Scripts.Count().ShouldBe(1);
        bundle.HttpFiles.Count().ShouldBe(1);
    }

    [Fact]
    public void PackerBundle_Apply_RunsContributorsInOrder()
    {
        var order = new List<string>();

        var c1 = new TestContributor("first", order);
        var c2 = new TestContributor("second", order);

        var bundle = new PackerBundle();
        bundle.Apply(c1, c2);

        order.ShouldBe(new[] { "first", "second" });
    }

    [Fact]
    public void VBoxManageConfig_ToCommands_EmitsCorrectArrays()
    {
        var config = new VBoxManageConfig
        {
            Cpus = 4,
            Memory = 256,
            NestedHwVirt = true
        };

        var commands = config.ToCommands();

        commands.ShouldContain(c => c[0] == "modifyvm" && c[2] == "--memory" && c[3] == "256");
        commands.ShouldContain(c => c[0] == "modifyvm" && c[2] == "--cpus" && c[3] == "4");
        commands.ShouldContain(c => c[0] == "modifyvm" && c[2] == "--nested-hw-virt" && c[3] == "on");
    }

    [Fact]
    public void EnvTemplate_Render_IncludesDescriptionsAndDefaults()
    {
        var template = new EnvTemplate
        {
            Variables = new List<EnvVariable>
            {
                new() { Key = "ALPINE_VERSION", DefaultValue = "3.21", Description = "Alpine version" },
                new() { Key = "DOCKER_BRIDGE", IsRequired = true }
            }
        };

        var rendered = template.Render();
        rendered.ShouldContain("# Alpine version");
        rendered.ShouldContain("ALPINE_VERSION=3.21");
        rendered.ShouldContain("DOCKER_BRIDGE=");
    }

    [Fact]
    public void EnvValues_Parse_RoundTrips()
    {
        var original = new EnvValues { Values = new Dictionary<string, string> { ["A"] = "1", ["B"] = "2" } };
        var rendered = original.Render();
        var parsed = EnvValues.Parse(rendered);

        parsed.Values["A"].ShouldBe("1");
        parsed.Values["B"].ShouldBe("2");
    }

    [Fact]
    public void HclExpressionHelpers_EmitCorrectSyntax()
    {
        Hcl.Var("name").Value.ShouldBe("var.name");
        Hcl.Local("timestamp").Value.ShouldBe("local.timestamp");
        Hcl.Data("amazon-ami", "ubuntu", "id").Value.ShouldBe("data.amazon-ami.ubuntu.id");
        Hcl.Func("upper", "var.name").Value.ShouldBe("upper(var.name)");
    }

    private sealed class TestContributor : IPackerBundleContributor
    {
        private readonly string _name;
        private readonly List<string> _order;

        public TestContributor(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        public void Contribute(PackerBundle bundle) => _order.Add(_name);
    }
}
