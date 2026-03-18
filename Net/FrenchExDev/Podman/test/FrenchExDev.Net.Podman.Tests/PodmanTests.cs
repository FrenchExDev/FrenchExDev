using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using FrenchExDev.Net.Podman;
using Shouldly;

namespace FrenchExDev.Net.Podman.Tests;

// ── Command Serialization Tests ─────────────────────────────────────────────

public sealed class CommandSerializationTests
{
    // ── run ──

    [Fact]
    public void Run_DetachTrue_SerializesAsFlag()
    {
        var cmd = new PodmanRunCommand { Detach = true };
        var args = cmd.ToArguments();
        args.ShouldContain("--detach");
    }

    [Fact]
    public void Run_DetachNull_NotIncluded()
    {
        var cmd = new PodmanRunCommand();
        var args = cmd.ToArguments();
        args.ShouldNotContain("--detach");
    }

    [Fact]
    public void Run_Name_SerializesWithValue()
    {
        var cmd = new PodmanRunCommand { Name = "my-container" };
        var args = cmd.ToArguments();
        args.ShouldContain("--name");
        args.ShouldContain("my-container");
    }

    [Fact]
    public void Run_MultipleVolumes_SerializesEach()
    {
        var cmd = new PodmanRunCommand { Volume = ["/host:/container", "/data:/data"] };
        var args = cmd.ToArguments();
        args.Count(a => a == "--volume").ShouldBe(2);
        args.ShouldContain("/host:/container");
        args.ShouldContain("/data:/data");
    }

    [Fact]
    public void Run_BoolFlags_OnlyTrueIncluded()
    {
        var cmd = new PodmanRunCommand
        {
            Tty = true,
            Interactive = true,
            Rm = true,
            Privileged = null
        };
        var args = cmd.ToArguments();
        args.ShouldContain("--tty");
        args.ShouldContain("--interactive");
        args.ShouldContain("--rm");
        args.ShouldNotContain("--privileged");
    }

    [Fact]
    public void Run_NullOptions_EmptyArgs()
    {
        var cmd = new PodmanRunCommand();
        var args = cmd.ToArguments();
        args.Count.ShouldBe(0);
    }

    [Fact]
    public void Run_CommandPath_IsRun()
    {
        var cmd = new PodmanRunCommand();
        cmd.CommandPath.ShouldBe(new[] { "run" });
    }

    [Fact]
    public void Run_ImplementsICliCommand()
    {
        var cmd = new PodmanRunCommand();
        cmd.ShouldBeAssignableTo<ICliCommand>();
    }

    [Fact]
    public void Run_EnvList_SerializesCorrectly()
    {
        var cmd = new PodmanRunCommand { Env = ["FOO=bar", "BAZ=qux"] };
        var args = cmd.ToArguments();
        args.Count(a => a == "--env").ShouldBe(2);
        args.ShouldContain("FOO=bar");
        args.ShouldContain("BAZ=qux");
    }

    [Fact]
    public void Run_PublishPorts_SerializesCorrectly()
    {
        var cmd = new PodmanRunCommand { Publish = ["8080:80", "443:443"] };
        var args = cmd.ToArguments();
        args.Count(a => a == "--publish").ShouldBe(2);
        args.ShouldContain("8080:80");
    }

    // ── ps ──

    [Fact]
    public void Ps_All_SerializesAsFlag()
    {
        var cmd = new PodmanPsCommand { All = true };
        var args = cmd.ToArguments();
        args.ShouldContain("--all");
    }

    [Fact]
    public void Ps_Format_SerializesWithValue()
    {
        var cmd = new PodmanPsCommand { Format = "json" };
        var args = cmd.ToArguments();
        args.ShouldContain("--format");
        args.ShouldContain("json");
    }

    [Fact]
    public void Ps_FilterList_SerializesEach()
    {
        var cmd = new PodmanPsCommand { Filter = ["status=running", "name=test"] };
        var args = cmd.ToArguments();
        args.Count(a => a == "--filter").ShouldBe(2);
    }

    [Fact]
    public void Ps_AllFlags_Serialized()
    {
        var cmd = new PodmanPsCommand
        {
            All = true, Quiet = true, NoTrunc = true,
            Noheading = true, Size = true, Pod = true
        };
        var args = cmd.ToArguments();
        args.ShouldContain("--all");
        args.ShouldContain("--quiet");
        args.ShouldContain("--no-trunc");
        args.ShouldContain("--noheading");
        args.ShouldContain("--size");
        args.ShouldContain("--pod");
    }

    [Fact]
    public void Ps_NullOptions_EmptyArgs()
    {
        var cmd = new PodmanPsCommand();
        var args = cmd.ToArguments();
        args.Count.ShouldBe(0);
    }

    [Fact]
    public void Ps_CommandPath_IsPs()
    {
        var cmd = new PodmanPsCommand();
        cmd.CommandPath.ShouldBe(new[] { "ps" });
    }

    // ── network ls ──

    [Fact]
    public void NetworkLs_CommandPath()
    {
        var cmd = new PodmanNetworkLsCommand();
        cmd.CommandPath.ShouldBe(new[] { "network", "ls" });
    }

    [Fact]
    public void NetworkLs_Format_SerializesCorrectly()
    {
        var cmd = new PodmanNetworkLsCommand { Format = "{{.Name}}" };
        var args = cmd.ToArguments();
        args.ShouldContain("--format");
        args.ShouldContain("{{.Name}}");
    }

    [Fact]
    public void NetworkLs_Quiet_SerializesAsFlag()
    {
        var cmd = new PodmanNetworkLsCommand { Quiet = true };
        var args = cmd.ToArguments();
        args.ShouldContain("--quiet");
    }

    [Fact]
    public void NetworkLs_NullOptions_EmptyArgs()
    {
        var cmd = new PodmanNetworkLsCommand();
        cmd.ToArguments().Count.ShouldBe(0);
    }

    // ── image list ──

    [Fact]
    public void ImageList_CommandPath()
    {
        var cmd = new PodmanImageListCommand();
        cmd.CommandPath.ShouldBe(new[] { "image", "list" });
    }

    // ── build ──

    [Fact]
    public void Build_Tag_SerializesAsFlag()
    {
        var cmd = new PodmanBuildCommand { Tag = true };
        var args = cmd.ToArguments();
        args.ShouldContain("--tag");
    }

    [Fact]
    public void Build_File_SerializesAsFlag()
    {
        var cmd = new PodmanBuildCommand { File = true };
        var args = cmd.ToArguments();
        args.ShouldContain("--file");
    }

    [Fact]
    public void Build_NullOptions_EmptyArgs()
    {
        var cmd = new PodmanBuildCommand();
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public void Build_CommandPath_IsBuild()
    {
        var cmd = new PodmanBuildCommand();
        cmd.CommandPath.ShouldBe(new[] { "build" });
    }
}

// ── Generated API Shape Tests ───────────────────────────────────────────────

public sealed class GeneratedCodeTests
{
    private static PodmanClient CreateClient() =>
        new(TestBindings.Create("podman"));

    // ── Static entry point ──

    [Fact]
    public void Podman_Create_ReturnsClient()
    {
        var client = Podman.Create(TestBindings.Create("podman"));
        client.ShouldNotBeNull();
    }

    // ── Top-level leaf commands via client ──

    [Fact]
    public async Task Client_Run_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.RunAsync(b => b.WithDetach(true).WithName("test"));
        cmd.ShouldNotBeNull();
        cmd.ShouldBeOfType<PodmanRunCommand>();
        cmd.Detach.ShouldBe(true);
        cmd.Name.ShouldBe("test");
    }

    [Fact]
    public async Task Client_Ps_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.PsAsync(b => b.WithAll(true).WithFormat("json"));
        cmd.All.ShouldBe(true);
        cmd.Format.ShouldBe("json");
    }

    [Fact]
    public async Task Client_Build_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.BuildAsync(b => b.WithFile(true));
        cmd.ShouldNotBeNull();
        cmd.File.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Exec_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.ExecAsync(b => b.WithDetach(true));
        cmd.Detach.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Pull_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.PullAsync(b => b.WithQuiet(true));
        cmd.Quiet.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Push_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.PushAsync(b => b.WithTlsVerify(true));
        cmd.TlsVerify.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Images_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.ImagesAsync(b => b.WithAll(true));
        cmd.All.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Inspect_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.InspectAsync(b => b.WithFormat("json"));
        cmd.Format.ShouldBe("json");
    }

    [Fact]
    public async Task Client_Info_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.InfoAsync(b => b.WithFormat("json"));
        cmd.Format.ShouldBe("json");
    }

    [Fact]
    public async Task Client_Kill_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.KillAsync(b => b.WithAll(true));
        cmd.All.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Rm_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.RmAsync(b => b.WithForce(true));
        cmd.Force.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Rmi_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.RmiAsync(b => b.WithForce(true));
        cmd.Force.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Stop_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.StopAsync(b => b.WithAll(true));
        cmd.All.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Start_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.StartAsync(b => b.WithAll(true));
        cmd.All.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Logs_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.LogsAsync(b => b.WithFollow(true));
        cmd.Follow.ShouldBe(true);
    }

    // ── No-option commands ──

    [Fact]
    public async Task Client_Run_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.RunAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public async Task Client_Ps_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.PsAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
        cmd.CommandPath.ShouldBe(new[] { "ps" });
    }

    // ── Sub-group navigation ──

    [Fact]
    public void Client_Container_Group_Exists()
    {
        var client = CreateClient();
        var group = client.Container;
        group.ShouldNotBeNull();
    }

    [Fact]
    public async Task Client_Container_List_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.Container.ListAsync(b => b.WithAll(true));
        cmd.All.ShouldBe(true);
        cmd.CommandPath.ShouldBe(new[] { "container", "list" });
    }

    [Fact]
    public void Client_Image_Group_Exists()
    {
        var client = CreateClient();
        var group = client.Image;
        group.ShouldNotBeNull();
    }

    [Fact]
    public async Task Client_Image_List_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.Image.ListAsync(b => b.WithAll(true));
        cmd.All.ShouldBe(true);
        cmd.CommandPath.ShouldBe(new[] { "image", "list" });
    }

    [Fact]
    public void Client_Network_Group_Exists()
    {
        var client = CreateClient();
        var group = client.Network;
        group.ShouldNotBeNull();
    }

    [Fact]
    public async Task Client_Network_Ls_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.Network.LsAsync(b => b.WithQuiet(true));
        cmd.Quiet.ShouldBe(true);
        cmd.CommandPath.ShouldBe(new[] { "network", "ls" });
    }

    [Fact]
    public void Client_Volume_Group_Exists()
    {
        var client = CreateClient();
        var group = client.Volume;
        group.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Pod_Group_Exists()
    {
        var client = CreateClient();
        var group = client.Pod;
        group.ShouldNotBeNull();
    }

    [Fact]
    public void Client_System_Group_Exists()
    {
        var client = CreateClient();
        var group = client.System;
        group.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Secret_Group_Exists()
    {
        var client = CreateClient();
        var group = client.Secret;
        group.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Manifest_Group_Exists()
    {
        var client = CreateClient();
        var group = client.Manifest;
        group.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Healthcheck_Group_Exists()
    {
        var client = CreateClient();
        var group = client.Healthcheck;
        group.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Generate_Group_Exists()
    {
        var client = CreateClient();
        var group = client.Generate;
        group.ShouldNotBeNull();
    }

    // ── Builder fluent chain ──

    [Fact]
    public void Builder_FluentChain_ReturnsBuilder()
    {
        var builder = new PodmanRunCommandBuilder()
            .WithDetach(true)
            .WithName("test")
            .WithTty(true)
            .WithInteractive(true);

        builder.ShouldBeOfType<PodmanRunCommandBuilder>();
    }

    [Fact]
    public void PsBuilder_FluentChain_ReturnsBuilder()
    {
        var builder = new PodmanPsCommandBuilder()
            .WithAll(true)
            .WithFormat("json")
            .WithQuiet(true);

        builder.ShouldBeOfType<PodmanPsCommandBuilder>();
    }

    [Fact]
    public void Builder_WithList_ReturnsBuilder()
    {
        var builder = new PodmanRunCommandBuilder()
            .WithVolume(["/host:/container"])
            .WithEnv(["FOO=bar"]);

        builder.ShouldBeOfType<PodmanRunCommandBuilder>();
    }

    // ── Run with many options via builder ──

    [Fact]
    public async Task Client_Run_AllCommonOptions_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.RunAsync(b => b
            .WithDetach(true)
            .WithName("mycontainer")
            .WithTty(true)
            .WithInteractive(true)
            .WithRm(true)
            .WithPublishAll(true)
            .WithHostname("myhost")
            .WithWorkdir("/app")
            .WithUser("nobody"));

        cmd.Detach.ShouldBe(true);
        cmd.Name.ShouldBe("mycontainer");
        cmd.Tty.ShouldBe(true);
        cmd.Interactive.ShouldBe(true);
        cmd.Rm.ShouldBe(true);
        cmd.PublishAll.ShouldBe(true);
        cmd.Hostname.ShouldBe("myhost");
        cmd.Workdir.ShouldBe("/app");
        cmd.User.ShouldBe("nobody");
        // 5 bool flags (1 arg each) + 4 string options (2 args each) = 13
        cmd.ToArguments().Count.ShouldBe(13);
    }

    // ── Ps with all options via builder ──

    [Fact]
    public async Task Client_Ps_AllOptions_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.PsAsync(b => b
            .WithAll(true)
            .WithFormat("json")
            .WithQuiet(true)
            .WithNoTrunc(true)
            .WithNoheading(true)
            .WithSize(true)
            .WithPod(true)
            .WithNs(true)
            .WithSort(true)
            .WithSync(true)
            .WithExternal(true)
            .WithLast("5")
            .WithWatch("2"));

        // 10 bool flags (1 arg each) + 3 string options (2 args each) = 16
        cmd.ToArguments().Count.ShouldBe(16);
    }
}

// ── Descriptor Tests ────────────────────────────────────────────────────────

public sealed class DescriptorTests
{
    [Fact]
    public void Descriptor_Exists()
    {
        var descriptor = new PodmanDescriptor();
        descriptor.ShouldNotBeNull();
    }
}
