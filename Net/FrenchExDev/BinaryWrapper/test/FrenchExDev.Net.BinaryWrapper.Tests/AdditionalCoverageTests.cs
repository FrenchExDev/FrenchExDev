using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Tests;

// ── ProcessOutput Tests ─────────────────────────────────────────────────────

public class ProcessOutputTests
{
    [Fact]
    public void Properties_ReturnConstructedValues()
    {
        var output = new ProcessOutput
        {
            ExitCode = 42,
            StandardOutput = "hello",
            StandardError = "oops"
        };
        output.ExitCode.ShouldBe(42);
        output.StandardOutput.ShouldBe("hello");
        output.StandardError.ShouldBe("oops");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new ProcessOutput { ExitCode = 0, StandardOutput = "ok", StandardError = "" };
        var b = new ProcessOutput { ExitCode = 0, StandardOutput = "ok", StandardError = "" };
        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void RecordEquality_DifferentExitCode_AreNotEqual()
    {
        var a = new ProcessOutput { ExitCode = 0, StandardOutput = "ok", StandardError = "" };
        var b = new ProcessOutput { ExitCode = 1, StandardOutput = "ok", StandardError = "" };
        a.ShouldNotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentStdout_AreNotEqual()
    {
        var a = new ProcessOutput { ExitCode = 0, StandardOutput = "ok", StandardError = "" };
        var b = new ProcessOutput { ExitCode = 0, StandardOutput = "fail", StandardError = "" };
        a.ShouldNotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentStderr_AreNotEqual()
    {
        var a = new ProcessOutput { ExitCode = 0, StandardOutput = "ok", StandardError = "" };
        var b = new ProcessOutput { ExitCode = 0, StandardOutput = "ok", StandardError = "err" };
        a.ShouldNotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new ProcessOutput { ExitCode = 0, StandardOutput = "ok", StandardError = "" };
        var modified = original with { ExitCode = 1 };
        modified.ExitCode.ShouldBe(1);
        modified.StandardOutput.ShouldBe("ok");
        original.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void ToString_ContainsPropertyValues()
    {
        var output = new ProcessOutput { ExitCode = 0, StandardOutput = "hello", StandardError = "err" };
        var str = output.ToString();
        str.ShouldContain("ExitCode");
        str.ShouldContain("0");
    }
}

// ── BinaryBinding Extended Tests ────────────────────────────────────────────

public class BinaryBindingExtendedTests
{
    [Fact]
    public void Overrides_CanBeSetAndQueried()
    {
        var overrides = new Dictionary<string, CommandOverrides>
        {
            ["container.run"] = new CommandOverrides
            {
                OptionNameMappings = new Dictionary<string, string> { ["detach"] = "daemon" },
                UnsupportedOptions = new HashSet<string> { "rm" }
            },
            ["image.pull"] = new CommandOverrides()
        };
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("docker"),
            ExecutablePath = "/usr/bin/docker",
            Overrides = overrides
        };

        binding.Overrides.Count.ShouldBe(2);
        binding.Overrides["container.run"].OptionNameMappings["detach"].ShouldBe("daemon");
        binding.Overrides["container.run"].UnsupportedOptions.ShouldContain("rm");
        binding.Overrides["image.pull"].OptionNameMappings.ShouldBeEmpty();
    }

    [Fact]
    public void EnvironmentVariables_CanBeSetAndQueried()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("docker"),
            ExecutablePath = "/usr/bin/docker",
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["DOCKER_HOST"] = "tcp://localhost:2375",
                ["DOCKER_TLS_VERIFY"] = "1"
            }
        };

        binding.EnvironmentVariables.Count.ShouldBe(2);
        binding.EnvironmentVariables["DOCKER_HOST"].ShouldBe("tcp://localhost:2375");
        binding.EnvironmentVariables["DOCKER_TLS_VERIFY"].ShouldBe("1");
    }

    [Fact]
    public void RecordEquality_SameReferences_AreEqual()
    {
        var id = new BinaryIdentifier("test");
        var envVars = new Dictionary<string, string> { ["KEY"] = "VAL" };
        var overrides = new Dictionary<string, CommandOverrides>();
        var a = new BinaryBinding
        {
            Identifier = id,
            ExecutablePath = "/bin/test",
            EnvironmentVariables = envVars,
            Overrides = overrides
        };
        var b = new BinaryBinding
        {
            Identifier = id,
            ExecutablePath = "/bin/test",
            EnvironmentVariables = envVars,
            Overrides = overrides
        };
        a.ShouldBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentDictInstances_NotEqual()
    {
        // Record equality uses reference equality for dictionary fields
        var id = new BinaryIdentifier("test");
        var a = new BinaryBinding
        {
            Identifier = id,
            ExecutablePath = "/bin/test",
            EnvironmentVariables = new Dictionary<string, string> { ["KEY"] = "VAL" }
        };
        var b = new BinaryBinding
        {
            Identifier = id,
            ExecutablePath = "/bin/test",
            EnvironmentVariables = new Dictionary<string, string> { ["KEY"] = "VAL" }
        };
        a.ShouldNotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentExecutablePath_NotEqual()
    {
        var id = new BinaryIdentifier("test");
        var a = new BinaryBinding { Identifier = id, ExecutablePath = "/bin/a" };
        var b = new BinaryBinding { Identifier = id, ExecutablePath = "/bin/b" };
        a.ShouldNotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/bin/test"
        };
        var modified = binding with { ExecutablePath = "/usr/local/bin/test" };
        modified.ExecutablePath.ShouldBe("/usr/local/bin/test");
        binding.ExecutablePath.ShouldBe("/bin/test");
    }
}

// ── CommandOverrides Extended Tests ─────────────────────────────────────────

public class CommandOverridesExtendedTests
{
    [Fact]
    public void OptionNameMappings_CanBeLookedUp()
    {
        var overrides = new CommandOverrides
        {
            OptionNameMappings = new Dictionary<string, string>
            {
                ["detach"] = "daemon",
                ["name"] = "alias"
            }
        };
        overrides.OptionNameMappings.Count.ShouldBe(2);
        overrides.OptionNameMappings["detach"].ShouldBe("daemon");
        overrides.OptionNameMappings["name"].ShouldBe("alias");
    }

    [Fact]
    public void UnsupportedOptions_ContainsCheck()
    {
        var overrides = new CommandOverrides
        {
            UnsupportedOptions = new HashSet<string> { "rm", "force", "verbose" }
        };
        overrides.UnsupportedOptions.Count.ShouldBe(3);
        overrides.UnsupportedOptions.Contains("rm").ShouldBeTrue();
        overrides.UnsupportedOptions.Contains("force").ShouldBeTrue();
        overrides.UnsupportedOptions.Contains("nonexistent").ShouldBeFalse();
    }

    [Fact]
    public void RecordEquality_SameCollections_AreEqual()
    {
        var mappings = new Dictionary<string, string> { ["a"] = "b" };
        var unsupported = new HashSet<string> { "c" };
        var a = new CommandOverrides { OptionNameMappings = mappings, UnsupportedOptions = unsupported };
        var b = new CommandOverrides { OptionNameMappings = mappings, UnsupportedOptions = unsupported };
        a.ShouldBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new CommandOverrides
        {
            OptionNameMappings = new Dictionary<string, string> { ["a"] = "b" }
        };
        var modified = original with
        {
            UnsupportedOptions = new HashSet<string> { "x" }
        };
        modified.OptionNameMappings["a"].ShouldBe("b");
        modified.UnsupportedOptions.ShouldContain("x");
        original.UnsupportedOptions.ShouldBeEmpty();
    }
}

// ── ProcessSpec Extended Tests ──────────────────────────────────────────────

public class ProcessSpecExtendedTests
{
    [Fact]
    public void Timeout_CanBeSet()
    {
        var spec = new ProcessSpec
        {
            ExecutablePath = "/bin/test",
            Timeout = TimeSpan.FromSeconds(30)
        };
        spec.Timeout.ShouldNotBeNull();
        spec.Timeout!.Value.TotalSeconds.ShouldBe(30);
    }

    [Fact]
    public void WorkingDirectory_CanBeSet()
    {
        var spec = new ProcessSpec
        {
            ExecutablePath = "/bin/test",
            WorkingDirectory = "/tmp/work"
        };
        spec.WorkingDirectory.ShouldBe("/tmp/work");
    }

    [Fact]
    public void EnvironmentVariables_CanBeSet()
    {
        var spec = new ProcessSpec
        {
            ExecutablePath = "/bin/test",
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["PATH"] = "/usr/bin",
                ["HOME"] = "/root"
            }
        };
        spec.EnvironmentVariables.Count.ShouldBe(2);
        spec.EnvironmentVariables["PATH"].ShouldBe("/usr/bin");
    }

    [Fact]
    public void Arguments_CanBeSet()
    {
        var spec = new ProcessSpec
        {
            ExecutablePath = "/bin/test",
            Arguments = ["--verbose", "--output", "result.txt"]
        };
        spec.Arguments.Count.ShouldBe(3);
        spec.Arguments[0].ShouldBe("--verbose");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var args = new List<string> { "--flag" };
        var env = new Dictionary<string, string> { ["K"] = "V" };
        var a = new ProcessSpec
        {
            ExecutablePath = "/bin/test",
            Arguments = args,
            WorkingDirectory = "/tmp",
            EnvironmentVariables = env,
            Timeout = TimeSpan.FromSeconds(5)
        };
        var b = new ProcessSpec
        {
            ExecutablePath = "/bin/test",
            Arguments = args,
            WorkingDirectory = "/tmp",
            EnvironmentVariables = env,
            Timeout = TimeSpan.FromSeconds(5)
        };
        a.ShouldBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var spec = new ProcessSpec
        {
            ExecutablePath = "/bin/test",
            Timeout = TimeSpan.FromSeconds(5)
        };
        var modified = spec with { Timeout = TimeSpan.FromSeconds(60) };
        modified.Timeout!.Value.TotalSeconds.ShouldBe(60);
        spec.Timeout!.Value.TotalSeconds.ShouldBe(5);
    }
}

// ── CommandExecution Extended Tests ──────────────────────────────────────────

public class CommandExecutionExtendedTests
{
    private static BinaryBinding TestBinding => new()
    {
        Identifier = new BinaryIdentifier("test"),
        ExecutablePath = "/bin/test"
    };

    [Fact]
    public async Task Streaming_EmptyStream_YieldsOnlyCompleteEvents()
    {
        var runner = new FakeProcessRunner(Array.Empty<OutputLine>());
        var executor = new CommandExecutor(new DictionaryBinaryResolver([TestBinding]), runner);
        var execution = new CommandExecution<TestEvent>(
            executor, new BinaryIdentifier("test"), new FakeCommand(), new TestOutputParser());

        var events = new List<TestEvent>();
        await foreach (var e in execution)
            events.Add(e);

        events.Count.ShouldBe(1);
        events[0].Value.ShouldBe("EXIT:0");
    }

    [Fact]
    public async Task Streaming_EmptyStream_EmptyComplete_YieldsNoEvents()
    {
        var runner = new FakeProcessRunner(Array.Empty<OutputLine>());
        var executor = new CommandExecutor(new DictionaryBinaryResolver([TestBinding]), runner);
        var execution = new CommandExecution<TestEvent>(
            executor, new BinaryIdentifier("test"), new FakeCommand(), new EmptyCompleteParser());

        var events = new List<TestEvent>();
        await foreach (var e in execution)
            events.Add(e);

        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithCollector_EmptyStream_ReturnsCompleteOnly()
    {
        var runner = new FakeProcessRunner(Array.Empty<OutputLine>());
        var executor = new CommandExecutor(new DictionaryBinaryResolver([TestBinding]), runner);
        var execution = new CommandExecution<TestEvent>(
            executor, new BinaryIdentifier("test"), new FakeCommand(), new TestOutputParser());

        var result = await execution.ExecuteAsync(new TestCollector());
        result.IsSuccess.ShouldBeTrue();
        result.Value!.ShouldBe("EXIT:0");
    }

    [Fact]
    public async Task ExecuteAsync_NonZeroExit_ReturnsFailure()
    {
        var runner = new FakeProcessRunner(stderr: "bad", exitCode: 1);
        var executor = new CommandExecutor(new DictionaryBinaryResolver([TestBinding]), runner);
        var execution = new CommandExecution<TestEvent>(
            executor, new BinaryIdentifier("test"), new FakeCommand(), new TestOutputParser());

        var result = await execution.ExecuteAsync();
        result.IsFailure.ShouldBeTrue();
        result.Error!.ExitCode.ShouldBe(1);
    }

    [Fact]
    public async Task Streaming_StdErrLines_PassedToParser()
    {
        var lines = new[]
        {
            new OutputLine("EVENT:from-stderr", OutputSource.StdErr),
            new OutputLine("EVENT:from-stdout", OutputSource.StdOut),
        };
        var runner = new FakeProcessRunner(lines);
        var executor = new CommandExecutor(new DictionaryBinaryResolver([TestBinding]), runner);
        var execution = new CommandExecution<TestEvent>(
            executor, new BinaryIdentifier("test"), new FakeCommand(), new TestOutputParser());

        var events = new List<TestEvent>();
        await foreach (var e in execution)
            events.Add(e);

        events.Count.ShouldBe(3);
        events[0].Value.ShouldBe("from-stderr");
        events[1].Value.ShouldBe("from-stdout");
        events[2].Value.ShouldBe("EXIT:0");
    }
}

// ── DictionaryBinaryResolver Extended Tests ─────────────────────────────────

public class DictionaryBinaryResolverExtendedTests
{
    private static BinaryBinding MakeBinding(string name) => new()
    {
        Identifier = new BinaryIdentifier(name),
        ExecutablePath = $"/usr/bin/{name}"
    };

    [Fact]
    public async Task ResolveAsync_MultipleBindings_ResolvesCorrectOne()
    {
        var resolver = new DictionaryBinaryResolver([
            MakeBinding("docker"),
            MakeBinding("vagrant"),
            MakeBinding("packer")
        ]);

        var docker = await resolver.ResolveAsync(new BinaryIdentifier("docker"));
        docker.IsSuccess.ShouldBeTrue();
        docker.Value!.ExecutablePath.ShouldBe("/usr/bin/docker");

        var vagrant = await resolver.ResolveAsync(new BinaryIdentifier("vagrant"));
        vagrant.IsSuccess.ShouldBeTrue();
        vagrant.Value!.ExecutablePath.ShouldBe("/usr/bin/vagrant");

        var packer = await resolver.ResolveAsync(new BinaryIdentifier("packer"));
        packer.IsSuccess.ShouldBeTrue();
        packer.Value!.ExecutablePath.ShouldBe("/usr/bin/packer");
    }

    [Fact]
    public async Task ResolveAsync_VersionInIdentifier_StillMatchesByName()
    {
        var resolver = new DictionaryBinaryResolver([MakeBinding("docker")]);
        var result = await resolver.ResolveAsync(new BinaryIdentifier("docker", "24.0"));
        result.IsSuccess.ShouldBeTrue();
        result.Value!.ExecutablePath.ShouldBe("/usr/bin/docker");
    }

    [Fact]
    public async Task ResolveAsync_EmptyResolver_AlwaysFails()
    {
        var resolver = new DictionaryBinaryResolver(Array.Empty<BinaryBinding>());
        var result = await resolver.ResolveAsync(new BinaryIdentifier("anything"));
        result.IsFailure.ShouldBeTrue();
        result.Error!.Message.ShouldContain("No binding found");
    }

    [Fact]
    public async Task ResolveAsync_FailureError_ContainsIdentifier()
    {
        var resolver = new DictionaryBinaryResolver([MakeBinding("docker")]);
        var result = await resolver.ResolveAsync(new BinaryIdentifier("missing"));
        result.IsFailure.ShouldBeTrue();
        result.Error!.Identifier.Name.ShouldBe("missing");
    }

    [Fact]
    public async Task Constructor_WithBindingWithVersion_ResolvesCorrectly()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("docker", "24.0"),
            ExecutablePath = "/usr/bin/docker",
            DetectedVersion = new SemanticVersion(24, 0, 7)
        };
        var resolver = new DictionaryBinaryResolver([binding]);
        var result = await resolver.ResolveAsync(new BinaryIdentifier("docker"));
        result.IsSuccess.ShouldBeTrue();
        result.Value!.DetectedVersion!.Major.ShouldBe(24);
    }
}

// ── SinceVersionAttribute / UntilVersionAttribute Extended Tests ────────────

public class SinceVersionAttributeExtendedTests
{
    [Fact]
    public void Version_ReturnsConstructorValue()
    {
        var attr = new SinceVersionAttribute("1.5.0");
        attr.Version.ShouldBe("1.5.0");
    }

    [Fact]
    public void Version_PreRelease()
    {
        var attr = new SinceVersionAttribute("2.0.0-beta");
        attr.Version.ShouldBe("2.0.0-beta");
    }

    [Fact]
    public void IsAttribute()
    {
        new SinceVersionAttribute("1.0.0").ShouldBeAssignableTo<Attribute>();
    }
}

public class UntilVersionAttributeExtendedTests
{
    [Fact]
    public void Version_ReturnsConstructorValue()
    {
        var attr = new UntilVersionAttribute("3.0.0");
        attr.Version.ShouldBe("3.0.0");
    }

    [Fact]
    public void Version_PreRelease()
    {
        var attr = new UntilVersionAttribute("2.0.0-rc.1");
        attr.Version.ShouldBe("2.0.0-rc.1");
    }

    [Fact]
    public void IsAttribute()
    {
        new UntilVersionAttribute("1.0.0").ShouldBeAssignableTo<Attribute>();
    }
}

// ── OutputSource Enum Tests ─────────────────────────────────────────────────

public class OutputSourceTests
{
    [Fact]
    public void StdOut_HasExpectedValue()
    {
        ((int)OutputSource.StdOut).ShouldBe(0);
    }

    [Fact]
    public void StdErr_HasExpectedValue()
    {
        ((int)OutputSource.StdErr).ShouldBe(1);
    }

    [Fact]
    public void EnumValues_AreTwoDistinct()
    {
        var values = Enum.GetValues<OutputSource>();
        values.Length.ShouldBe(2);
        values.ShouldContain(OutputSource.StdOut);
        values.ShouldContain(OutputSource.StdErr);
    }

    [Fact]
    public void ToString_ReturnsExpectedNames()
    {
        OutputSource.StdOut.ToString().ShouldBe("StdOut");
        OutputSource.StdErr.ToString().ShouldBe("StdErr");
    }
}

// ── BuildProcessSpec Override Edge Cases ────────────────────────────────────

public class BuildProcessSpecOverrideTests
{
    [Fact]
    public async Task Overrides_NonDashDashArgs_ArePassedThrough()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/bin/test",
            Overrides = new Dictionary<string, CommandOverrides>
            {
                ["run"] = new CommandOverrides
                {
                    UnsupportedOptions = new HashSet<string> { "verbose" }
                }
            }
        };
        var runner = new FakeProcessRunner(stdout: "ok\n");
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);

        var cmd = new FakeCommand
        {
            CommandPath = ["run"],
            Args = ["--verbose", "positional-arg", "-f"]
        };

        await executor.ExecuteAsync(new BinaryIdentifier("test"), cmd);

        var args = runner.LastSpec!.Arguments.ToList();
        args.ShouldNotContain("--verbose");
        args.ShouldContain("positional-arg");
        args.ShouldContain("-f");
    }

    [Fact]
    public async Task Overrides_NoMatchingCommandPath_ArgsPassedUnmodified()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/bin/test",
            Overrides = new Dictionary<string, CommandOverrides>
            {
                ["other.command"] = new CommandOverrides
                {
                    UnsupportedOptions = new HashSet<string> { "detach" }
                }
            }
        };
        var runner = new FakeProcessRunner(stdout: "ok\n");
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);

        var cmd = new FakeCommand
        {
            CommandPath = ["run"],
            Args = ["--detach"]
        };

        await executor.ExecuteAsync(new BinaryIdentifier("test"), cmd);

        runner.LastSpec!.Arguments.ShouldContain("--detach");
    }

    [Fact]
    public async Task Overrides_MappingAndUnsupported_ApplySimultaneously()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/bin/test",
            Overrides = new Dictionary<string, CommandOverrides>
            {
                ["build"] = new CommandOverrides
                {
                    OptionNameMappings = new Dictionary<string, string>
                    {
                        ["output"] = "out-dir",
                        ["format"] = "fmt"
                    },
                    UnsupportedOptions = new HashSet<string> { "debug", "trace" }
                }
            }
        };
        var runner = new FakeProcessRunner(stdout: "ok\n");
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);

        var cmd = new FakeCommand
        {
            CommandPath = ["build"],
            Args = ["--output", "./dist", "--debug", "--format", "json", "--trace", "--name", "app"]
        };

        await executor.ExecuteAsync(new BinaryIdentifier("test"), cmd);

        var args = runner.LastSpec!.Arguments.ToList();
        args.ShouldContain("--out-dir");
        args.ShouldNotContain("--output");
        args.ShouldContain("--fmt");
        args.ShouldNotContain("--format");
        args.ShouldNotContain("--debug");
        args.ShouldNotContain("--trace");
        args.ShouldContain("--name");
        args.ShouldContain("app");
        args.ShouldContain("./dist");
        args.ShouldContain("json");
    }

    [Fact]
    public async Task BuildProcessSpec_EnvironmentVariables_PassedToSpec()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/bin/test",
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["MY_VAR"] = "my_value"
            }
        };
        var runner = new FakeProcessRunner(stdout: "ok\n");
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);

        await executor.ExecuteAsync(new BinaryIdentifier("test"), new FakeCommand());

        runner.LastSpec!.EnvironmentVariables["MY_VAR"].ShouldBe("my_value");
    }

    [Fact]
    public async Task BuildProcessSpec_CommandPath_PrependedToArguments()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/bin/test"
        };
        var runner = new FakeProcessRunner(stdout: "ok\n");
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);

        var cmd = new FakeCommand
        {
            CommandPath = ["config", "set"],
            Args = ["--key", "theme"]
        };

        await executor.ExecuteAsync(new BinaryIdentifier("test"), cmd);

        var args = runner.LastSpec!.Arguments.ToList();
        args[0].ShouldBe("config");
        args[1].ShouldBe("set");
        args[2].ShouldBe("--key");
        args[3].ShouldBe("theme");
    }
}

// ── Error Record Tests ──────────────────────────────────────────────────────

public class ErrorRecordExtendedTests
{
    [Fact]
    public void BinaryResolutionError_Properties()
    {
        var id = new BinaryIdentifier("vagrant", "2.4");
        var error = new BinaryResolutionError(id, "Not found in PATH");
        error.Identifier.ShouldBe(id);
        error.Identifier.Name.ShouldBe("vagrant");
        error.Identifier.Version.ShouldBe("2.4");
        error.Message.ShouldBe("Not found in PATH");
    }

    [Fact]
    public void BinaryResolutionError_DifferentMessages_NotEqual()
    {
        var id = new BinaryIdentifier("docker");
        var a = new BinaryResolutionError(id, "msg1");
        var b = new BinaryResolutionError(id, "msg2");
        a.ShouldNotBe(b);
    }

    [Fact]
    public void CommandError_Properties()
    {
        var error = new CommandError(127, "command not found", "Execution failed");
        error.ExitCode.ShouldBe(127);
        error.StandardError.ShouldBe("command not found");
        error.Message.ShouldBe("Execution failed");
    }

    [Fact]
    public void CommandError_DifferentExitCodes_NotEqual()
    {
        var a = new CommandError(0, "", "ok");
        var b = new CommandError(1, "", "ok");
        a.ShouldNotBe(b);
    }

    [Fact]
    public void CommandError_With_CreatesModifiedCopy()
    {
        var original = new CommandError(1, "err", "fail");
        var modified = original with { ExitCode = 0, Message = "recovered" };
        modified.ExitCode.ShouldBe(0);
        modified.StandardError.ShouldBe("err");
        modified.Message.ShouldBe("recovered");
    }
}

// ── TestBindings Helper Tests ───────────────────────────────────────────────

public class TestBindingsTests
{
    [Fact]
    public void Create_SetsExpectedPath()
    {
        var binding = TestBindings.Create("vagrant");
        binding.Identifier.Name.ShouldBe("vagrant");
        binding.ExecutablePath.ShouldBe("/usr/bin/vagrant");
        binding.DetectedVersion.ShouldBeNull();
    }

    [Fact]
    public void Create_WithVersion_SetsDetectedVersion()
    {
        var v = new SemanticVersion(2, 4, 1);
        var binding = TestBindings.Create("vagrant", v);
        binding.DetectedVersion.ShouldBe(v);
    }

    [Fact]
    public async Task ResolverFor_SingleName_Resolves()
    {
        var resolver = TestBindings.ResolverFor("packer");
        var result = await resolver.ResolveAsync(new BinaryIdentifier("packer"));
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolverFor_MultipleBindings_ResolvesAll()
    {
        var resolver = TestBindings.ResolverFor(
            TestBindings.Create("a"),
            TestBindings.Create("b"));

        (await resolver.ResolveAsync(new BinaryIdentifier("a"))).IsSuccess.ShouldBeTrue();
        (await resolver.ResolveAsync(new BinaryIdentifier("b"))).IsSuccess.ShouldBeTrue();
    }
}
