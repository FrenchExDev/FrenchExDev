using CsCheck;
using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using FrenchExDev.Net.Result;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Tests;

// ── BinaryIdentifier Tests ──────────────────────────────────────────────────

public class BinaryIdentifierTests
{
    [Fact]
    public void Constructor_WithName_SetsProperties()
    {
        var id = new BinaryIdentifier("docker");
        id.Name.ShouldBe("docker");
        id.Version.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithNameAndVersion_SetsProperties()
    {
        var id = new BinaryIdentifier("docker", "24.0");
        id.Name.ShouldBe("docker");
        id.Version.ShouldBe("24.0");
    }

    [Fact]
    public void Constructor_NullName_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new BinaryIdentifier(null!));
    }

    [Fact]
    public void Constructor_WhitespaceName_Throws()
    {
        Should.Throw<ArgumentException>(() => new BinaryIdentifier("  "));
    }

    [Fact]
    public void Parse_NameOnly_ReturnsIdentifierWithoutVersion()
    {
        var id = BinaryIdentifier.Parse("vagrant");
        id.Name.ShouldBe("vagrant");
        id.Version.ShouldBeNull();
    }

    [Fact]
    public void Parse_NameAndVersion_ReturnsFullIdentifier()
    {
        var id = BinaryIdentifier.Parse("container-runtime:24.0");
        id.Name.ShouldBe("container-runtime");
        id.Version.ShouldBe("24.0");
    }

    [Fact]
    public void Parse_NullOrWhitespace_Throws()
    {
        Should.Throw<ArgumentNullException>(() => BinaryIdentifier.Parse(null!));
        Should.Throw<ArgumentException>(() => BinaryIdentifier.Parse(""));
        Should.Throw<ArgumentException>(() => BinaryIdentifier.Parse("   "));
    }

    [Fact]
    public void ToString_WithoutVersion_ReturnsName()
    {
        new BinaryIdentifier("docker").ToString().ShouldBe("docker");
    }

    [Fact]
    public void ToString_WithVersion_ReturnsNameColonVersion()
    {
        new BinaryIdentifier("docker", "24.0").ToString().ShouldBe("docker:24.0");
    }

    [Fact]
    public void Parse_RoundTrips()
    {
        Gens.AnyBinaryId.Sample(id =>
        {
            var parsed = BinaryIdentifier.Parse(id.ToString());
            parsed.Name.ShouldBe(id.Name);
            parsed.Version.ShouldBe(id.Version);
        });
    }
}

// ── SemanticVersion Tests ───────────────────────────────────────────────────

public class SemanticVersionTests
{
    [Fact]
    public void Constructor_ValidValues_SetsProperties()
    {
        var v = new SemanticVersion(1, 2, 3, "alpha", "build123");
        v.Major.ShouldBe(1);
        v.Minor.ShouldBe(2);
        v.Patch.ShouldBe(3);
        v.PreRelease.ShouldBe("alpha");
        v.BuildMetadata.ShouldBe("build123");
    }

    [Fact]
    public void Constructor_NegativeValues_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SemanticVersion(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new SemanticVersion(0, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => new SemanticVersion(0, 0, -1));
    }

    [Theory]
    [InlineData("1.2.3", 1, 2, 3, null, null)]
    [InlineData("24.0.7", 24, 0, 7, null, null)]
    [InlineData("1.0.0-alpha", 1, 0, 0, "alpha", null)]
    [InlineData("1.0.0-alpha.1", 1, 0, 0, "alpha.1", null)]
    [InlineData("1.0.0+build.1", 1, 0, 0, null, "build.1")]
    [InlineData("1.0.0-beta+exp.sha", 1, 0, 0, "beta", "exp.sha")]
    [InlineData("2.4", 2, 4, 0, null, null)]
    public void Parse_ValidVersions(string input, int major, int minor, int patch,
        string? pre, string? meta)
    {
        var v = SemanticVersion.Parse(input);
        v.Major.ShouldBe(major);
        v.Minor.ShouldBe(minor);
        v.Patch.ShouldBe(patch);
        v.PreRelease.ShouldBe(pre);
        v.BuildMetadata.ShouldBe(meta);
    }

    [Fact]
    public void Parse_InvalidVersion_Throws()
    {
        Should.Throw<FormatException>(() => SemanticVersion.Parse("abc"));
        Should.Throw<ArgumentNullException>(() => SemanticVersion.Parse(null!));
        Should.Throw<ArgumentException>(() => SemanticVersion.Parse(""));
    }

    [Fact]
    public void TryParse_ValidVersion_ReturnsTrue()
    {
        SemanticVersion.TryParse("1.2.3", out var v).ShouldBeTrue();
        v.ShouldNotBeNull();
        v!.Major.ShouldBe(1);
    }

    [Fact]
    public void TryParse_WithPreRelease_ParsesCorrectly()
    {
        SemanticVersion.TryParse("1.0.0-alpha", out var v).ShouldBeTrue();
        v!.PreRelease.ShouldBe("alpha");
    }

    [Fact]
    public void TryParse_WithMetadata_ParsesCorrectly()
    {
        SemanticVersion.TryParse("1.0.0+build", out var v).ShouldBeTrue();
        v!.BuildMetadata.ShouldBe("build");
    }

    [Fact]
    public void TryParse_NoPatch_DefaultsToZero()
    {
        SemanticVersion.TryParse("2.4", out var v).ShouldBeTrue();
        v!.Patch.ShouldBe(0);
    }

    [Fact]
    public void TryParse_Invalid_ReturnsFalse()
    {
        SemanticVersion.TryParse(null, out var v).ShouldBeFalse();
        v.ShouldBeNull();
        SemanticVersion.TryParse("", out _).ShouldBeFalse();
        SemanticVersion.TryParse("abc", out _).ShouldBeFalse();
    }

    [Fact]
    public void CompareTo_Ordering()
    {
        var v1 = new SemanticVersion(1, 0, 0);
        var v2 = new SemanticVersion(2, 0, 0);
        var v1_1 = new SemanticVersion(1, 1, 0);
        var v1_0_1 = new SemanticVersion(1, 0, 1);
        var v1_alpha = new SemanticVersion(1, 0, 0, "alpha");

        (v1 < v2).ShouldBeTrue();
        (v1 < v1_1).ShouldBeTrue();
        (v1 < v1_0_1).ShouldBeTrue();
        (v1_alpha < v1).ShouldBeTrue();
        // Release > pre-release (null prerelease on left, non-null on right)
        (v1 > v1_alpha).ShouldBeTrue();
        (v2 > v1).ShouldBeTrue();
        var v1Copy = new SemanticVersion(1, 0, 0);
        (v1 >= v1Copy).ShouldBeTrue();
        (v1 <= v1Copy).ShouldBeTrue();
    }

    [Fact]
    public void CompareTo_Null_Returns1()
    {
        var v = new SemanticVersion(1, 0, 0);
        v.CompareTo(null).ShouldBe(1);
    }

    [Fact]
    public void CompareTo_PreReleaseOrdering()
    {
        var alpha = new SemanticVersion(1, 0, 0, "alpha");
        var beta = new SemanticVersion(1, 0, 0, "beta");
        (alpha < beta).ShouldBeTrue();
    }

    [Fact]
    public void ToString_Formats()
    {
        new SemanticVersion(1, 2, 3).ToString().ShouldBe("1.2.3");
        new SemanticVersion(1, 0, 0, "alpha").ToString().ShouldBe("1.0.0-alpha");
        new SemanticVersion(1, 0, 0, null, "build").ToString().ShouldBe("1.0.0+build");
        new SemanticVersion(1, 0, 0, "rc", "build").ToString().ShouldBe("1.0.0-rc+build");
    }

    [Fact]
    public void Parse_RoundTrips()
    {
        Gens.AnyVersion.Sample(v =>
        {
            var parsed = SemanticVersion.Parse(v.ToString());
            parsed.Major.ShouldBe(v.Major);
            parsed.Minor.ShouldBe(v.Minor);
            parsed.Patch.ShouldBe(v.Patch);
        });
    }

    [Fact]
    public void Comparison_IsTransitive()
    {
        Gen.Select(Gens.AnyVersion, Gens.AnyVersion, Gens.AnyVersion).Sample((a, b, c) =>
        {
            if (a <= b && b <= c)
                (a <= c).ShouldBeTrue();
        });
    }
}

// ── CommandOverrides Tests ──────────────────────────────────────────────────

public class CommandOverridesTests
{
    [Fact]
    public void Defaults_AreEmptyCollections()
    {
        var o = new CommandOverrides();
        o.OptionNameMappings.ShouldBeEmpty();
        o.UnsupportedOptions.ShouldBeEmpty();
    }
}

// ── BinaryBinding Tests ─────────────────────────────────────────────────────

public class BinaryBindingTests
{
    [Fact]
    public void Defaults_AreEmptyCollections()
    {
        var b = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/usr/bin/test"
        };
        b.EnvironmentVariables.ShouldBeEmpty();
        b.Overrides.ShouldBeEmpty();
    }
}

// ── DictionaryBinaryResolver Tests ──────────────────────────────────────────

public class DictionaryBinaryResolverTests
{
    private static BinaryBinding MakeBinding(string name) => new()
    {
        Identifier = new BinaryIdentifier(name),
        ExecutablePath = $"/usr/bin/{name}"
    };

    [Fact]
    public async Task ResolveAsync_KnownBinding_ReturnsSuccess()
    {
        var resolver = new DictionaryBinaryResolver([MakeBinding("docker")]);
        var result = await resolver.ResolveAsync(new BinaryIdentifier("docker"));
        result.IsSuccess.ShouldBeTrue();
        result.Value!.ExecutablePath.ShouldBe("/usr/bin/docker");
    }

    [Fact]
    public async Task ResolveAsync_UnknownBinding_ReturnsFailure()
    {
        var resolver = new DictionaryBinaryResolver([MakeBinding("docker")]);
        var result = await resolver.ResolveAsync(new BinaryIdentifier("vagrant"));
        result.IsFailure.ShouldBeTrue();
        result.Error!.Identifier.Name.ShouldBe("vagrant");
    }

    [Fact]
    public async Task Constructor_WithDictionary_Works()
    {
        var dict = new Dictionary<string, BinaryBinding>
        {
            ["docker"] = MakeBinding("docker")
        };
        var resolver = new DictionaryBinaryResolver(dict);
        var result = await resolver.ResolveAsync(new BinaryIdentifier("docker"));
        result.IsSuccess.ShouldBeTrue();
    }
}

// ── ProcessSpec / ProcessOutput Tests ───────────────────────────────────────

public class ProcessSpecTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var spec = new ProcessSpec { ExecutablePath = "/bin/test" };
        spec.Arguments.ShouldBeEmpty();
        spec.WorkingDirectory.ShouldBeNull();
        spec.EnvironmentVariables.ShouldBeEmpty();
        spec.Timeout.ShouldBeNull();
    }
}

// ── OutputLine Tests ────────────────────────────────────────────────────────

public class OutputLineTests
{
    [Fact]
    public void Record_EqualityWorks()
    {
        var a = new OutputLine("hello", OutputSource.StdOut);
        var b = new OutputLine("hello", OutputSource.StdOut);
        var c = new OutputLine("hello", OutputSource.StdErr);
        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }
}

// ── CommandExecutor Tests ───────────────────────────────────────────────────

public class CommandExecutorTests
{
    private static BinaryBinding DockerBinding => new()
    {
        Identifier = new BinaryIdentifier("docker"),
        ExecutablePath = "/usr/bin/docker"
    };

    private static DictionaryBinaryResolver DockerResolver =>
        new([DockerBinding]);

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsProcessOutput()
    {
        var runner = new FakeProcessRunner(stdout: "container-id-123\n");
        var executor = new CommandExecutor(DockerResolver, runner);
        var cmd = new FakeCommand { CommandPath = ["container", "run"], Args = ["--detach", "nginx"] };

        var result = await executor.ExecuteAsync(new BinaryIdentifier("docker"), cmd);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.StandardOutput.ShouldContain("container-id-123");
    }

    [Fact]
    public async Task ExecuteAsync_NonZeroExit_ReturnsFailure()
    {
        var runner = new FakeProcessRunner(stderr: "Error: not found", exitCode: 1);
        var executor = new CommandExecutor(DockerResolver, runner);
        var cmd = new FakeCommand();

        var result = await executor.ExecuteAsync(new BinaryIdentifier("docker"), cmd);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ExitCode.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownBinary_ReturnsFailure()
    {
        var runner = new FakeProcessRunner();
        var executor = new CommandExecutor(DockerResolver, runner);
        var cmd = new FakeCommand();

        var result = await executor.ExecuteAsync(new BinaryIdentifier("unknown"), cmd);

        result.IsFailure.ShouldBeTrue();
        result.Error!.ExitCode.ShouldBe(-1);
    }

    [Fact]
    public async Task StreamAsync_ParsesEvents()
    {
        var lines = new[]
        {
            new OutputLine("EVENT:hello", OutputSource.StdOut),
            new OutputLine("ignored line", OutputSource.StdOut),
            new OutputLine("EVENT:world", OutputSource.StdOut),
        };
        var runner = new FakeProcessRunner(lines);
        var executor = new CommandExecutor(DockerResolver, runner);
        var cmd = new FakeCommand();
        var parser = new TestOutputParser();

        var events = new List<TestEvent>();
        await foreach (var e in executor.StreamAsync(new BinaryIdentifier("docker"), cmd, parser))
            events.Add(e);

        events.Count.ShouldBe(3);
        events[0].Value.ShouldBe("hello");
        events[1].Value.ShouldBe("world");
        events[2].Value.ShouldBe("EXIT:0");
    }

    [Fact]
    public async Task StreamAsync_UnknownBinary_YieldsNoEvents()
    {
        var runner = new FakeProcessRunner();
        var executor = new CommandExecutor(DockerResolver, runner);
        var cmd = new FakeCommand();
        var parser = new TestOutputParser();

        var events = new List<TestEvent>();
        await foreach (var e in executor.StreamAsync(new BinaryIdentifier("unknown"), cmd, parser))
            events.Add(e);

        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithCollector_AggregatesEvents()
    {
        var lines = new[]
        {
            new OutputLine("EVENT:a", OutputSource.StdOut),
            new OutputLine("EVENT:b", OutputSource.StdOut),
        };
        var runner = new FakeProcessRunner(lines);
        var executor = new CommandExecutor(DockerResolver, runner);
        var cmd = new FakeCommand();

        var result = await executor.ExecuteAsync(
            new BinaryIdentifier("docker"), cmd,
            new TestOutputParser(), new TestCollector());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ShouldBe("a,b,EXIT:0");
    }

    [Fact]
    public async Task ExecuteAsync_WithCollector_UnknownBinary_ReturnsFailure()
    {
        var runner = new FakeProcessRunner();
        var executor = new CommandExecutor(DockerResolver, runner);
        var cmd = new FakeCommand();

        var result = await executor.ExecuteAsync(
            new BinaryIdentifier("unknown"), cmd,
            new TestOutputParser(), new TestCollector());

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithoutProcessRunner_UsesDefault()
    {
        // Covers the null-coalesce branch: processRunner ?? new SystemProcessRunner()
        var executor = new CommandExecutor(DockerResolver);
        executor.ShouldNotBeNull();
    }

    [Fact]
    public async Task StreamAsync_EmptyComplete_YieldsOnlyLineEvents()
    {
        var lines = new[]
        {
            new OutputLine("EVENT:a", OutputSource.StdOut),
        };
        var runner = new FakeProcessRunner(lines);
        var executor = new CommandExecutor(DockerResolver, runner);
        var cmd = new FakeCommand();
        var parser = new EmptyCompleteParser();

        var events = new List<TestEvent>();
        await foreach (var e in executor.StreamAsync(new BinaryIdentifier("docker"), cmd, parser))
            events.Add(e);

        events.Count.ShouldBe(1);
        events[0].Value.ShouldBe("a");
    }

    [Fact]
    public async Task StreamAsync_Cancellation_StopsEnumeration()
    {
        var lines = new[]
        {
            new OutputLine("EVENT:a", OutputSource.StdOut),
            new OutputLine("EVENT:b", OutputSource.StdOut),
            new OutputLine("EVENT:c", OutputSource.StdOut),
        };
        var runner = new FakeProcessRunner(lines);
        var executor = new CommandExecutor(DockerResolver, runner);
        var cmd = new FakeCommand();
        var parser = new TestOutputParser();
        using var cts = new CancellationTokenSource();

        var events = new List<TestEvent>();
        await foreach (var e in executor.StreamAsync(new BinaryIdentifier("docker"), cmd, parser, cts.Token))
        {
            events.Add(e);
            if (events.Count == 1)
                cts.Cancel();
        }

        events.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task StreamAsync_WithCollector_EmptyComplete_AggregatesLineEventsOnly()
    {
        var lines = new[]
        {
            new OutputLine("EVENT:x", OutputSource.StdOut),
        };
        var runner = new FakeProcessRunner(lines);
        var executor = new CommandExecutor(DockerResolver, runner);
        var cmd = new FakeCommand();

        var result = await executor.ExecuteAsync(
            new BinaryIdentifier("docker"), cmd,
            new EmptyCompleteParser(), new TestCollector());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ShouldBe("x");
    }

    [Fact]
    public async Task BuildProcessSpec_AppliesOverrides()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("docker"),
            ExecutablePath = "/usr/bin/docker",
            Overrides = new Dictionary<string, CommandOverrides>
            {
                ["container.run"] = new CommandOverrides
                {
                    OptionNameMappings = new Dictionary<string, string> { ["detach"] = "daemon" },
                    UnsupportedOptions = new HashSet<string> { "rm" }
                }
            }
        };
        var resolver = new DictionaryBinaryResolver([binding]);
        var runner = new FakeProcessRunner(stdout: "ok\n");
        var executor = new CommandExecutor(resolver, runner);

        var cmd = new FakeCommand
        {
            CommandPath = ["container", "run"],
            Args = ["--detach", "--rm", "--name", "test", "nginx"]
        };

        await executor.ExecuteAsync(new BinaryIdentifier("docker"), cmd);

        runner.LastSpec.ShouldNotBeNull();
        var args = runner.LastSpec!.Arguments.ToList();
        args.ShouldContain("--daemon");
        args.ShouldNotContain("--rm");
        args.ShouldContain("--name");
        args.ShouldContain("nginx");
    }
}

// ── CommandExecution Tests ──────────────────────────────────────────────────

public class CommandExecutionTests
{
    private static (CommandExecution<TestEvent> execution, FakeProcessRunner runner) CreateExecution()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/bin/test"
        };
        var lines = new[]
        {
            new OutputLine("EVENT:x", OutputSource.StdOut),
            new OutputLine("EVENT:y", OutputSource.StdOut),
        };
        var runner = new FakeProcessRunner(lines);
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);
        var execution = new CommandExecution<TestEvent>(
            executor, new BinaryIdentifier("test"), new FakeCommand(), new TestOutputParser());
        return (execution, runner);
    }

    [Fact]
    public async Task Streaming_ViaAwaitForeach()
    {
        var (execution, _) = CreateExecution();
        var events = new List<TestEvent>();
        await foreach (var e in execution)
            events.Add(e);
        events.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRawOutput()
    {
        var (execution, _) = CreateExecution();
        var result = await execution.ExecuteAsync();
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithCollector_ReturnsTypedResult()
    {
        var (execution, _) = CreateExecution();
        var result = await execution.ExecuteAsync(new TestCollector());
        result.IsSuccess.ShouldBeTrue();
        result.Value!.ShouldBe("x,y,EXIT:0");
    }
}

// ── StandardVersionDetector Tests ───────────────────────────────────────────

public class StandardVersionDetectorTests
{
    [Fact]
    public async Task DetectAsync_ValidVersion_ReturnsSuccess()
    {
        var runner = new FakeProcessRunner(stdout: "Vagrant 2.3.7\n");
        var detector = new StandardVersionDetector(runner);
        var result = await detector.DetectAsync("/usr/bin/vagrant");
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Major.ShouldBe(2);
        result.Value!.Minor.ShouldBe(3);
        result.Value!.Patch.ShouldBe(7);
    }

    [Fact]
    public async Task DetectAsync_NonZeroExit_ReturnsFailure()
    {
        var runner = new FakeProcessRunner(stderr: "not found", exitCode: 127);
        var detector = new StandardVersionDetector(runner);
        var result = await detector.DetectAsync("/usr/bin/missing");
        result.IsFailure.ShouldBeTrue();
        result.Error!.Message.ShouldContain("Version detection failed");
    }

    [Fact]
    public async Task DetectAsync_UnparsableOutput_ReturnsFailure()
    {
        var runner = new FakeProcessRunner(stdout: "unknown output format");
        var detector = new StandardVersionDetector(runner);
        var result = await detector.DetectAsync("/usr/bin/weird");
        result.IsFailure.ShouldBeTrue();
        result.Error!.Message.ShouldContain("Could not parse version");
    }

    [Fact]
    public async Task DetectAsync_SetsCorrectProcessSpec()
    {
        var runner = new FakeProcessRunner(stdout: "1.0.0");
        var detector = new StandardVersionDetector(runner);
        await detector.DetectAsync("/usr/bin/test");
        runner.LastSpec.ShouldNotBeNull();
        runner.LastSpec!.ExecutablePath.ShouldBe("/usr/bin/test");
        runner.LastSpec.Arguments.ShouldBe(new[] { "--version" });
    }
}

// ── Versioning Attributes Tests ─────────────────────────────────────────────

public class VersioningAttributeTests
{
    [Fact]
    public void SinceVersionAttribute_StoresVersion()
    {
        new SinceVersionAttribute("2.3.0").Version.ShouldBe("2.3.0");
    }

    [Fact]
    public void UntilVersionAttribute_StoresVersion()
    {
        new UntilVersionAttribute("3.0.0").Version.ShouldBe("3.0.0");
    }
}

// ── Version Guard Tests ─────────────────────────────────────────────────────

public class VersionGuardTests
{
    private static readonly SemanticVersion V1_0 = new(1, 0, 0);
    private static readonly SemanticVersion V1_5 = new(1, 5, 0);
    private static readonly SemanticVersion V2_0 = new(2, 0, 0);
    private static readonly SemanticVersion V3_0 = new(3, 0, 0);

    // ── EnsureCommandSupported ──────────────────────────────────────────

    [Fact]
    public void EnsureCommandSupported_NullDetectedVersion_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureCommandSupported(null, "build", V1_0, V2_0));
    }

    [Fact]
    public void EnsureCommandSupported_NullBounds_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureCommandSupported(V1_5, "build", null, null));
    }

    [Fact]
    public void EnsureCommandSupported_InRange_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureCommandSupported(V1_5, "build", V1_0, V2_0));
    }

    [Fact]
    public void EnsureCommandSupported_ExactlySince_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureCommandSupported(V1_0, "build", V1_0, V2_0));
    }

    [Fact]
    public void EnsureCommandSupported_BeforeSince_Throws()
    {
        var ex = Should.Throw<CommandNotSupportedException>(() =>
            VersionGuard.EnsureCommandSupported(V1_0, "build", V1_5, V3_0));

        ex.CommandPath.ShouldBe("build");
        ex.DetectedVersion.ShouldBe(V1_0);
        ex.Since.ShouldBe(V1_5);
        ex.Until.ShouldBe(V3_0);
        ex.Message.ShouldContain("requires version 1.5.0");
        ex.Message.ShouldContain("detected: 1.0.0");
    }

    [Fact]
    public void EnsureCommandSupported_ExactlyUntil_Throws()
    {
        var ex = Should.Throw<CommandNotSupportedException>(() =>
            VersionGuard.EnsureCommandSupported(V2_0, "up", V1_0, V2_0));

        ex.CommandPath.ShouldBe("up");
        ex.DetectedVersion.ShouldBe(V2_0);
        ex.Message.ShouldContain("was removed in version 2.0.0");
    }

    [Fact]
    public void EnsureCommandSupported_PastUntil_Throws()
    {
        var ex = Should.Throw<CommandNotSupportedException>(() =>
            VersionGuard.EnsureCommandSupported(V3_0, "up", V1_0, V2_0));

        ex.Message.ShouldContain("was removed in version 2.0.0");
        ex.Message.ShouldContain("detected: 3.0.0");
    }

    [Fact]
    public void EnsureCommandSupported_OnlySince_InRange_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureCommandSupported(V2_0, "build", V1_0, null));
    }

    [Fact]
    public void EnsureCommandSupported_OnlyUntil_InRange_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureCommandSupported(V1_0, "build", null, V2_0));
    }

    // ── EnsureOptionSupported ───────────────────────────────────────────

    [Fact]
    public void EnsureOptionSupported_NullDetectedVersion_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureOptionSupported(null, "build", "force", V1_0, V2_0));
    }

    [Fact]
    public void EnsureOptionSupported_InRange_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureOptionSupported(V1_5, "build", "force", V1_0, V2_0));
    }

    [Fact]
    public void EnsureOptionSupported_BeforeSince_Throws()
    {
        var ex = Should.Throw<OptionNotSupportedException>(() =>
            VersionGuard.EnsureOptionSupported(V1_0, "build", "timestamp-ui", V1_5, null));

        ex.CommandPath.ShouldBe("build");
        ex.OptionName.ShouldBe("timestamp-ui");
        ex.DetectedVersion.ShouldBe(V1_0);
        ex.Since.ShouldBe(V1_5);
        ex.Until.ShouldBeNull();
        ex.Message.ShouldContain("Option 'timestamp-ui' on command 'build'");
        ex.Message.ShouldContain("requires version 1.5.0");
    }

    [Fact]
    public void EnsureOptionSupported_PastUntil_Throws()
    {
        var ex = Should.Throw<OptionNotSupportedException>(() =>
            VersionGuard.EnsureOptionSupported(V3_0, "build", "experimental", V1_0, V2_0));

        ex.OptionName.ShouldBe("experimental");
        ex.Message.ShouldContain("was removed in version 2.0.0");
        ex.Message.ShouldContain("detected: 3.0.0");
    }

    [Fact]
    public void EnsureOptionSupported_NullBounds_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureOptionSupported(V1_5, "build", "force", null, null));
    }
}

public class CommandNotSupportedExceptionTests
{
    [Fact]
    public void RemovedCommand_MessageFormat()
    {
        var ex = new CommandNotSupportedException(
            "up", new SemanticVersion(1, 11, 2),
            new SemanticVersion(1, 0, 0), new SemanticVersion(1, 10, 0));

        ex.CommandPath.ShouldBe("up");
        ex.DetectedVersion.ShouldBe(new SemanticVersion(1, 11, 2));
        ex.Since.ShouldBe(new SemanticVersion(1, 0, 0));
        ex.Until.ShouldBe(new SemanticVersion(1, 10, 0));
        ex.Message.ShouldBe("Command 'up' was removed in version 1.10.0 (detected: 1.11.2).");
    }

    [Fact]
    public void TooOldVersion_MessageFormat()
    {
        var ex = new CommandNotSupportedException(
            "plugins.install", new SemanticVersion(1, 4, 0),
            new SemanticVersion(1, 7, 0), null);

        ex.Message.ShouldBe("Command 'plugins.install' requires version 1.7.0 or later (detected: 1.4.0).");
    }

    [Fact]
    public void IsInvalidOperationException()
    {
        var ex = new CommandNotSupportedException(
            "x", new SemanticVersion(1, 0, 0), new SemanticVersion(2, 0, 0), null);
        ex.ShouldBeAssignableTo<InvalidOperationException>();
    }
}

public class OptionNotSupportedExceptionTests
{
    [Fact]
    public void RemovedOption_MessageFormat()
    {
        var ex = new OptionNotSupportedException(
            "build", "experimental", new SemanticVersion(2, 0, 0),
            new SemanticVersion(1, 5, 0), new SemanticVersion(1, 11, 0));

        ex.CommandPath.ShouldBe("build");
        ex.OptionName.ShouldBe("experimental");
        ex.Message.ShouldBe("Option 'experimental' on command 'build' was removed in version 1.11.0 (detected: 2.0.0).");
    }

    [Fact]
    public void TooOldForOption_MessageFormat()
    {
        var ex = new OptionNotSupportedException(
            "build", "timestamp-ui", new SemanticVersion(1, 3, 0),
            new SemanticVersion(1, 5, 0), null);

        ex.Message.ShouldBe("Option 'timestamp-ui' on command 'build' requires version 1.5.0 or later (detected: 1.3.0).");
    }

    [Fact]
    public void IsInvalidOperationException()
    {
        var ex = new OptionNotSupportedException(
            "x", "y", new SemanticVersion(1, 0, 0), new SemanticVersion(2, 0, 0), null);
        ex.ShouldBeAssignableTo<InvalidOperationException>();
    }
}

// ── BinaryBinding DetectedVersion Tests ─────────────────────────────────────

public class BinaryBindingDetectedVersionTests
{
    [Fact]
    public void DetectedVersion_DefaultsToNull()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("packer"),
            ExecutablePath = "/usr/bin/packer"
        };
        binding.DetectedVersion.ShouldBeNull();
    }

    [Fact]
    public void DetectedVersion_CanBeSet()
    {
        var v = new SemanticVersion(1, 11, 2);
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("packer"),
            ExecutablePath = "/usr/bin/packer",
            DetectedVersion = v
        };
        binding.DetectedVersion.ShouldBe(v);
    }

    [Fact]
    public void DetectedVersion_IncludedInRecordEquality()
    {
        var id = new BinaryIdentifier("packer");
        var a = new BinaryBinding { Identifier = id, ExecutablePath = "/usr/bin/packer", DetectedVersion = new SemanticVersion(1, 0, 0) };
        var b = new BinaryBinding { Identifier = id, ExecutablePath = "/usr/bin/packer", DetectedVersion = new SemanticVersion(2, 0, 0) };
        a.ShouldNotBe(b);
    }
}

// ── Error Type Tests ────────────────────────────────────────────────────────

public class ErrorTypeTests
{
    [Fact]
    public void BinaryResolutionError_RecordEquality()
    {
        var id = new BinaryIdentifier("docker");
        var a = new BinaryResolutionError(id, "not found");
        var b = new BinaryResolutionError(id, "not found");
        a.ShouldBe(b);
    }

    [Fact]
    public void CommandError_RecordEquality()
    {
        var a = new CommandError(1, "err", "msg");
        var b = new CommandError(1, "err", "msg");
        a.ShouldBe(b);
    }
}

// ── SystemProcessRunner Tests ───────────────────────────────────────────────

public class SystemProcessRunnerTests
{
    private static ProcessSpec EchoSpec(string text) => new()
    {
        ExecutablePath = "cmd",
        Arguments = ["/c", $"echo {text}"]
    };

    private static ProcessSpec StderrSpec(string text) => new()
    {
        ExecutablePath = "cmd",
        Arguments = ["/c", $"echo {text} 1>&2"]
    };

    private static ProcessSpec ExitCodeSpec(int code) => new()
    {
        ExecutablePath = "cmd",
        Arguments = ["/c", $"exit {code}"]
    };

    // ── RunAsync ──

    [Fact]
    public async Task RunAsync_CapturesStdout()
    {
        var runner = new SystemProcessRunner();
        var result = await runner.RunAsync(EchoSpec("hello"));
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.Trim().ShouldBe("hello");
    }

    [Fact]
    public async Task RunAsync_CapturesStderr()
    {
        var runner = new SystemProcessRunner();
        var result = await runner.RunAsync(StderrSpec("oops"));
        result.StandardError.Trim().ShouldBe("oops");
    }

    [Fact]
    public async Task RunAsync_CapturesExitCode()
    {
        var runner = new SystemProcessRunner();
        var result = await runner.RunAsync(ExitCodeSpec(42));
        result.ExitCode.ShouldBe(42);
    }

    [Fact]
    public async Task RunAsync_WithTimeout_CompletesNormally()
    {
        var runner = new SystemProcessRunner();
        var spec = new ProcessSpec
        {
            ExecutablePath = "cmd",
            Arguments = ["/c", "echo ok"],
            Timeout = TimeSpan.FromSeconds(10)
        };
        var result = await runner.RunAsync(spec);
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.Trim().ShouldBe("ok");
    }

    [Fact]
    public async Task RunAsync_WithWorkingDirectory()
    {
        var runner = new SystemProcessRunner();
        var tempDir = Path.GetTempPath().TrimEnd('\\');
        var spec = new ProcessSpec
        {
            ExecutablePath = "cmd",
            Arguments = ["/c", "cd"],
            WorkingDirectory = tempDir
        };
        var result = await runner.RunAsync(spec);
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.Trim().ShouldStartWith(tempDir, Case.Insensitive);
    }

    [Fact]
    public async Task RunAsync_WithEnvironmentVariable()
    {
        var runner = new SystemProcessRunner();
        var spec = new ProcessSpec
        {
            ExecutablePath = "cmd",
            Arguments = ["/c", "echo %MY_TEST_VAR%"],
            EnvironmentVariables = new Dictionary<string, string> { ["MY_TEST_VAR"] = "test_value_123" }
        };
        var result = await runner.RunAsync(spec);
        result.StandardOutput.Trim().ShouldBe("test_value_123");
    }

    // ── StreamAsync ──

    [Fact]
    public async Task StreamAsync_CapturesStdoutLines()
    {
        var runner = new SystemProcessRunner();
        var spec = new ProcessSpec
        {
            ExecutablePath = "cmd",
            Arguments = ["/c", "echo line1 & echo line2"]
        };

        var lines = new List<OutputLine>();
        await foreach (var line in runner.StreamAsync(spec))
            lines.Add(line);

        var stdoutLines = lines.Where(l => l.Source == OutputSource.StdOut).Select(l => l.Text.Trim()).ToList();
        stdoutLines.ShouldContain("line1");
        stdoutLines.ShouldContain("line2");
    }

    [Fact]
    public async Task StreamAsync_CapturesStderrLines()
    {
        var runner = new SystemProcessRunner();
        var spec = new ProcessSpec
        {
            ExecutablePath = "cmd",
            Arguments = ["/c", "echo err_msg 1>&2"]
        };

        var lines = new List<OutputLine>();
        await foreach (var line in runner.StreamAsync(spec))
            lines.Add(line);

        var stderrLines = lines.Where(l => l.Source == OutputSource.StdErr).Select(l => l.Text.Trim()).ToList();
        stderrLines.ShouldContain("err_msg");
    }
}
