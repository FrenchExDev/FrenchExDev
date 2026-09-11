using FrenchExDev.Net.Packer.Bundle;
using FrenchExDev.Net.Packer.Bundle.Hcl2;

namespace FrenchExDev.Net.Packer.Bundle.Tests;

public class VBoxManageConfigFullTests
{
    [Fact]
    public void AllFlags_EmitCommands()
    {
        var config = new VBoxManageConfig
        {
            Cpus = 4, Memory = 2048, VideoMemory = 128,
            Chipset = "ich9", ParavirtProvider = "kvm", OsType = "Linux_64",
            GraphicsController = "vmsvga",
            IoApic = true, HwVirtEx = true, NestedHwVirt = true, NestedPaging = true,
            LargePages = true, VtxUx = true, VtxVPid = true, Pae = true,
            Acpi = true, HPet = true, PageFusion = false, Vrde = false, Usb = false,
            HwVirtExclusive = true,
            DnsProxy = true, LocalhostReachable = true, DnsHostResolver = true,
            StorageControllerName = "SATA", HostIoCache = true, NonRotational = true, Discard = true
        };

        var cmds = config.ToCommands();
        cmds.Count.ShouldBeGreaterThan(20);
        cmds.ShouldContain(c => c[2] == "--memory" && c[3] == "2048");
        cmds.ShouldContain(c => c[2] == "--cpus" && c[3] == "4");
        cmds.ShouldContain(c => c[2] == "--chipset" && c[3] == "ich9");
        cmds.ShouldContain(c => c[2] == "--paravirtprovider" && c[3] == "kvm");
        cmds.ShouldContain(c => c[2] == "--ostype" && c[3] == "Linux_64");
        cmds.ShouldContain(c => c[2] == "--graphicscontroller" && c[3] == "vmsvga");
        cmds.ShouldContain(c => c[2] == "--ioapic" && c[3] == "on");
        cmds.ShouldContain(c => c[2] == "--pae" && c[3] == "on");
        cmds.ShouldContain(c => c[2] == "--pagefusion" && c[3] == "off");
        cmds.ShouldContain(c => c[2] == "--vrde" && c[3] == "off");
        cmds.ShouldContain(c => c[2] == "--usb" && c[3] == "off");
        cmds.ShouldContain(c => c[0] == "setproperty" && c[1] == "hwvirtexcl" && c[2] == "on");
        cmds.ShouldContain(c => c[0] == "storagectl" && c[3] == "--hostiocache" && c[4] == "on");
        cmds.ShouldContain(c => c[0] == "storageattach" && c[4] == "--nonrotational" && c[5] == "on");
        cmds.ShouldContain(c => c[0] == "setextradata");
    }

    [Fact]
    public void EmptyConfig_EmitsNoCommands()
    {
        var cmds = new VBoxManageConfig().ToCommands();
        cmds.Count.ShouldBe(0);
    }

    [Fact]
    public void DisableDiscardWithoutNonRotational()
    {
        var config = new VBoxManageConfig { Discard = true, NonRotational = false };
        var cmds = config.ToCommands();
        cmds.ShouldNotContain(c => c[0] == "setextradata");
    }
}

public class HclWriterEdgeCaseTests
{
    private static string Write(Action<HclWriter> action)
    {
        var sw = new StringWriter();
        using (var w = new HclWriter(sw)) action(w);
        return sw.ToString().Replace("\r\n", "\n").TrimEnd('\n');
    }

    [Fact] public void NullInt_EmitsNothing() => Write(w => w.Argument("x", (int?)null)).ShouldBeEmpty();
    [Fact] public void NullBool_EmitsNothing() => Write(w => w.Argument("x", (bool?)null)).ShouldBeEmpty();
    [Fact] public void NullUint_EmitsNothing() => Write(w => w.Argument("x", (uint?)null)).ShouldBeEmpty();
    [Fact] public void NullList_EmitsNothing() => Write(w => w.ArgumentList("x", null)).ShouldBeEmpty();
    [Fact] public void EmptyList_EmitsNothing() => Write(w => w.ArgumentList("x", Array.Empty<string>())).ShouldBeEmpty();
    [Fact] public void NullMap_EmitsNothing() => Write(w => w.ArgumentMap("x", null)).ShouldBeEmpty();
    [Fact] public void EmptyMap_EmitsNothing() => Write(w => w.ArgumentMap("x", new Dictionary<string, string>())).ShouldBeEmpty();
    [Fact] public void NullListOfLists_EmitsNothing() => Write(w => w.ArgumentListOfLists("x", null)).ShouldBeEmpty();
    [Fact] public void EmptyListOfLists_EmitsNothing() => Write(w => w.ArgumentListOfLists("x", Array.Empty<IReadOnlyList<string>>())).ShouldBeEmpty();
    [Fact] public void UintArgument_EmitsValue() => Write(w => w.Argument("x", (uint?)42)).ShouldBe("x = 42");
    [Fact] public void BoolFalse_EmitsFalse() => Write(w => w.Argument("x", false)).ShouldBe("x = false");
    [Fact] public void Raw_EmitsText() => Write(w => w.Raw("hello")).ShouldBe("hello");
    [Fact] public void RawLine_EmitsWithNewline() => Write(w => w.RawLine("hello")).ShouldBe("hello");
    [Fact] public void BlankLine_EmitsEmpty() => Write(w => { w.Argument("a", 1); w.BlankLine(); w.Argument("b", 2); }).ShouldContain("\n\n");
}

public class PackerConfigWriteToTests
{
    private static string Write(Action<HclWriter> action)
    {
        var sw = new StringWriter();
        using (var w = new HclWriter(sw)) action(w);
        return sw.ToString();
    }

    [Fact]
    public void PackerConfig_WriteTo_EmitsBlock()
    {
        var config = new PackerConfigBuilder()
            .WithRequiredVersion(">= 1.7.0")
            .WithRequiredPlugin("vbox", ">= 1.0", "github.com/hashicorp/virtualbox")
            .Build();

        var hcl = Write(w => config.WriteTo(w));
        hcl.ShouldContain("packer {");
        hcl.ShouldContain("required_version");
        hcl.ShouldContain("required_plugins {");
        hcl.ShouldContain("vbox {");
    }

    [Fact]
    public void PackerConfig_NoPlugins_NoPluginsBlock()
    {
        var config = new PackerConfigBuilder().WithRequiredVersion(">= 1.7.0").Build();
        var hcl = Write(w => config.WriteTo(w));
        hcl.ShouldNotContain("required_plugins");
    }
}

public class PackerVariableWriteToTests
{
    private static string Write(Action<HclWriter> action)
    {
        var sw = new StringWriter();
        using (var w = new HclWriter(sw)) action(w);
        return sw.ToString();
    }

    [Fact]
    public void StringDefault() =>
        Write(w => new PackerVariable { Name = "v", Default = "hello" }.WriteTo(w)).ShouldContain("default = \"hello\"");

    [Fact]
    public void IntDefault() =>
        Write(w => new PackerVariable { Name = "v", Default = 42 }.WriteTo(w)).ShouldContain("default = 42");

    [Fact]
    public void BoolDefault() =>
        Write(w => new PackerVariable { Name = "v", Default = true }.WriteTo(w)).ShouldContain("default = true");

    [Fact]
    public void Sensitive() =>
        Write(w => new PackerVariable { Name = "v", Sensitive = true }.WriteTo(w)).ShouldContain("sensitive = true");

    [Fact]
    public void Validation()
    {
        var v = new PackerVariable
        {
            Name = "v",
            Validation = new PackerVariableValidation("can(var.v)", "bad")
        };
        var hcl = Write(w => v.WriteTo(w));
        hcl.ShouldContain("validation {");
        hcl.ShouldContain("condition = can(var.v)");
        hcl.ShouldContain("error_message = \"bad\"");
    }
}

public class PackerBuildWriteToTests
{
    private static string Write(Action<HclWriter> action)
    {
        var sw = new StringWriter();
        using (var w = new HclWriter(sw)) action(w);
        return sw.ToString();
    }

    [Fact]
    public void Build_WithOverrides()
    {
        var build = new PackerBuildBuilder()
            .WithSource("source.vbox.alpine")
            .WithSourceOverride(new PackerBuildSourceOverride
            {
                SourceRef = "vbox.alpine",
                Arguments = new() { ["vm_name"] = "override" }
            })
            .Build();

        var hcl = Write(w => build.WriteTo(w));
        hcl.ShouldContain("source \"vbox.alpine\"");
        hcl.ShouldContain("vm_name = \"override\"");
    }

    [Fact]
    public void Provisioner_WithOnlyExcept()
    {
        var prov = new PackerProvisioner
        {
            Type = "shell",
            Only = new List<string> { "source.vbox.alpine" },
            Except = new List<string> { "source.docker.test" }
        };
        var hcl = Write(w => prov.WriteTo(w));
        hcl.ShouldContain("only =");
        hcl.ShouldContain("except =");
    }

    [Fact]
    public void PostProcessor_WithOnlyExcept()
    {
        var pp = new PackerPostProcessor
        {
            Type = "vagrant",
            Only = new List<string> { "source.vbox.alpine" }
        };
        var hcl = Write(w => pp.WriteTo(w));
        hcl.ShouldContain("post-processor \"vagrant\"");
        hcl.ShouldContain("only =");
    }
}

public class PackerSourceWriteToTests
{
    private static string Write(Action<HclWriter> action)
    {
        var sw = new StringWriter();
        using (var w = new HclWriter(sw)) action(w);
        return sw.ToString();
    }

    [Fact]
    public void Source_WithAllArgTypes()
    {
        var source = new PackerSource
        {
            Type = "test", Name = "s1",
            Arguments = new()
            {
                ["str"] = "hello",
                ["num"] = 42,
                ["unum"] = (uint)100,
                ["flag"] = true,
                ["list"] = (IReadOnlyList<string>)new List<string> { "a", "b" },
                ["map"] = (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["k"] = "v" },
                ["lol"] = (IReadOnlyList<IReadOnlyList<string>>)new List<IReadOnlyList<string>>
                {
                    new List<string> { "x", "y" }
                },
                ["expr"] = Hcl.Var("name"),
                ["null_val"] = null,
                ["other"] = 3.14
            }
        };

        var hcl = Write(w => source.WriteTo(w));
        hcl.ShouldContain("str = \"hello\"");
        hcl.ShouldContain("num = 42");
        hcl.ShouldContain("unum = 100");
        hcl.ShouldContain("flag = true");
        hcl.ShouldContain("list =");
        hcl.ShouldContain("map = {");
        hcl.ShouldContain("expr = var.name");
        hcl.ShouldContain("other = \""); // fallback to ToString (locale-dependent format)
    }
}

public class EnvValuesEdgeCaseTests
{
    [Fact]
    public void Parse_SkipsComments()
    {
        var env = EnvValues.Parse("# comment\nKEY=val\n\n  # another\nB=2");
        env.Values.Count.ShouldBe(2);
        env.Values["KEY"].ShouldBe("val");
    }

    [Fact]
    public void Parse_SkipsEmptyLines()
    {
        var env = EnvValues.Parse("\n\n\nA=1\n\n");
        env.Values.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_SkipsLinesWithoutEquals()
    {
        var env = EnvValues.Parse("noequalssign\nA=1");
        env.Values.Count.ShouldBe(1);
    }

    [Fact]
    public void Render_Empty() => new EnvValues().Render().ShouldBeEmpty();
}

public class BundleFileEdgeCaseTests
{
    [Fact]
    public void RelativePath_WithEmptyDir()
    {
        var f = new BundleFile { Name = ".env", Extension = "", Directory = "", Content = "" };
        f.RelativePath.ShouldBe(".env");
    }

    [Fact]
    public void RelativePath_WithDir()
    {
        var f = new BundleFile { Name = "test", Extension = ".sh", Directory = "scripts", Content = "" };
        f.RelativePath.ShouldBe("scripts/test.sh");
    }
}

public class PackerBundleWriterEdgeCaseTests
{
    [Fact]
    public async Task EmptyBundle_WritesNothing()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"empty-{Guid.NewGuid():N}");
        try
        {
            await new PackerBundleWriter().WriteAsync(new PackerBundle(), dir);
            Directory.Exists(dir).ShouldBeTrue();
            Directory.GetFiles(dir, "*.pkr.hcl").Length.ShouldBe(0);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Bundle_WithEnvTemplate_MaterializesFile()
    {
        var bundle = new PackerBundle();
        bundle.EnvTemplate.Variables.Add(new EnvVariable { Key = "X", DefaultValue = "1" });
        bundle.EnvValues.Values["X"] = "actual";

        var dir = Path.Combine(Path.GetTempPath(), $"env-{Guid.NewGuid():N}");
        try
        {
            await new PackerBundleWriter().WriteAsync(bundle, dir);
            File.Exists(Path.Combine(dir, ".env.template")).ShouldBeTrue();
            File.Exists(Path.Combine(dir, ".env")).ShouldBeTrue();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}

public class HclExpressionTests
{
    [Fact] public void Raw() => Hcl.Raw("true").Value.ShouldBe("true");
    [Fact] public void ToString_ReturnsValue() => Hcl.Var("x").ToString().ShouldBe("var.x");
}

public class VBoxManageCommandTests
{
    [Fact] public void ModifyVm() => VBoxManageCommand.ModifyVm("k", "v").ShouldBe(new List<string> { "modifyvm", "{{ .Name }}", "--k", "v" });
    [Fact] public void SetExtraData() => VBoxManageCommand.SetExtraData("k", "v").ShouldBe(new List<string> { "setextradata", "{{ .Name }}", "k", "v" });
    [Fact] public void SetProperty() => VBoxManageCommand.SetProperty("k", "v").ShouldBe(new List<string> { "setproperty", "k", "v" });
    [Fact] public void StorageCtl() => VBoxManageCommand.StorageCtl("n", "k", "v")[0].ShouldBe("storagectl");
    [Fact] public void StorageAttach() => VBoxManageCommand.StorageAttach("c", "0", "k", "v")[0].ShouldBe("storageattach");
}
