using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using FrenchExDev.Net.Docker;
using Shouldly;

namespace FrenchExDev.Net.Docker.Tests;

// ── Command Serialization Tests ─────────────────────────────────────────────

public sealed class CommandSerializationTests
{
    // ── container run ──

    [Fact]
    public void ContainerRun_DetachTrue_SerializesAsFlag()
    {
        var cmd = new DockerContainerRunCommand { Detach = true };
        var args = cmd.ToArguments();
        args.ShouldContain("--detach");
    }

    [Fact]
    public void ContainerRun_DetachNull_NotIncluded()
    {
        var cmd = new DockerContainerRunCommand();
        var args = cmd.ToArguments();
        args.ShouldNotContain("--detach");
    }

    [Fact]
    public void ContainerRun_Name_SerializesWithValue()
    {
        var cmd = new DockerContainerRunCommand { Name = "my-container" };
        var args = cmd.ToArguments();
        args.ShouldContain("--name");
        args.ShouldContain("my-container");
    }

    [Fact]
    public void ContainerRun_BoolFlags_OnlyTrueIncluded()
    {
        var cmd = new DockerContainerRunCommand
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
    public void ContainerRun_NullOptions_EmptyArgs()
    {
        var cmd = new DockerContainerRunCommand();
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public void ContainerRun_CommandPath()
    {
        var cmd = new DockerContainerRunCommand();
        cmd.CommandPath.ShouldBe(new[] { "container", "run" });
    }

    [Fact]
    public void ContainerRun_ImplementsICliCommand()
    {
        var cmd = new DockerContainerRunCommand();
        cmd.ShouldBeAssignableTo<ICliCommand>();
    }

    [Fact]
    public void ContainerRun_StringOptions_SerializeCorrectly()
    {
        var cmd = new DockerContainerRunCommand
        {
            Hostname = "myhost",
            Workdir = "/app",
            User = "nobody"
        };
        var args = cmd.ToArguments();
        args.ShouldContain("--hostname");
        args.ShouldContain("myhost");
        args.ShouldContain("--workdir");
        args.ShouldContain("/app");
        args.ShouldContain("--user");
        args.ShouldContain("nobody");
    }

    // ── container ls ──

    [Fact]
    public void ContainerLs_All_SerializesAsFlag()
    {
        var cmd = new DockerContainerLsCommand { All = true };
        var args = cmd.ToArguments();
        args.ShouldContain("--all");
    }

    [Fact]
    public void ContainerLs_Format_SerializesWithValue()
    {
        var cmd = new DockerContainerLsCommand { Format = "json" };
        var args = cmd.ToArguments();
        args.ShouldContain("--format");
        args.ShouldContain("json");
    }

    [Fact]
    public void ContainerLs_AllFlags_Serialized()
    {
        var cmd = new DockerContainerLsCommand
        {
            All = true, Quiet = true, NoTrunc = true, Size = true
        };
        var args = cmd.ToArguments();
        args.ShouldContain("--all");
        args.ShouldContain("--quiet");
        args.ShouldContain("--no-trunc");
        args.ShouldContain("--size");
    }

    [Fact]
    public void ContainerLs_NullOptions_EmptyArgs()
    {
        var cmd = new DockerContainerLsCommand();
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public void ContainerLs_CommandPath()
    {
        var cmd = new DockerContainerLsCommand();
        cmd.CommandPath.ShouldBe(new[] { "container", "ls" });
    }

    // ── network ls ──

    [Fact]
    public void NetworkLs_CommandPath()
    {
        var cmd = new DockerNetworkLsCommand();
        cmd.CommandPath.ShouldBe(new[] { "network", "ls" });
    }

    [Fact]
    public void NetworkLs_Format_SerializesCorrectly()
    {
        var cmd = new DockerNetworkLsCommand { Format = "{{.Name}}" };
        var args = cmd.ToArguments();
        args.ShouldContain("--format");
        args.ShouldContain("{{.Name}}");
    }

    [Fact]
    public void NetworkLs_Quiet_SerializesAsFlag()
    {
        var cmd = new DockerNetworkLsCommand { Quiet = true };
        cmd.ToArguments().ShouldContain("--quiet");
    }

    [Fact]
    public void NetworkLs_NullOptions_EmptyArgs()
    {
        var cmd = new DockerNetworkLsCommand();
        cmd.ToArguments().Count.ShouldBe(0);
    }

    // ── image ls ──

    [Fact]
    public void ImageLs_CommandPath()
    {
        var cmd = new DockerImageLsCommand();
        cmd.CommandPath.ShouldBe(new[] { "image", "ls" });
    }

    [Fact]
    public void ImageLs_All_SerializesAsFlag()
    {
        var cmd = new DockerImageLsCommand { All = true };
        cmd.ToArguments().ShouldContain("--all");
    }

    // ── builder build ──

    [Fact]
    public void BuilderBuild_File_SerializesWithValue()
    {
        var cmd = new DockerBuilderBuildCommand { File = "Dockerfile.prod" };
        var args = cmd.ToArguments();
        args.ShouldContain("--file");
        args.ShouldContain("Dockerfile.prod");
    }

    [Fact]
    public void BuilderBuild_NullOptions_EmptyArgs()
    {
        var cmd = new DockerBuilderBuildCommand();
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public void BuilderBuild_CommandPath()
    {
        var cmd = new DockerBuilderBuildCommand();
        cmd.CommandPath.ShouldBe(new[] { "builder", "build" });
    }

    [Fact]
    public void BuilderBuild_CacheFrom_SerializesList()
    {
        var cmd = new DockerBuilderBuildCommand { CacheFrom = ["registry/image:latest", "local"] };
        var args = cmd.ToArguments();
        args.Count(a => a == "--cache-from").ShouldBe(2);
        args.ShouldContain("registry/image:latest");
        args.ShouldContain("local");
    }
}

// ── Generated API Shape Tests ───────────────────────────────────────────────

public sealed class GeneratedCodeTests
{
    private static DockerClient CreateClient() =>
        new(TestBindings.Create("docker"));

    // ── Static entry point ──

    [Fact]
    public void Docker_Create_ReturnsClient()
    {
        var client = Docker.Create(TestBindings.Create("docker"));
        client.ShouldNotBeNull();
    }

    // ── Top-level leaf commands via client ──

    [Fact]
    public async Task Client_Inspect_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.InspectAsync(b => b.WithFormat("json"));
        cmd.Format.ShouldBe("json");
    }

    [Fact]
    public async Task Client_Kill_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.KillAsync(b => b.WithSignal("SIGTERM"));
        cmd.Signal.ShouldBe("SIGTERM");
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
        var cmd = await client.StopAsync(b => b.WithTime("10"));
        cmd.Time.ShouldBe("10");
    }

    [Fact]
    public async Task Client_Start_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.StartAsync(b => b.WithDetachKeys("ctrl-c"));
        cmd.DetachKeys.ShouldBe("ctrl-c");
    }

    [Fact]
    public async Task Client_Logs_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.LogsAsync(b => b.WithFollow(true));
        cmd.Follow.ShouldBe(true);
    }

    [Fact]
    public async Task Client_Pause_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.PauseAsync(b => { });
        cmd.ShouldNotBeNull();
    }

    [Fact]
    public async Task Client_Unpause_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.UnpauseAsync(b => { });
        cmd.ShouldNotBeNull();
    }

    // ── No-option commands ──

    [Fact]
    public async Task Client_ContainerRun_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.Container.RunAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public async Task Client_ContainerLs_NoProperties_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.Container.LsAsync(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
        cmd.CommandPath.ShouldBe(new[] { "container", "ls" });
    }

    // ── Sub-group navigation ──

    [Fact]
    public void Client_Container_Group_Exists()
    {
        var client = CreateClient();
        client.Container.ShouldNotBeNull();
    }

    [Fact]
    public async Task Client_Container_Ls_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.Container.LsAsync(b => b.WithAll(true));
        cmd.All.ShouldBe(true);
        cmd.CommandPath.ShouldBe(new[] { "container", "ls" });
    }

    [Fact]
    public void Client_Image_Group_Exists()
    {
        var client = CreateClient();
        client.Image.ShouldNotBeNull();
    }

    [Fact]
    public async Task Client_Image_Ls_ReturnsCommand()
    {
        var client = CreateClient();
        var cmd = await client.Image.LsAsync(b => b.WithAll(true));
        cmd.All.ShouldBe(true);
        cmd.CommandPath.ShouldBe(new[] { "image", "ls" });
    }

    [Fact]
    public void Client_Network_Group_Exists()
    {
        var client = CreateClient();
        client.Network.ShouldNotBeNull();
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
        client.Volume.ShouldNotBeNull();
    }

    [Fact]
    public void Client_System_Group_Exists()
    {
        var client = CreateClient();
        client.System.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Plugin_Group_Exists()
    {
        var client = CreateClient();
        client.Plugin.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Manifest_Group_Exists()
    {
        var client = CreateClient();
        client.Manifest.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Context_Group_Exists()
    {
        var client = CreateClient();
        client.Context.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Trust_Group_Exists()
    {
        var client = CreateClient();
        client.Trust.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Checkpoint_Group_Exists()
    {
        var client = CreateClient();
        client.Checkpoint.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Builder_Group_Exists()
    {
        var client = CreateClient();
        client.Builder.ShouldNotBeNull();
    }

    // ── Nested group navigation ──

    [Fact]
    public void Client_Trust_Key_Group_Exists()
    {
        var client = CreateClient();
        client.Trust.Key.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Trust_Signer_Group_Exists()
    {
        var client = CreateClient();
        client.Trust.Signer.ShouldNotBeNull();
    }

    // ── Builder fluent chain ──

    [Fact]
    public void RunBuilder_FluentChain_ReturnsBuilder()
    {
        var builder = new DockerContainerRunCommandBuilder()
            .WithDetach(true)
            .WithName("test")
            .WithTty(true)
            .WithInteractive(true);

        builder.ShouldBeOfType<DockerContainerRunCommandBuilder>();
    }

    [Fact]
    public void LsBuilder_FluentChain_ReturnsBuilder()
    {
        var builder = new DockerContainerLsCommandBuilder()
            .WithAll(true)
            .WithFormat("json")
            .WithQuiet(true);

        builder.ShouldBeOfType<DockerContainerLsCommandBuilder>();
    }

    // ── Run with many options via builder ──

    [Fact]
    public async Task Client_ContainerRun_AllCommonOptions_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.Container.RunAsync(b => b
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

    // ── ContainerLs with all options via builder ──

    [Fact]
    public async Task Client_ContainerLs_AllOptions_ViaBuilder()
    {
        var client = CreateClient();
        var cmd = await client.Container.LsAsync(b => b
            .WithAll(true)
            .WithFormat("json")
            .WithQuiet(true)
            .WithNoTrunc(true)
            .WithSize(true)
            .WithLatest(true)
            .WithLast("5"));

        // 5 bool flags (1 arg each) + 2 string options (2 args each) = 9
        cmd.ToArguments().Count.ShouldBe(9);
    }
}

// ── Descriptor Tests ────────────────────────────────────────────────────────

public sealed class DescriptorTests
{
    [Fact]
    public void DockerDescriptor_CanBeInstantiated()
    {
        var descriptor = new DockerDescriptor();
        descriptor.ShouldNotBeNull();
    }
}
