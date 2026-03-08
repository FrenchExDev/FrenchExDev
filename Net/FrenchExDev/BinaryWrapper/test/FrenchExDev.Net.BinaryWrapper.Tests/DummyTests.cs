using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Tests;

// ── Dummy Command Serialization ─────────────────────────────────────────────

public sealed class DummyCommandSerializationTests
{
    // ── run ──

    [Fact]
    public void Run_Detach_SerializesAsFlag()
    {
        var cmd = new DummyRunCommand { Detach = true };
        cmd.ToArguments().ShouldContain("--detach");
    }

    [Fact]
    public void Run_DetachNull_NotIncluded()
    {
        new DummyRunCommand().ToArguments().ShouldNotContain("--detach");
    }

    [Fact]
    public void Run_Name_SerializesWithValue()
    {
        var cmd = new DummyRunCommand { Name = "my-task" };
        var args = cmd.ToArguments();
        args.ShouldContain("--name");
        args.ShouldContain("my-task");
    }

    [Fact]
    public void Run_EnvList_SerializesEach()
    {
        var cmd = new DummyRunCommand { Env = ["FOO=1", "BAR=2"] };
        var args = cmd.ToArguments();
        args.Count(a => a == "--env").ShouldBe(2);
        args.ShouldContain("FOO=1");
        args.ShouldContain("BAR=2");
    }

    [Fact]
    public void Run_Retries_SerializesAsStringValue()
    {
        var cmd = new DummyRunCommand { Retries = "3" };
        var args = cmd.ToArguments();
        args.ShouldContain("--retries");
        args.ShouldContain("3");
    }

    [Fact]
    public void Run_Timeout_SerializesAsStringValue()
    {
        var cmd = new DummyRunCommand { Timeout = "30" };
        var args = cmd.ToArguments();
        args.ShouldContain("--timeout");
        args.ShouldContain("30");
    }

    [Fact]
    public void Run_NullOptions_EmptyArgs()
    {
        new DummyRunCommand().ToArguments().Count.ShouldBe(0);
    }

    [Fact]
    public void Run_CommandPath_IsRun()
    {
        new DummyRunCommand().CommandPath.ShouldBe(new[] { "run" });
    }

    [Fact]
    public void Run_ImplementsICliCommand()
    {
        new DummyRunCommand().ShouldBeAssignableTo<ICliCommand>();
    }

    [Fact]
    public void Run_AllOptions_CorrectArgCount()
    {
        var cmd = new DummyRunCommand
        {
            Detach = true,        // 1 arg
            Name = "test",        // 2 args
            Env = ["A=1", "B=2"], // 4 args (2 pairs)
            Retries = "5",        // 2 args
            Timeout = "60"        // 2 args
        };
        cmd.ToArguments().Count.ShouldBe(11);
    }

    // ── list ──

    [Fact]
    public void List_All_SerializesAsFlag()
    {
        new DummyListCommand { All = true }.ToArguments().ShouldContain("--all");
    }

    [Fact]
    public void List_Format_SerializesWithValue()
    {
        var cmd = new DummyListCommand { Format = "json" };
        var args = cmd.ToArguments();
        args.ShouldContain("--format");
        args.ShouldContain("json");
    }

    [Fact]
    public void List_Quiet_SerializesAsFlag()
    {
        new DummyListCommand { Quiet = true }.ToArguments().ShouldContain("--quiet");
    }

    [Fact]
    public void List_CommandPath()
    {
        new DummyListCommand().CommandPath.ShouldBe(new[] { "list" });
    }

    // ── config get / set ──

    [Fact]
    public void ConfigGet_Key_Serializes()
    {
        var cmd = new DummyConfigGetCommand { Key = "theme" };
        var args = cmd.ToArguments();
        args.ShouldContain("--key");
        args.ShouldContain("theme");
    }

    [Fact]
    public void ConfigGet_CommandPath()
    {
        new DummyConfigGetCommand().CommandPath.ShouldBe(new[] { "config", "get" });
    }

    [Fact]
    public void ConfigSet_KeyAndValue_Serializes()
    {
        var cmd = new DummyConfigSetCommand { Key = "theme", Value = "dark" };
        var args = cmd.ToArguments();
        args.ShouldContain("--key");
        args.ShouldContain("theme");
        args.ShouldContain("--value");
        args.ShouldContain("dark");
    }

    [Fact]
    public void ConfigSet_CommandPath()
    {
        new DummyConfigSetCommand().CommandPath.ShouldBe(new[] { "config", "set" });
    }
}

// ── Dummy Generated Client API Tests ────────────────────────────────────────

public sealed class DummyGeneratedClientTests
{
    private static DummyClient CreateClient() =>
        new(TestBindings.Create("dummy"));

    private static DummyClient CreateClient(SemanticVersion version) =>
        new(TestBindings.Create("dummy", version));

    // ── Static entry point ──

    [Fact]
    public void Dummy_Create_ReturnsClient()
    {
        Dummy.Create(TestBindings.Create("dummy")).ShouldNotBeNull();
    }

    // ── Leaf commands ──

    [Fact]
    public void Client_Run_ReturnsCommand()
    {
        var cmd = CreateClient().Run(b => b.WithDetach(true).WithName("test"));
        cmd.Detach.ShouldBe(true);
        cmd.Name.ShouldBe("test");
    }

    [Fact]
    public void Client_List_ReturnsCommand()
    {
        var cmd = CreateClient().List(b => b.WithAll(true).WithFormat("json"));
        cmd.All.ShouldBe(true);
        cmd.Format.ShouldBe("json");
    }

    // ── Sub-group navigation ──

    [Fact]
    public void Client_Config_Group_Exists()
    {
        CreateClient().Config.ShouldNotBeNull();
    }

    [Fact]
    public void Client_Config_Get_ReturnsCommand()
    {
        var cmd = CreateClient().Config.Get(b => b.WithKey("theme"));
        cmd.Key.ShouldBe("theme");
        cmd.CommandPath.ShouldBe(new[] { "config", "get" });
    }

    [Fact]
    public void Client_Config_Set_ReturnsCommand()
    {
        var cmd = CreateClient().Config.Set(b => b.WithKey("x").WithValue("y"));
        cmd.Key.ShouldBe("x");
        cmd.Value.ShouldBe("y");
    }

    // ── Builder fluent chains ──

    [Fact]
    public void RunBuilder_FluentChain()
    {
        var builder = new DummyRunCommandBuilder()
            .WithDetach(true)
            .WithName("t")
            .WithEnv(["A=1"])
            .WithRetries("3");
        builder.ShouldBeOfType<DummyRunCommandBuilder>();
    }

    [Fact]
    public void ListBuilder_FluentChain()
    {
        new DummyListCommandBuilder()
            .WithAll(true)
            .WithFormat("json")
            .ShouldBeOfType<DummyListCommandBuilder>();
    }

    // ── No-option commands ──

    [Fact]
    public void Client_Run_NoOptions()
    {
        var cmd = CreateClient().Run(b => { });
        cmd.ToArguments().Count.ShouldBe(0);
    }

    // ── Version-gated option ──

    [Fact]
    public void RunBuilder_WithTimeout_V2_Succeeds()
    {
        var client = CreateClient(new SemanticVersion(2, 0, 0));
        var cmd = client.Run(b => b.WithTimeout("60"));
        cmd.Timeout.ShouldBe("60");
    }

    [Fact]
    public void RunBuilder_WithTimeout_V1_Throws()
    {
        var client = CreateClient(new SemanticVersion(1, 0, 0));
        Should.Throw<OptionNotSupportedException>(() =>
            client.Run(b => b.WithTimeout("60")));
    }

    // ── Version-gated option (quiet added in v2) ──

    [Fact]
    public void ListBuilder_WithQuiet_V2_Succeeds()
    {
        var client = CreateClient(new SemanticVersion(2, 0, 0));
        var cmd = client.List(b => b.WithQuiet(true));
        cmd.Quiet.ShouldBe(true);
    }

    [Fact]
    public void ListBuilder_WithQuiet_V1_Throws()
    {
        var client = CreateClient(new SemanticVersion(1, 0, 0));
        Should.Throw<OptionNotSupportedException>(() =>
            client.List(b => b.WithQuiet(true)));
    }

    // ── Run with all common options via builder ──

    [Fact]
    public void Client_Run_AllOptions_ViaBuilder()
    {
        var client = CreateClient(new SemanticVersion(2, 0, 0));
        var cmd = client.Run(b => b
            .WithDetach(true)
            .WithName("my-task")
            .WithEnv(["A=1", "B=2"])
            .WithRetries("5")
            .WithTimeout("30"));

        cmd.Detach.ShouldBe(true);
        cmd.Name.ShouldBe("my-task");
        cmd.Env.ShouldBe(new[] { "A=1", "B=2" });
        cmd.Retries.ShouldBe("5");
        cmd.Timeout.ShouldBe("30");
        cmd.ToArguments().Count.ShouldBe(11);
    }
}

// ── Dummy Descriptor Tests ──────────────────────────────────────────────────

public sealed class DummyDescriptorTests
{
    [Fact]
    public void DummyDescriptor_CanBeInstantiated()
    {
        new DummyDescriptor().ShouldNotBeNull();
    }
}
