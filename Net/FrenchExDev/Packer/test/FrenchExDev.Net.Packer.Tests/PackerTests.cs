using CsCheck;
using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using FrenchExDev.Net.Packer;
using Shouldly;

namespace FrenchExDev.Net.Packer.Tests;

// ── Command Serialization Tests ─────────────────────────────────────────────

public sealed class CommandSerializationTests
{
    [Fact]
    public void Build_ForceTrue_SerializesAsEqualsFormat()
    {
        var cmd = new PackerBuildCommand { Force = true };
        var args = cmd.ToArguments();
        args.ShouldContain("-force=true");
    }

    [Fact]
    public void Build_ForceFalse_SerializesAsEqualsFalse()
    {
        var cmd = new PackerBuildCommand { Force = false };
        var args = cmd.ToArguments();
        args.ShouldContain("-force=false");
    }

    [Fact]
    public void Build_ParallelBuilds_SerializesWithEqualsFormat()
    {
        var cmd = new PackerBuildCommand { ParallelBuilds = "4" };
        var args = cmd.ToArguments();
        args.ShouldContain("-parallel-builds=4");
    }

    [Fact]
    public void Build_VarTrue_SerializesCorrectly()
    {
        var cmd = new PackerBuildCommand { Var = true };
        var args = cmd.ToArguments();
        args.ShouldContain("-var=true");
    }

    [Fact]
    public void Build_Color_SerializesAsString()
    {
        var cmd = new PackerBuildCommand { Color = "false" };
        var args = cmd.ToArguments();
        args.ShouldContain("-color=false");
    }

    [Fact]
    public void Build_AllOptions_CorrectCount()
    {
        var cmd = new PackerBuildCommand
        {
            Force = true,
            Color = "true",
            Debug = false,
            OnError = "abort",
            ParallelBuilds = "2",
            TimestampUi = true,
            Var = true,
            VarFile = "vars.json",
            MachineReadable = true
        };
        var args = cmd.ToArguments();

        args.Count.ShouldBe(9);
    }

    [Fact]
    public void Build_NullOptions_NotIncluded()
    {
        var cmd = new PackerBuildCommand();
        var args = cmd.ToArguments();
        args.Count.ShouldBe(0);
    }

    [Fact]
    public void Build_CommandPath_IsBuild()
    {
        var cmd = new PackerBuildCommand();
        cmd.CommandPath.ShouldBe(new[] { "build" });
    }

    [Fact]
    public void Build_ImplementsICliCommand()
    {
        var cmd = new PackerBuildCommand();
        cmd.ShouldBeAssignableTo<ICliCommand>();
    }

    [Fact]
    public void Validate_SyntaxOnly_SerializesCorrectly()
    {
        var cmd = new PackerValidateCommand { SyntaxOnly = true };
        var args = cmd.ToArguments();
        args.ShouldContain("-syntax-only=true");
    }

    [Fact]
    public void Validate_NoWarnUndeclaredVar_SerializesCorrectly()
    {
        var cmd = new PackerValidateCommand { NoWarnUndeclaredVar = true };
        var args = cmd.ToArguments();
        args.ShouldContain("-no-warn-undeclared-var=true");
    }

    [Fact]
    public void Init_UpgradeAndForce_BothSerialized()
    {
        var cmd = new PackerInitCommand { Upgrade = true, Force = true };
        var args = cmd.ToArguments();
        args.ShouldContain("-upgrade=true");
        args.ShouldContain("-force=true");
    }

    [Fact]
    public void Fmt_CheckAndDiff_SerializeCorrectly()
    {
        var cmd = new PackerFmtCommand { Check = true, Diff = true };
        var args = cmd.ToArguments();
        args.ShouldContain("-check=true");
        args.ShouldContain("-diff=true");
    }

    [Fact]
    public void Inspect_NoOptions_EmptyArgs()
    {
        var cmd = new PackerInspectCommand();
        var args = cmd.ToArguments();
        args.Count.ShouldBe(0);
    }

    [Fact]
    public void Inspect_MachineReadable_SerializesCorrectly()
    {
        var cmd = new PackerInspectCommand { MachineReadable = true };
        var args = cmd.ToArguments();
        args.ShouldContain("-machine-readable=true");
    }

    [Fact]
    public void Hcl2Upgrade_OutputFile_SerializesCorrectly()
    {
        var cmd = new PackerHcl2UpgradeCommand { OutputFile = "output.pkr.hcl" };
        var args = cmd.ToArguments();
        args.ShouldContain("-output-file=output.pkr.hcl");
    }

    [Fact]
    public void Console_VarAndVarFile_SerializeCorrectly()
    {
        var cmd = new PackerConsoleCommand { Var = true, VarFile = "f.json" };
        var args = cmd.ToArguments();
        args.ShouldContain("-var=true");
        args.ShouldContain("-var-file=f.json");
    }

    [Fact]
    public void PluginsInstall_Force_SerializesCorrectly()
    {
        var cmd = new PackerPluginsInstallCommand { Force = true };
        var args = cmd.ToArguments();
        args.ShouldContain("-force=true");
    }

    [Fact]
    public void PluginsRemove_NoProperties_EmptyArgs()
    {
        var cmd = new PackerPluginsRemoveCommand();
        var args = cmd.ToArguments();
        args.Count.ShouldBe(0);
    }

    [Fact]
    public void PluginsInstalled_CommandPath()
    {
        var cmd = new PackerPluginsInstalledCommand();
        cmd.CommandPath.ShouldBe(new[] { "plugins", "installed" });
    }

    [Fact]
    public void PluginsRequired_NoProperties_EmptyArgs()
    {
        var cmd = new PackerPluginsRequiredCommand();
        var args = cmd.ToArguments();
        args.Count.ShouldBe(0);
    }

    [Fact]
    public void Build_WarnOnUndeclaredVar_SerializesCorrectly()
    {
        var cmd = new PackerBuildCommand { WarnOnUndeclaredVar = true };
        var args = cmd.ToArguments();
        args.ShouldContain("-warn-on-undeclared-var=true");
    }

    [Fact]
    public void Build_Except_SerializesAsString()
    {
        var cmd = new PackerBuildCommand { Except = "docker" };
        var args = cmd.ToArguments();
        args.ShouldContain("-except=docker");
    }

    [Fact]
    public void Build_Only_SerializesAsString()
    {
        var cmd = new PackerBuildCommand { Only = "amazon-ebs" };
        var args = cmd.ToArguments();
        args.ShouldContain("-only=amazon-ebs");
    }

    [Fact]
    public void Build_AllProperties_AllSerialized()
    {
        var cmd = new PackerBuildCommand
        {
            Color = "true", Debug = true, Except = "docker", Only = "aws",
            Force = true, MachineReadable = true, OnError = "abort",
            ParallelBuilds = "4", TimestampUi = true, Var = true,
            VarFile = "vars.json", WarnOnUndeclaredVar = true,
            IgnorePrereleasePlugins = true, UseSequentialEvaluation = true
        };
        var args = cmd.ToArguments();
        args.Count.ShouldBe(14);
        args.ShouldContain("-ignore-prerelease-plugins=true");
        args.ShouldContain("-use-sequential-evaluation=true");
    }

    [Fact]
    public void Fix_Validate_SerializesCorrectly()
    {
        var cmd = new PackerFixCommand { ValidateOpt = "true" };
        var args = cmd.ToArguments();
        args.ShouldContain("-validate=true");
    }

    [Fact]
    public void Fix_NoOptions_EmptyArgs()
    {
        var cmd = new PackerFixCommand();
        cmd.ToArguments().Count.ShouldBe(0);
        cmd.CommandPath.ShouldBe(new[] { "fix" });
    }

    [Fact]
    public void Version_NoProperties_EmptyArgs()
    {
        var cmd = new PackerVersionCommand();
        cmd.ToArguments().Count.ShouldBe(0);
        cmd.CommandPath.ShouldBe(new[] { "version" });
    }

    [Fact]
    public void Validate_AllProperties_AllSerialized()
    {
        var cmd = new PackerValidateCommand
        {
            SyntaxOnly = true, Except = "docker", Only = "aws",
            MachineReadable = true, Var = true, VarFile = "vars.json",
            NoWarnUndeclaredVar = true, EvaluateDatasources = true,
            IgnorePrereleasePlugins = true, UseSequentialEvaluation = true
        };
        var args = cmd.ToArguments();
        args.Count.ShouldBe(10);
        args.ShouldContain("-evaluate-datasources=true");
        args.ShouldContain("-except=docker");
        args.ShouldContain("-only=aws");
        args.ShouldContain("-machine-readable=true");
        args.ShouldContain("-var-file=vars.json");
    }

    [Fact]
    public void Console_AllProperties_AllSerialized()
    {
        var cmd = new PackerConsoleCommand
        {
            Var = true, VarFile = "f.json",
            ConfigType = true, UseSequentialEvaluation = true
        };
        var args = cmd.ToArguments();
        args.Count.ShouldBe(4);
        args.ShouldContain("-config-type=true");
        args.ShouldContain("-use-sequential-evaluation=true");
    }

    [Fact]
    public void Fmt_AllProperties_AllSerialized()
    {
        var cmd = new PackerFmtCommand
        {
            Check = true, Diff = true, Write = "output.pkr.hcl", Recursive = true
        };
        var args = cmd.ToArguments();
        args.Count.ShouldBe(4);
        args.ShouldContain("-write=output.pkr.hcl");
        args.ShouldContain("-recursive=true");
    }

    [Fact]
    public void Hcl2Upgrade_AllProperties_AllSerialized()
    {
        var cmd = new PackerHcl2UpgradeCommand
        {
            OutputFile = "new.pkr.hcl", WithAnnotations = true
        };
        var args = cmd.ToArguments();
        args.Count.ShouldBe(2);
        args.ShouldContain("-with-annotations=true");
    }

    [Fact]
    public void Inspect_AllProperties_AllSerialized()
    {
        var cmd = new PackerInspectCommand
        {
            MachineReadable = true, UseSequentialEvaluation = true
        };
        var args = cmd.ToArguments();
        args.Count.ShouldBe(2);
        args.ShouldContain("-use-sequential-evaluation=true");
    }

    [Fact]
    public void PluginsInstall_AllProperties_AllSerialized()
    {
        var cmd = new PackerPluginsInstallCommand
        {
            Path = "/usr/local/bin", Force = true
        };
        var args = cmd.ToArguments();
        args.Count.ShouldBe(2);
        args.ShouldContain("-path=/usr/local/bin");
    }

    [Fact]
    public void Init_NoOptions_EmptyArgs()
    {
        var cmd = new PackerInitCommand();
        cmd.ToArguments().Count.ShouldBe(0);
    }
}

// ── Generated API Shape Tests ───────────────────────────────────────────────

public sealed class GeneratedCodeTests
{
    private static PackerClient CreateClient() =>
        new(TestBindings.Create("packer"));

    [Fact]
    public async Task Client_Build_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.BuildAsync(b => b.WithForce(true));
        cmd.ShouldNotBeNull();
        cmd.ShouldBeOfType<PackerBuildCommand>();
        cmd.Force.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Validate_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.ValidateAsync(b => b.WithSyntaxOnly(true));
        cmd.SyntaxOnly.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Init_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.InitAsync(b => b.WithUpgrade(true));
        cmd.Upgrade.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Fmt_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.FmtAsync(b => b.WithCheck(true));
        cmd.Check.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Inspect_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.InspectAsync(b => b.WithMachineReadable(true));
        cmd.MachineReadable.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Hcl2Upgrade_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.Hcl2UpgradeAsync(b => b.WithOutputFile("new.pkr.hcl"));
        cmd.OutputFile.ShouldBe("new.pkr.hcl");
    }

    [Fact]
    public async Task Client_Console_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.ConsoleAsync(b => b.WithVar(true));
        cmd.Var.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Plugins_Install_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.Plugins.InstallAsync(b => b.WithForce(true));
        cmd.Force.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Plugins_Remove_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.Plugins.RemoveAsync(b => { });
        cmd.ShouldNotBeNull();
        cmd.CommandPath.ShouldBe(new[] { "plugins", "remove" });
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public async Task Client_Plugins_Installed_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.Plugins.InstalledAsync(b => { });
        cmd.ShouldNotBeNull();
        cmd.CommandPath.ShouldBe(new[] { "plugins", "installed" });
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public async Task Client_Plugins_Required_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.Plugins.RequiredAsync(b => { });
        cmd.ShouldNotBeNull();
        cmd.CommandPath.ShouldBe(new[] { "plugins", "required" });
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public void Builder_FluentChain_ReturnsBuilder()
    {
        var builder = new PackerBuildCommandBuilder()
            .WithForce(true)
            .WithParallelBuilds("4")
            .WithVar(true);

        builder.ShouldBeOfType<PackerBuildCommandBuilder>();
    }

    [Fact]
    public async Task Client_Fix_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.FixAsync(b => b.WithValidateOpt("true"));
        cmd.ValidateOpt.ShouldBe("true");
        cmd.CommandPath.ShouldBe(new[] { "fix" });
    }

    [Fact]
    public async Task Client_Version_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.VersionAsync(b => { });
        cmd.ShouldNotBeNull();
        cmd.CommandPath.ShouldBe(new[] { "version" });
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public async Task Client_Build_AllProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.BuildAsync(b => b
            .WithColor("true").WithDebug(true).WithExcept("docker")
            .WithOnly("aws").WithForce(true).WithMachineReadable(true)
            .WithOnError("abort").WithParallelBuilds("4")
            .WithTimestampUi(true).WithVar(true).WithVarFile("vars.json")
            .WithWarnOnUndeclaredVar(true));
        cmd.ToArguments().Count.ShouldBe(12);
    }

    [Fact]
    public async Task Client_Validate_AllProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.ValidateAsync(b => b
            .WithSyntaxOnly(true).WithExcept("docker").WithOnly("aws")
            .WithMachineReadable(true).WithVar(true).WithVarFile("vars.json")
            .WithNoWarnUndeclaredVar(true).WithEvaluateDatasources(true));
        cmd.ToArguments().Count.ShouldBe(8);
    }

    [Fact]
    public async Task Client_Console_AllProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.ConsoleAsync(b => b
            .WithVar(true).WithVarFile("f.json")
            .WithConfigType(true).WithUseSequentialEvaluation(true));
        cmd.ToArguments().Count.ShouldBe(4);
    }

    [Fact]
    public async Task Client_Fmt_AllProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.FmtAsync(b => b
            .WithCheck(true).WithDiff(true)
            .WithWrite("output.pkr.hcl").WithRecursive(true));
        cmd.ToArguments().Count.ShouldBe(4);
    }

    [Fact]
    public async Task Client_Hcl2Upgrade_AllProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.Hcl2UpgradeAsync(b => b
            .WithOutputFile("new.pkr.hcl").WithWithAnnotations(true));
        cmd.ToArguments().Count.ShouldBe(2);
    }

    [Fact]
    public async Task Client_Init_AllProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.InitAsync(b => b.WithUpgrade(true).WithForce(true));
        cmd.ToArguments().Count.ShouldBe(2);
    }

    [Fact]
    public async Task Client_Inspect_AllProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.InspectAsync(b => b
            .WithMachineReadable(true).WithUseSequentialEvaluation(true));
        cmd.ToArguments().Count.ShouldBe(2);
    }

    [Fact]
    public async Task Client_PluginsInstall_AllProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.Plugins.InstallAsync(b => b
            .WithPath("/usr/local/bin").WithForce(true));
        cmd.ToArguments().Count.ShouldBe(2);
    }

    [Fact]
    public async Task Client_Build_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.BuildAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public async Task Client_Validate_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.ValidateAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public async Task Client_Console_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.ConsoleAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
        cmd.CommandPath.ShouldBe(new[] { "console" });
    }

    [Fact]
    public async Task Client_Fmt_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.FmtAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
        cmd.CommandPath.ShouldBe(new[] { "fmt" });
    }

    [Fact]
    public async Task Client_Hcl2Upgrade_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.Hcl2UpgradeAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
        cmd.CommandPath.ShouldBe(new[] { "hcl2_upgrade" });
    }

    [Fact]
    public async Task Client_Init_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.InitAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
        cmd.CommandPath.ShouldBe(new[] { "init" });
    }

    [Fact]
    public async Task Client_Inspect_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.InspectAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
        cmd.CommandPath.ShouldBe(new[] { "inspect" });
    }

    [Fact]
    public async Task Client_Fix_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.FixAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
        cmd.CommandPath.ShouldBe(new[] { "fix" });
    }

    [Fact]
    public void Packer_Create_ReturnsClient()
    {
        var client = Packer.Create(TestBindings.Create("packer"));
        client.ShouldNotBeNull();
    }
}

// ── PackerBuildParser Tests ─────────────────────────────────────────────────

public sealed class PackerBuildParserTests
{
    private readonly PackerBuildParser _parser = new();

    [Fact]
    public void ParseLine_BuildOutput_YieldsPackerBuildOutput()
    {
        var line = new OutputLine("==> amazon-ebs: Creating AMI...", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerBuildOutput>();
        evt.BuildName.ShouldBe("amazon-ebs");
        evt.Message.ShouldBe("Creating AMI...");
    }

    [Fact]
    public void ParseLine_BuildError_YieldsPackerBuildError()
    {
        var line = new OutputLine("==> amazon-ebs (error): Something went wrong", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerBuildError>();
        evt.BuildName.ShouldBe("amazon-ebs");
        evt.Message.ShouldBe("Something went wrong");
    }

    [Fact]
    public void ParseLine_ProvisionerOutput_YieldsProvisionerEvent()
    {
        var line = new OutputLine("    amazon-ebs: Running shell provisioner", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerProvisionerOutput>();
        evt.BuildName.ShouldBe("amazon-ebs");
        evt.Message.ShouldBe("Running shell provisioner");
    }

    [Fact]
    public void ParseLine_BuildFinished_YieldsFinishedSuccess()
    {
        var line = new OutputLine("Build 'amazon-ebs' finished.", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerBuildFinished>();
        evt.BuildName.ShouldBe("amazon-ebs");
        evt.Success.ShouldBeTrue();
    }

    [Fact]
    public void ParseLine_BuildErrored_YieldsFinishedFailed()
    {
        var line = new OutputLine("Build 'amazon-ebs' errored after 2m3s: error text", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerBuildFinished>();
        evt.BuildName.ShouldBe("amazon-ebs");
        evt.Success.ShouldBeFalse();
    }

    [Fact]
    public void ParseLine_ArtifactLine_YieldsFinishedWithArtifactId()
    {
        var line = new OutputLine("--> amazon-ebs: AMIs were created: us-east-1: ami-12345678", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerBuildFinished>();
        evt.BuildName.ShouldBe("amazon-ebs");
        evt.ArtifactId.ShouldBe("AMIs were created: us-east-1: ami-12345678");
        evt.Success.ShouldBeTrue();
    }

    [Fact]
    public void ParseLine_EmptyLine_YieldsNothing()
    {
        var events = _parser.ParseLine(new OutputLine("", OutputSource.StdOut)).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void ParseLine_UnrecognizedText_YieldsOutputLine()
    {
        var line = new OutputLine("some random output", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        events[0].ShouldBeOfType<PackerOutputLine>();
    }

    [Fact]
    public void ParseLine_StdErr_StillMatchesPatterns()
    {
        var line = new OutputLine("==> amazon-ebs: Creating AMI...", OutputSource.StdErr);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        events[0].ShouldBeOfType<PackerBuildOutput>();
    }

    [Fact]
    public void ParseLine_WhitespaceOnly_YieldsNothing()
    {
        var events = _parser.ParseLine(new OutputLine("   \t  ", OutputSource.StdOut)).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void ParseLine_DottedBuildName_YieldsBuildOutput()
    {
        var line = new OutputLine("==> docker.alpine: Pulling image...", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerBuildOutput>();
        evt.BuildName.ShouldBe("docker.alpine");
    }

    [Fact]
    public void ParseLine_ComplexBuildName_YieldsProvisionerOutput()
    {
        var line = new OutputLine("    learn-packer.qemu.base-v2: Installing packages", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerProvisionerOutput>();
        evt.BuildName.ShouldBe("learn-packer.qemu.base-v2");
    }

    [Fact]
    public void ParseLine_DottedBuildName_YieldsFinished()
    {
        var line = new OutputLine("Build 'docker.alpine' finished.", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerBuildFinished>();
        evt.BuildName.ShouldBe("docker.alpine");
        evt.Success.ShouldBeTrue();
    }

    [Fact]
    public void Complete_NegativeExitCode_YieldsError()
    {
        var events = _parser.Complete(-1).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerBuildError>();
        evt.Message.ShouldContain("-1");
    }

    [Fact]
    public void Complete_MinValueExitCode_YieldsError()
    {
        var events = _parser.Complete(int.MinValue).ToList();
        events.Count.ShouldBe(1);
        events[0].ShouldBeOfType<PackerBuildError>();
    }

    [Fact]
    public void Complete_ZeroExitCode_YieldsNothing()
    {
        var events = _parser.Complete(0).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_NonZeroExitCode_YieldsError()
    {
        var events = _parser.Complete(1).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerBuildError>();
        evt.Message.ShouldContain("exited with code 1");
    }
}

// ── PackerMachineReadableParser Tests ───────────────────────────────────────

public sealed class PackerMachineReadableParserTests
{
    private readonly PackerMachineReadableParser _parser = new();

    [Fact]
    public void ParseLine_ValidMachineReadable_YieldsEvent()
    {
        var line = new OutputLine("1234567890,amazon-ebs,ui,say,Building...", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerMachineReadableEvent>();
        evt.Timestamp.ShouldBe(1234567890L);
        evt.Target.ShouldBe("amazon-ebs");
        evt.EventType.ShouldBe("ui");
        evt.Data.ShouldBe(new[] { "say", "Building..." });
    }

    [Fact]
    public void ParseLine_PackerCommaEscape_Unescaped()
    {
        var line = new OutputLine("1234567890,,ui,data with %!(PACKER_COMMA) inside", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerMachineReadableEvent>();
        evt.Target.ShouldBe("");
        evt.Data[0].ShouldContain(",");
    }

    [Fact]
    public void ParseLine_StdErr_YieldsOutputLine()
    {
        var line = new OutputLine("error text", OutputSource.StdErr);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        events[0].ShouldBeOfType<PackerOutputLine>();
    }

    [Fact]
    public void ParseLine_InvalidFormat_YieldsOutputLine()
    {
        var line = new OutputLine("not,machine,readable", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        // "not" is not a valid timestamp, so falls through to output line
        events[0].ShouldBeOfType<PackerOutputLine>();
    }

    [Fact]
    public void ParseLine_EmptyLine_YieldsNothing()
    {
        var events = _parser.ParseLine(new OutputLine("", OutputSource.StdOut)).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_NonZeroExitCode_YieldsError()
    {
        var events = _parser.Complete(42).ToList();
        events.Count.ShouldBe(1);
        events[0].ShouldBeOfType<PackerBuildError>();
    }

    [Fact]
    public void Complete_ZeroExitCode_YieldsNothing()
    {
        var events = _parser.Complete(0).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void ParseLine_TooFewParts_YieldsOutputLine()
    {
        var line = new OutputLine("hello,world", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        events[0].ShouldBeOfType<PackerOutputLine>();
    }

    [Fact]
    public void ParseLine_ExactlyThreeParts_YieldsEventWithEmptyData()
    {
        var line = new OutputLine("1234567890,target,type", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerMachineReadableEvent>();
        evt.Timestamp.ShouldBe(1234567890L);
        evt.Target.ShouldBe("target");
        evt.EventType.ShouldBe("type");
        evt.Data.ShouldBeEmpty();
    }

    [Fact]
    public void ParseLine_SingleValue_YieldsOutputLine()
    {
        var line = new OutputLine("singlevalue", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        events[0].ShouldBeOfType<PackerOutputLine>();
    }

    [Fact]
    public void ParseLine_WhitespaceOnly_YieldsNothing()
    {
        var events = _parser.ParseLine(new OutputLine("   \t  ", OutputSource.StdOut)).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void ParseLine_MultiplePackerCommaEscapes_AllUnescaped()
    {
        var line = new OutputLine("1234567890,,ui,a%!(PACKER_COMMA)b%!(PACKER_COMMA)c", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerMachineReadableEvent>();
        evt.Data[0].ShouldBe("a,b,c");
    }

    [Fact]
    public void ParseLine_EmptyFieldsBetweenCommas_PreservesStructure()
    {
        // "1234567890,,,type," splits to ["1234567890","","","type",""]
        // target=parts[1]="", eventType=parts[2]="", data=parts[3..]= ["type",""]
        var line = new OutputLine("1234567890,,,type,", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerMachineReadableEvent>();
        evt.Target.ShouldBe("");
        evt.EventType.ShouldBe("");
        evt.Data.Length.ShouldBe(2);
        evt.Data[0].ShouldBe("type");
        evt.Data[1].ShouldBe("");
    }

    [Fact]
    public void ParseLine_NegativeTimestamp_YieldsMachineReadableEvent()
    {
        var line = new OutputLine("-1,target,type,data", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerMachineReadableEvent>();
        evt.Timestamp.ShouldBe(-1L);
    }

    [Fact]
    public void Complete_NegativeExitCode_YieldsError()
    {
        var events = _parser.Complete(-1).ToList();
        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<PackerBuildError>();
        evt.Message.ShouldContain("-1");
    }
}

// ── PackerEvent Record Tests ────────────────────────────────────────────────

public sealed class PackerEventTests
{
    [Fact]
    public void PackerBuildStarted_Properties()
    {
        var evt = new PackerBuildStarted("amazon-ebs", "my-build");
        evt.BuilderType.ShouldBe("amazon-ebs");
        evt.BuildName.ShouldBe("my-build");
    }

    [Fact]
    public void PackerBuildOutput_Equality()
    {
        var a = new PackerBuildOutput("amazon-ebs", "Creating AMI...");
        var b = new PackerBuildOutput("amazon-ebs", "Creating AMI...");
        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void PackerBuildOutput_Inequality()
    {
        var a = new PackerBuildOutput("amazon-ebs", "Creating AMI...");
        var b = new PackerBuildOutput("amazon-ebs", "Deleting AMI...");
        a.ShouldNotBe(b);
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void PackerBuildFinished_Equality()
    {
        var a = new PackerBuildFinished("aws", true, "ami-123");
        var b = new PackerBuildFinished("aws", true, "ami-123");
        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void PackerBuildFinished_Inequality_DifferentSuccess()
    {
        var a = new PackerBuildFinished("aws", true, null);
        var b = new PackerBuildFinished("aws", false, null);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void PackerMachineReadableEvent_Equality()
    {
        var data = new[] { "say", "hello" };
        var a = new PackerMachineReadableEvent(123L, "t", "ui", data);
        var b = new PackerMachineReadableEvent(123L, "t", "ui", data);
        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void PackerProvisionerOutput_ProvisionerType()
    {
        var evt = new PackerProvisionerOutput("build", "shell", "running script");
        evt.ProvisionerType.ShouldBe("shell");
    }
}

// ── PackerBuildCollector Tests ──────────────────────────────────────────────

public sealed class PackerBuildCollectorTests
{
    [Fact]
    public void Complete_WithSuccessfulBuild_ReturnsSuccessResult()
    {
        var collector = new PackerBuildCollector();
        collector.OnEvent(new PackerBuildOutput("amazon-ebs", "Creating AMI..."));
        collector.OnEvent(new PackerBuildFinished("amazon-ebs", Success: true, ArtifactId: "ami-12345678"));

        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.Artifacts.Count.ShouldBe(1);
        result.Artifacts[0].ArtifactId.ShouldBe("ami-12345678");
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_WithFailedBuild_ReturnsFailureResult()
    {
        var collector = new PackerBuildCollector();
        collector.OnEvent(new PackerBuildError("amazon-ebs", "Permission denied"));
        collector.OnEvent(new PackerBuildFinished("amazon-ebs", Success: false, ArtifactId: null));

        var result = collector.Complete();
        result.Success.ShouldBeFalse();
        result.Artifacts.ShouldBeEmpty();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldContain("Permission denied");
    }

    [Fact]
    public void Complete_NoEvents_ReturnsSuccess()
    {
        var collector = new PackerBuildCollector();
        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.Artifacts.ShouldBeEmpty();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_MultipleArtifacts_AllCollected()
    {
        var collector = new PackerBuildCollector();
        collector.OnEvent(new PackerBuildFinished("amazon-ebs", true, "ami-111"));
        collector.OnEvent(new PackerBuildFinished("docker", true, "sha256:abc"));

        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.Artifacts.Count.ShouldBe(2);
    }

    [Fact]
    public void Complete_ErrorWithoutFinish_StillReportsErrors()
    {
        var collector = new PackerBuildCollector();
        collector.OnEvent(new PackerBuildError("packer", "Process exited with code 1"));

        var result = collector.Complete();
        result.Success.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void Complete_FinishedSuccessWithoutArtifactId_NoArtifactAdded()
    {
        var collector = new PackerBuildCollector();
        collector.OnEvent(new PackerBuildFinished("amazon-ebs", Success: true, ArtifactId: null));

        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.Artifacts.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_MixedBuilds_ErrorOnOneMeansFailure()
    {
        var collector = new PackerBuildCollector();
        collector.OnEvent(new PackerBuildFinished("amazon-ebs", true, "ami-111"));
        collector.OnEvent(new PackerBuildError("docker", "timeout"));
        collector.OnEvent(new PackerBuildFinished("docker", false, null));

        var result = collector.Complete();
        result.Success.ShouldBeFalse();
        result.Artifacts.Count.ShouldBe(1);
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void Complete_UnrelatedEvents_AreIgnored()
    {
        var collector = new PackerBuildCollector();
        collector.OnEvent(new PackerBuildOutput("amazon-ebs", "Creating AMI..."));
        collector.OnEvent(new PackerBuildStarted("amazon-ebs", "my-build"));
        collector.OnEvent(new PackerMachineReadableEvent(123, "target", "ui", new[] { "data" }));
        collector.OnEvent(new PackerProvisionerOutput("amazon-ebs", "shell", "running"));
        collector.OnEvent(new PackerOutputLine("random text", OutputSource.StdOut));

        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.Artifacts.ShouldBeEmpty();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_ArtifactBuilderType_IsUnknown()
    {
        var collector = new PackerBuildCollector();
        collector.OnEvent(new PackerBuildFinished("amazon-ebs", true, "ami-123"));

        var result = collector.Complete();
        result.Artifacts[0].BuilderType.ShouldBe("unknown");
        result.Artifacts[0].BuildName.ShouldBe("amazon-ebs");
    }

    [Fact]
    public void Complete_FailedFinish_NoError_StillFails()
    {
        // Catches && vs || mutation on PackerBuildResult.cs:41
        var collector = new PackerBuildCollector();
        collector.OnEvent(new PackerBuildFinished("docker", Success: false, ArtifactId: null));

        var result = collector.Complete();
        result.Success.ShouldBeFalse();
        result.Errors.ShouldBeEmpty();
        result.Artifacts.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_EmptyArtifactId_IsAdded()
    {
        // Documents: `not null` matches empty string ""
        var collector = new PackerBuildCollector();
        collector.OnEvent(new PackerBuildFinished("aws", true, ""));

        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.Artifacts.Count.ShouldBe(1);
        result.Artifacts[0].ArtifactId.ShouldBe("");
    }
}

// ── Integration Tests — Parser → Collector ──────────────────────────────────

public sealed class PackerIntegrationTests
{
    private static PackerBuildResult RunBuild(params string[] lines)
    {
        var parser = new PackerBuildParser();
        var collector = new PackerBuildCollector();

        foreach (var text in lines)
        {
            var line = new OutputLine(text, OutputSource.StdOut);
            foreach (var evt in parser.ParseLine(line))
                collector.OnEvent(evt);
        }

        foreach (var evt in parser.Complete(0))
            collector.OnEvent(evt);

        return collector.Complete();
    }

    private static PackerBuildResult RunBuildWithExitCode(int exitCode, params string[] lines)
    {
        var parser = new PackerBuildParser();
        var collector = new PackerBuildCollector();

        foreach (var text in lines)
        {
            var line = new OutputLine(text, OutputSource.StdOut);
            foreach (var evt in parser.ParseLine(line))
                collector.OnEvent(evt);
        }

        foreach (var evt in parser.Complete(exitCode))
            collector.OnEvent(evt);

        return collector.Complete();
    }

    [Fact]
    public void DockerAlpine_SuccessfulBuild_FullLifecycle()
    {
        var result = RunBuild(
            "==> docker.alpine: Creating a temporary directory...",
            "==> docker.alpine: Pulling Docker image: alpine:latest",
            "==> docker.alpine: Starting docker container...",
            "==> docker.alpine: Provisioning with shell script: /tmp/script.sh",
            "    docker.alpine: Installing curl...",
            "    docker.alpine: Done.",
            "==> docker.alpine: Committing container",
            "Build 'docker.alpine' finished.",
            "==> Builds finished. The artifacts of successful builds are:",
            "--> docker.alpine: sha256:abcdef1234567890"
        );

        result.Success.ShouldBeTrue();
        result.Artifacts.Count.ShouldBe(1);
        result.Artifacts[0].ArtifactId.ShouldBe("sha256:abcdef1234567890");
        result.Artifacts[0].BuildName.ShouldBe("docker.alpine");
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void DockerAlpine_FailedBuild_FullLifecycle()
    {
        var result = RunBuildWithExitCode(1,
            "==> docker.alpine: Creating a temporary directory...",
            "==> docker.alpine: Pulling Docker image: alpine:latest",
            "==> docker.alpine (error): Cannot connect to Docker daemon",
            "Build 'docker.alpine' errored after 5s: Cannot connect to Docker daemon"
        );

        result.Success.ShouldBeFalse();
        result.Artifacts.ShouldBeEmpty();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void MultiBuild_OneSucceeds_OneFails()
    {
        var result = RunBuildWithExitCode(1,
            "==> amazon-ebs.web: Prevalidating AMI Name...",
            "==> docker.alpine: Pulling Docker image: alpine:latest",
            "==> amazon-ebs.web: Creating AMI...",
            "==> docker.alpine (error): Cannot connect to Docker daemon",
            "Build 'docker.alpine' errored after 5s: connection refused",
            "--> amazon-ebs.web: AMIs were created: us-east-1: ami-0abc123",
            "Build 'amazon-ebs.web' finished."
        );

        result.Success.ShouldBeFalse();
        result.Artifacts.Count.ShouldBe(1);
        result.Artifacts[0].BuildName.ShouldBe("amazon-ebs.web");
        result.Artifacts[0].ArtifactId.ShouldBe("AMIs were created: us-east-1: ami-0abc123");
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(1);
    }
}

// ── Fuzz / Property-Based Tests ─────────────────────────────────────────────

public sealed class PackerBuildParserFuzzTests
{
    private readonly PackerBuildParser _parser = new();

    [Fact]
    public void ParseLine_NeverThrows_OnAnyInput()
    {
        Gens.AnyOutputLine.Sample(line =>
        {
            var events = _parser.ParseLine(line).ToList();
            events.ShouldNotBeNull();
        });
    }

    [Fact]
    public void ParseLine_AlwaysReturnsValidEvents_OnAnyInput()
    {
        Gens.AnyOutputLine.Sample(line =>
        {
            var events = _parser.ParseLine(line).ToList();
            foreach (var evt in events)
                evt.ShouldBeAssignableTo<PackerEvent>();
        });
    }

    [Fact]
    public void ParseLine_NonEmpty_AlwaysYieldsAtLeastOneEvent()
    {
        Gens.NonEmpty.Sample(text =>
        {
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBeGreaterThanOrEqualTo(1);
        });
    }

    [Fact]
    public void Complete_NeverThrows_OnAnyExitCode()
    {
        Gen.Int.Sample(exitCode =>
        {
            var events = _parser.Complete(exitCode).ToList();
            events.ShouldNotBeNull();
        });
    }
}

public sealed class PackerMachineReadableParserFuzzTests
{
    private readonly PackerMachineReadableParser _parser = new();

    [Fact]
    public void ParseLine_NeverThrows_OnAnyInput()
    {
        Gens.AnyOutputLine.Sample(line =>
        {
            var events = _parser.ParseLine(line).ToList();
            events.ShouldNotBeNull();
        });
    }

    [Fact]
    public void ParseLine_StdOut_NonEmpty_AlwaysYieldsEvent()
    {
        Gens.NonEmpty.Sample(text =>
        {
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBeGreaterThanOrEqualTo(1);
        });
    }

    [Fact]
    public void ParseLine_StdErr_AlwaysYieldsOutputLine()
    {
        Gen.String.Sample(text =>
        {
            var line = new OutputLine(text ?? "", OutputSource.StdErr);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            events[0].ShouldBeOfType<PackerOutputLine>();
        });
    }

    [Fact]
    public void ParseLine_ValidFormat_AlwaysYieldsMachineReadableEvent()
    {
        Gen.Select(
            Gen.Long[0, long.MaxValue],
            Gen.String,
            Gen.String,
            Gen.String)
        .Sample((ts, target, evtType, data) =>
        {
            target ??= "";
            evtType ??= "";
            data ??= "";
            var text = $"{ts},{target},{evtType},{data}";
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            var evt = events[0].ShouldBeOfType<PackerMachineReadableEvent>();
            evt.Timestamp.ShouldBe(ts);
        });
    }

    [Fact]
    public void Complete_NeverThrows_OnAnyExitCode()
    {
        Gen.Int.Sample(exitCode =>
        {
            var events = _parser.Complete(exitCode).ToList();
            events.ShouldNotBeNull();
        });
    }
}

public sealed class PackerBuildCollectorFuzzTests
{
    [Fact]
    public void OnEvent_ThenComplete_NeverThrows_OnRandomEventSequence()
    {
        var anyEvent = Gen.OneOf<PackerEvent>(
            Gen.Select(Gen.String, Gen.String)
                .Select((a, b) => (PackerEvent)new PackerBuildOutput(a ?? "", b ?? "")),
            Gen.Select(Gen.String, Gen.String)
                .Select((a, b) => (PackerEvent)new PackerBuildError(a ?? "", b ?? "")),
            Gen.Select(Gen.String, Gen.Bool, Gen.Null(Gen.String))
                .Select((n, s, a) => (PackerEvent)new PackerBuildFinished(n ?? "", s, a)),
            Gen.Select(Gen.String, Gen.String)
                .Select((a, b) => (PackerEvent)new PackerBuildStarted(a ?? "", b ?? "")),
            Gen.String
                .Select(t => (PackerEvent)new PackerOutputLine(t ?? "", OutputSource.StdOut))
        );

        anyEvent.List.Sample(events =>
        {
            var collector = new PackerBuildCollector();
            foreach (var evt in events)
                collector.OnEvent(evt);
            var result = collector.Complete();
            result.ShouldNotBeNull();
            result.Artifacts.ShouldNotBeNull();
            result.Errors.ShouldNotBeNull();
        });
    }

    [Fact]
    public void Complete_SuccessReflectsState()
    {
        var finishGen = Gen.String.Select(n => new PackerBuildFinished(n ?? "", true, "art-" + n)).Array;
        var errorGen = Gen.String.Select(n => new PackerBuildError(n ?? "", "err")).Array;

        Gen.Select(finishGen, errorGen).Sample((finishes, errors) =>
        {
            var collector = new PackerBuildCollector();
            foreach (var f in finishes) collector.OnEvent(f);
            foreach (var e in errors) collector.OnEvent(e);
            var result = collector.Complete();

            if (errors.Length > 0)
                result.Success.ShouldBeFalse();
            else
                result.Success.ShouldBeTrue();

            result.Artifacts.Count.ShouldBe(finishes.Length);
            result.Errors.Count.ShouldBe(errors.Length);
        });
    }
}

public sealed class PackerBuildParserRoundTripFuzzTests
{
    private readonly PackerBuildParser _parser = new();

    // \S+? in regex means non-whitespace, non-empty identifier
    private static readonly Gen<string> AnyBuildName =
        Gen.Char['a', 'z'].Array[1, 20].Select(cs => new string(cs));

    // Message text: non-empty, no leading/trailing whitespace issues
    private static readonly Gen<string> AnyMessage =
        Gen.Char['a', 'z'].Array[1, 30].Select(cs => new string(cs));

    [Fact]
    public void BuildOutput_Format_RoundTrips()
    {
        Gen.Select(AnyBuildName, AnyMessage).Sample((buildName, message) =>
        {
            var text = $"==> {buildName}: {message}";
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            var evt = events[0].ShouldBeOfType<PackerBuildOutput>();
            evt.BuildName.ShouldBe(buildName);
            evt.Message.ShouldBe(message);
        });
    }

    [Fact]
    public void BuildError_Format_RoundTrips()
    {
        Gen.Select(AnyBuildName, AnyMessage).Sample((buildName, message) =>
        {
            var text = $"==> {buildName} (error): {message}";
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            var evt = events[0].ShouldBeOfType<PackerBuildError>();
            evt.BuildName.ShouldBe(buildName);
            evt.Message.ShouldBe(message);
        });
    }

    [Fact]
    public void BuildFinished_Format_RoundTrips()
    {
        AnyBuildName.Sample(buildName =>
        {
            var text = $"Build '{buildName}' finished.";
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            var evt = events[0].ShouldBeOfType<PackerBuildFinished>();
            evt.BuildName.ShouldBe(buildName);
            evt.Success.ShouldBeTrue();
            evt.ArtifactId.ShouldBeNull();
        });
    }

    [Fact]
    public void BuildErrored_Format_RoundTrips()
    {
        AnyBuildName.Sample(buildName =>
        {
            var text = $"Build '{buildName}' errored after 1m: some error";
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            var evt = events[0].ShouldBeOfType<PackerBuildFinished>();
            evt.BuildName.ShouldBe(buildName);
            evt.Success.ShouldBeFalse();
        });
    }

    [Fact]
    public void Artifact_Format_RoundTrips()
    {
        Gen.Select(AnyBuildName, AnyMessage).Sample((buildName, artifactId) =>
        {
            var text = $"--> {buildName}: {artifactId}";
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            var evt = events[0].ShouldBeOfType<PackerBuildFinished>();
            evt.BuildName.ShouldBe(buildName);
            evt.ArtifactId.ShouldBe(artifactId);
            evt.Success.ShouldBeTrue();
        });
    }

    [Fact]
    public void ProvisionerOutput_Format_RoundTrips()
    {
        Gen.Select(AnyBuildName, AnyMessage).Sample((buildName, message) =>
        {
            var text = $"    {buildName}: {message}";
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            var evt = events[0].ShouldBeOfType<PackerProvisionerOutput>();
            evt.BuildName.ShouldBe(buildName);
            evt.Message.ShouldBe(message);
            evt.ProvisionerType.ShouldBe("shell");
        });
    }

    [Fact]
    public void ParseLine_YieldsAtMostOneEvent()
    {
        Gens.AnyOutputLine.Sample(line =>
        {
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBeLessThanOrEqualTo(1);
        });
    }

    [Fact]
    public void Complete_ZeroAlwaysEmpty_NonZeroAlwaysError()
    {
        Gen.Int[1, int.MaxValue].Sample(code =>
        {
            var events = _parser.Complete(code).ToList();
            events.Count.ShouldBe(1);
            var err = events[0].ShouldBeOfType<PackerBuildError>();
            err.Message.ShouldContain(code.ToString());
        });
    }
}

public sealed class PackerMachineReadableParserRoundTripFuzzTests
{
    private readonly PackerMachineReadableParser _parser = new();

    [Fact]
    public void PackerCommaEscape_IsAlwaysUnescaped()
    {
        Gen.Select(
            Gen.Long[0, long.MaxValue],
            Gen.String,
            Gen.String)
        .Sample((ts, target, data) =>
        {
            target ??= "";
            data ??= "";
            var escapedData = data.Replace(",", "%!(PACKER_COMMA)");
            var text = $"{ts},{target},ui,{escapedData}";
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            var evt = events[0].ShouldBeOfType<PackerMachineReadableEvent>();
            // The unescaped data should contain commas where escapes were
            var joined = string.Join(",", evt.Data);
            joined.ShouldNotContain("%!(PACKER_COMMA)");
        });
    }

    [Fact]
    public void ParseLine_InvalidTimestamp_AlwaysYieldsOutputLine()
    {
        Gen.Select(Gens.NonEmpty, Gen.String, Gen.String).Sample((badTs, target, evtType) =>
        {
            if (long.TryParse(badTs, out _)) return; // skip if accidentally valid

            target ??= "";
            evtType ??= "";
            var text = $"{badTs},{target},{evtType}";
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            events[0].ShouldBeOfType<PackerOutputLine>();
        });
    }

    [Fact]
    public void ParseLine_MultipleDataFields_AllPreserved()
    {
        Gen.Select(
            Gen.Long[0, long.MaxValue],
            Gen.Int[1, 10])
        .Sample((ts, fieldCount) =>
        {
            var fields = Enumerable.Range(0, fieldCount).Select(i => $"field{i}").ToArray();
            var text = $"{ts},target,type,{string.Join(",", fields)}";
            var line = new OutputLine(text, OutputSource.StdOut);
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBe(1);
            var evt = events[0].ShouldBeOfType<PackerMachineReadableEvent>();
            evt.Data.Length.ShouldBe(fieldCount);
            for (var i = 0; i < fieldCount; i++)
                evt.Data[i].ShouldBe($"field{i}");
        });
    }

    [Fact]
    public void ParseLine_YieldsAtMostOneEvent()
    {
        Gens.AnyOutputLine.Sample(line =>
        {
            var events = _parser.ParseLine(line).ToList();
            events.Count.ShouldBeLessThanOrEqualTo(1);
        });
    }
}

public sealed class PackerCommandSerializationFuzzTests
{
    [Fact]
    public void Build_StringProperties_NeverNull_InArgs()
    {
        Gen.Select(Gen.String, Gen.String, Gen.String, Gen.String, Gen.String)
        .Sample((color, except, only, onError, varFile) =>
        {
            var cmd = new PackerBuildCommand
            {
                Color = color, Except = except, Only = only,
                OnError = onError, VarFile = varFile
            };
            var args = cmd.ToArguments();
            foreach (var arg in args)
            {
                arg.ShouldNotBeNull();
                arg.ShouldContain("=");
            }
        });
    }

    [Fact]
    public void Build_BoolProperties_SerializeAsTrueOrFalse()
    {
        Gen.Select(Gen.Bool, Gen.Bool, Gen.Bool, Gen.Bool)
        .Sample((force, debug, machineReadable, timestampUi) =>
        {
            var cmd = new PackerBuildCommand
            {
                Force = force, Debug = debug,
                MachineReadable = machineReadable, TimestampUi = timestampUi
            };
            var args = cmd.ToArguments();
            args.Count.ShouldBe(4);
            foreach (var arg in args)
                arg.ShouldMatch(@"-\S+=(?:true|false)");
        });
    }

    [Fact]
    public void Build_ToArguments_CountEqualsSetProperties()
    {
        Gen.Select(Gen.Bool, Gen.Bool, Gen.Bool, Gen.Bool)
        .Sample((setColor, setForce, setExcept, setVarFile) =>
        {
            var cmd = new PackerBuildCommand
            {
                Color = setColor ? "auto" : null,
                Force = setForce ? true : null,
                Except = setExcept ? "docker" : null,
                VarFile = setVarFile ? "v.json" : null
            };
            var expected = new[] { setColor, setForce, setExcept, setVarFile }.Count(x => x);
            cmd.ToArguments().Count.ShouldBe(expected);
        });
    }

    [Fact]
    public void Validate_ToArguments_AllArgsStartWithDash()
    {
        Gen.Select(Gen.Bool, Gen.Bool, Gen.Bool, Gen.Bool)
        .Sample((syntaxOnly, machineReadable, noWarn, evalDs) =>
        {
            var cmd = new PackerValidateCommand
            {
                SyntaxOnly = syntaxOnly ? true : null,
                MachineReadable = machineReadable ? true : null,
                NoWarnUndeclaredVar = noWarn ? true : null,
                EvaluateDatasources = evalDs ? true : null
            };
            var args = cmd.ToArguments();
            foreach (var arg in args)
                arg.ShouldStartWith("-");
        });
    }
}
