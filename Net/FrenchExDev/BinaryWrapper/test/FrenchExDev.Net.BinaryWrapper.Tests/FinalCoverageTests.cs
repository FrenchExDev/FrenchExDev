using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Tests;

// ── SemanticVersion Final Coverage ───────────────────────────────────────────

public sealed class SemanticVersionFinalCoverageTests
{
    // CompareTo: both have PreRelease, same PreRelease => returns 0
    [Fact]
    public void CompareTo_BothPreReleaseSame_ReturnsZero()
    {
        var a = new SemanticVersion(1, 0, 0, "alpha");
        var b = new SemanticVersion(1, 0, 0, "alpha");
        a.CompareTo(b).ShouldBe(0);
    }

    // CompareTo: both have PreRelease, different => compares lexically
    [Fact]
    public void CompareTo_BothPreReleaseDifferent_ComparesLexically()
    {
        var a = new SemanticVersion(1, 0, 0, "alpha");
        var b = new SemanticVersion(1, 0, 0, "beta");
        (a < b).ShouldBeTrue();
        (b > a).ShouldBeTrue();
    }

    // CompareTo: left has PreRelease, right does not => left < right
    [Fact]
    public void CompareTo_LeftPreRelease_RightRelease_LeftIsSmaller()
    {
        var pre = new SemanticVersion(1, 0, 0, "rc.1");
        var rel = new SemanticVersion(1, 0, 0);
        (pre < rel).ShouldBeTrue();
        pre.CompareTo(rel).ShouldBe(-1);
    }

    // CompareTo: left is release, right has PreRelease => left > right
    [Fact]
    public void CompareTo_LeftRelease_RightPreRelease_LeftIsGreater()
    {
        var rel = new SemanticVersion(1, 0, 0);
        var pre = new SemanticVersion(1, 0, 0, "beta");
        (rel > pre).ShouldBeTrue();
        rel.CompareTo(pre).ShouldBe(1);
    }

    // CompareTo: both null PreRelease, same version => 0
    [Fact]
    public void CompareTo_BothNullPreRelease_SameVersion_ReturnsZero()
    {
        var a = new SemanticVersion(3, 5, 7);
        var b = new SemanticVersion(3, 5, 7);
        a.CompareTo(b).ShouldBe(0);
        (a >= b).ShouldBeTrue();
        (a <= b).ShouldBeTrue();
    }

    // CompareTo: differs only in minor
    [Fact]
    public void CompareTo_DiffersInMinor()
    {
        var a = new SemanticVersion(1, 2, 0);
        var b = new SemanticVersion(1, 3, 0);
        (a < b).ShouldBeTrue();
    }

    // CompareTo: differs only in patch
    [Fact]
    public void CompareTo_DiffersInPatch()
    {
        var a = new SemanticVersion(1, 2, 3);
        var b = new SemanticVersion(1, 2, 4);
        (a < b).ShouldBeTrue();
    }

    // Parse: version embedded in text (not just a version string)
    [Fact]
    public void Parse_VersionInText_ExtractsVersion()
    {
        // The regex uses Match (not FullMatch), so "Vagrant 2.3.7" will match "2.3.7"
        var v = SemanticVersion.Parse("Vagrant 2.3.7");
        v.Major.ShouldBe(2);
        v.Minor.ShouldBe(3);
        v.Patch.ShouldBe(7);
    }

    // Parse: version with build metadata only (no pre-release)
    [Fact]
    public void Parse_VersionWithBuildMetadataOnly()
    {
        var v = SemanticVersion.Parse("1.2.3+build.42");
        v.PreRelease.ShouldBeNull();
        v.BuildMetadata.ShouldBe("build.42");
    }

    // Parse: version with both pre-release and build metadata
    [Fact]
    public void Parse_VersionWithPreReleaseAndBuildMetadata()
    {
        var v = SemanticVersion.Parse("1.0.0-beta.1+sha.abc123");
        v.PreRelease.ShouldBe("beta.1");
        v.BuildMetadata.ShouldBe("sha.abc123");
    }

    // TryParse: whitespace-only string returns false
    [Fact]
    public void TryParse_WhitespaceOnly_ReturnsFalse()
    {
        SemanticVersion.TryParse("   ", out var v).ShouldBeFalse();
        v.ShouldBeNull();
    }

    // TryParse: non-matching string returns false
    [Fact]
    public void TryParse_NonMatching_ReturnsFalse()
    {
        SemanticVersion.TryParse("no-version-here", out var v).ShouldBeFalse();
        v.ShouldBeNull();
    }

    // ToString: with pre-release only
    [Fact]
    public void ToString_WithPreReleaseOnly()
    {
        new SemanticVersion(2, 1, 0, "rc.1").ToString().ShouldBe("2.1.0-rc.1");
    }

    // ToString: with build metadata only
    [Fact]
    public void ToString_WithBuildMetadataOnly()
    {
        new SemanticVersion(2, 1, 0, null, "sha.abc").ToString().ShouldBe("2.1.0+sha.abc");
    }

    // Operators: <= and >= edge cases
    [Fact]
    public void Operators_LessOrEqual_WhenLess()
    {
        (new SemanticVersion(1, 0, 0) <= new SemanticVersion(2, 0, 0)).ShouldBeTrue();
    }

    [Fact]
    public void Operators_GreaterOrEqual_WhenGreater()
    {
        (new SemanticVersion(2, 0, 0) >= new SemanticVersion(1, 0, 0)).ShouldBeTrue();
    }

    // Constructor: default minor and patch
    [Fact]
    public void Constructor_MajorOnly_MinorAndPatchDefaultToZero()
    {
        var v = new SemanticVersion(5);
        v.Minor.ShouldBe(0);
        v.Patch.ShouldBe(0);
        v.PreRelease.ShouldBeNull();
        v.BuildMetadata.ShouldBeNull();
    }

    // Parse: whitespace throws
    [Fact]
    public void Parse_Whitespace_Throws()
    {
        Should.Throw<ArgumentException>(() => SemanticVersion.Parse("  "));
    }
}

// ── BinaryIdentifier Final Coverage ──────────────────────────────────────────

public sealed class BinaryIdentifierFinalCoverageTests
{
    // Parse: colon at end means empty version
    [Fact]
    public void Parse_ColonAtEnd_EmptyVersion()
    {
        var id = BinaryIdentifier.Parse("docker:");
        id.Name.ShouldBe("docker");
        id.Version.ShouldBe("");
    }

    // Parse: multiple colons — only first is split point
    [Fact]
    public void Parse_MultipleColons_SplitsOnFirst()
    {
        var id = BinaryIdentifier.Parse("docker:24.0:extra");
        id.Name.ShouldBe("docker");
        id.Version.ShouldBe("24.0:extra");
    }

    // Record equality
    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new BinaryIdentifier("docker", "24.0");
        var b = new BinaryIdentifier("docker", "24.0");
        a.ShouldBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentVersion_NotEqual()
    {
        var a = new BinaryIdentifier("docker", "24.0");
        var b = new BinaryIdentifier("docker", "25.0");
        a.ShouldNotBe(b);
    }

    // Constructor: empty string for name throws
    [Fact]
    public void Constructor_EmptyName_Throws()
    {
        Should.Throw<ArgumentException>(() => new BinaryIdentifier(""));
    }
}

// ── VersionGuard Final Coverage ──────────────────────────────────────────────

public sealed class VersionGuardFinalCoverageTests
{
    private static readonly SemanticVersion V1_0 = new(1, 0, 0);
    private static readonly SemanticVersion V1_5 = new(1, 5, 0);
    private static readonly SemanticVersion V2_0 = new(2, 0, 0);

    // EnsureCommandSupported: only since, version is exactly since => does not throw
    [Fact]
    public void EnsureCommandSupported_ExactlySince_NullUntil_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureCommandSupported(V1_5, "build", V1_5, null));
    }

    // EnsureCommandSupported: only until, version is just below until => does not throw
    [Fact]
    public void EnsureCommandSupported_JustBelowUntil_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureCommandSupported(V1_5, "build", null, V2_0));
    }

    // EnsureOptionSupported: only since, exactly since => does not throw
    [Fact]
    public void EnsureOptionSupported_ExactlySince_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureOptionSupported(V1_5, "build", "force", V1_5, null));
    }

    // EnsureOptionSupported: detected == until => throws
    [Fact]
    public void EnsureOptionSupported_ExactlyUntil_Throws()
    {
        var ex = Should.Throw<OptionNotSupportedException>(() =>
            VersionGuard.EnsureOptionSupported(V2_0, "build", "debug", V1_0, V2_0));
        ex.Message.ShouldContain("was removed in version 2.0.0");
    }

    // EnsureOptionSupported: both null bounds => does not throw
    [Fact]
    public void EnsureOptionSupported_BothBoundsNull_DoesNotThrow()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureOptionSupported(V1_5, "build", "force", null, null));
    }

    // EnsureCommandSupported: detected is null => returns immediately
    [Fact]
    public void EnsureCommandSupported_NullDetectedVersion_NeverThrows()
    {
        Should.NotThrow(() =>
            VersionGuard.EnsureCommandSupported(null, "cmd", V1_0, V2_0));
    }
}

// ── CommandNotSupportedException Final Coverage ──────────────────────────────

public sealed class CommandNotSupportedExceptionFinalCoverageTests
{
    // FormatMessage: until is null => "requires version" message
    [Fact]
    public void FormatMessage_UntilNull_RequiresMessage()
    {
        var ex = new CommandNotSupportedException(
            "deploy", new SemanticVersion(1, 0, 0),
            new SemanticVersion(2, 0, 0), null);
        ex.Message.ShouldBe("Command 'deploy' requires version 2.0.0 or later (detected: 1.0.0).");
    }

    // FormatMessage: until is not null but detected < until => "requires version" message
    [Fact]
    public void FormatMessage_DetectedBeforeUntil_RequiresMessage()
    {
        var ex = new CommandNotSupportedException(
            "deploy", new SemanticVersion(0, 9, 0),
            new SemanticVersion(1, 0, 0), new SemanticVersion(3, 0, 0));
        ex.Message.ShouldBe("Command 'deploy' requires version 1.0.0 or later (detected: 0.9.0).");
    }

    // FormatMessage: until is not null and detected >= until => "was removed" message
    [Fact]
    public void FormatMessage_DetectedAtOrPastUntil_RemovedMessage()
    {
        var ex = new CommandNotSupportedException(
            "deploy", new SemanticVersion(3, 0, 0),
            new SemanticVersion(1, 0, 0), new SemanticVersion(3, 0, 0));
        ex.Message.ShouldBe("Command 'deploy' was removed in version 3.0.0 (detected: 3.0.0).");
    }

    // Properties
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        var since = new SemanticVersion(1, 0, 0);
        var until = new SemanticVersion(2, 0, 0);
        var detected = new SemanticVersion(2, 5, 0);
        var ex = new CommandNotSupportedException("cmd", detected, since, until);
        ex.CommandPath.ShouldBe("cmd");
        ex.DetectedVersion.ShouldBe(detected);
        ex.Since.ShouldBe(since);
        ex.Until.ShouldBe(until);
    }
}

// ── OptionNotSupportedException Final Coverage ───────────────────────────────

public sealed class OptionNotSupportedExceptionFinalCoverageTests
{
    // FormatMessage: until is null => "requires version" message
    [Fact]
    public void FormatMessage_UntilNull_RequiresMessage()
    {
        var ex = new OptionNotSupportedException(
            "build", "json-output", new SemanticVersion(1, 0, 0),
            new SemanticVersion(2, 0, 0), null);
        ex.Message.ShouldBe("Option 'json-output' on command 'build' requires version 2.0.0 or later (detected: 1.0.0).");
    }

    // FormatMessage: until is not null but detected < until => "requires version"
    [Fact]
    public void FormatMessage_DetectedBeforeUntil_RequiresMessage()
    {
        var ex = new OptionNotSupportedException(
            "build", "json-output", new SemanticVersion(0, 5, 0),
            new SemanticVersion(1, 0, 0), new SemanticVersion(3, 0, 0));
        ex.Message.ShouldBe("Option 'json-output' on command 'build' requires version 1.0.0 or later (detected: 0.5.0).");
    }

    // FormatMessage: until is not null and detected >= until => "was removed"
    [Fact]
    public void FormatMessage_DetectedAtOrPastUntil_RemovedMessage()
    {
        var ex = new OptionNotSupportedException(
            "build", "legacy-flag", new SemanticVersion(3, 0, 0),
            new SemanticVersion(1, 0, 0), new SemanticVersion(2, 0, 0));
        ex.Message.ShouldBe("Option 'legacy-flag' on command 'build' was removed in version 2.0.0 (detected: 3.0.0).");
    }

    // Properties
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        var since = new SemanticVersion(1, 5, 0);
        var until = new SemanticVersion(3, 0, 0);
        var detected = new SemanticVersion(4, 0, 0);
        var ex = new OptionNotSupportedException("run", "flag", detected, since, until);
        ex.CommandPath.ShouldBe("run");
        ex.OptionName.ShouldBe("flag");
        ex.DetectedVersion.ShouldBe(detected);
        ex.Since.ShouldBe(since);
        ex.Until.ShouldBe(until);
    }
}

// ── CommandExecutor.BuildProcessSpec Final Coverage ───────────────────────────

public sealed class CommandExecutorBuildProcessSpecFinalCoverageTests
{
    // Override with arg that starts with "--" but is not in unsupported or mapped
    [Fact]
    public async Task BuildProcessSpec_OverridesPresent_UnmappedDashDashArg_PassedThrough()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/bin/test",
            Overrides = new Dictionary<string, CommandOverrides>
            {
                ["run"] = new CommandOverrides
                {
                    OptionNameMappings = new Dictionary<string, string> { ["old"] = "new" },
                    UnsupportedOptions = new HashSet<string> { "removed" }
                }
            }
        };
        var runner = new FakeProcessRunner(stdout: "ok\n");
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);

        var cmd = new FakeCommand
        {
            CommandPath = ["run"],
            Args = ["--keep-this", "--old", "--removed"]
        };

        await executor.ExecuteAsync(new BinaryIdentifier("test"), cmd);

        var args = runner.LastSpec!.Arguments.ToList();
        args.ShouldContain("--keep-this"); // not in mappings or unsupported
        args.ShouldContain("--new");       // mapped from --old
        args.ShouldNotContain("--removed");
        args.ShouldNotContain("--old");
    }

    // No overrides at all (empty Overrides dict)
    [Fact]
    public async Task BuildProcessSpec_EmptyOverrides_AllArgsPassedThrough()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/bin/test"
            // Overrides defaults to empty
        };
        var runner = new FakeProcessRunner(stdout: "ok\n");
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), runner);

        var cmd = new FakeCommand
        {
            CommandPath = ["run"],
            Args = ["--flag", "value", "positional"]
        };

        await executor.ExecuteAsync(new BinaryIdentifier("test"), cmd);

        var args = runner.LastSpec!.Arguments.ToList();
        args.ShouldContain("run");
        args.ShouldContain("--flag");
        args.ShouldContain("value");
        args.ShouldContain("positional");
    }

    // Overrides: arg does not start with "--" but overrides exist for the command
    [Fact]
    public async Task BuildProcessSpec_NonDashArgsNotAffectedByOverrides()
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
            Args = ["-v", "positional", "--verbose"]
        };

        await executor.ExecuteAsync(new BinaryIdentifier("test"), cmd);

        var args = runner.LastSpec!.Arguments.ToList();
        args.ShouldContain("-v");           // short flag not affected
        args.ShouldContain("positional");   // positional not affected
        args.ShouldNotContain("--verbose"); // this one is filtered
    }

    // Empty command path
    [Fact]
    public async Task BuildProcessSpec_EmptyCommandPath_NoSegmentsPrepended()
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
            CommandPath = [],
            Args = ["--flag"]
        };

        await executor.ExecuteAsync(new BinaryIdentifier("test"), cmd);

        var args = runner.LastSpec!.Arguments.ToList();
        args.Count.ShouldBe(1);
        args[0].ShouldBe("--flag");
    }
}

// ── StandardVersionDetector Final Coverage ───────────────────────────────────

public sealed class StandardVersionDetectorFinalCoverageTests
{
    // Version text with prefix (e.g., "v1.2.3")
    [Fact]
    public async Task DetectAsync_VersionWithPrefix_ParsesCorrectly()
    {
        var runner = new FakeProcessRunner(stdout: "packer v1.11.2\n");
        var detector = new StandardVersionDetector(runner);
        var result = await detector.DetectAsync("/usr/bin/packer");
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Major.ShouldBe(1);
        result.Value!.Minor.ShouldBe(11);
        result.Value!.Patch.ShouldBe(2);
    }

    // Non-zero exit with non-empty stderr
    [Fact]
    public async Task DetectAsync_NonZeroExit_ErrorContainsStderr()
    {
        var runner = new FakeProcessRunner(stderr: "command not found", exitCode: 127);
        var detector = new StandardVersionDetector(runner);
        var result = await detector.DetectAsync("/usr/bin/missing");
        result.IsFailure.ShouldBeTrue();
        result.Error!.StandardError.ShouldBe("command not found");
        result.Error!.ExitCode.ShouldBe(127);
    }

    // Output is just whitespace
    [Fact]
    public async Task DetectAsync_WhitespaceOutput_ReturnsFailure()
    {
        var runner = new FakeProcessRunner(stdout: "   \n  ");
        var detector = new StandardVersionDetector(runner);
        var result = await detector.DetectAsync("/usr/bin/test");
        result.IsFailure.ShouldBeTrue();
        result.Error!.Message.ShouldContain("Could not parse version");
    }

    // Version without patch (e.g., "2.0")
    [Fact]
    public async Task DetectAsync_TwoPartVersion_ParsesCorrectly()
    {
        var runner = new FakeProcessRunner(stdout: "2.4\n");
        var detector = new StandardVersionDetector(runner);
        var result = await detector.DetectAsync("/usr/bin/tool");
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Major.ShouldBe(2);
        result.Value!.Minor.ShouldBe(4);
        result.Value!.Patch.ShouldBe(0);
    }
}

// ── CommandExecution Final Coverage ──────────────────────────────────────────

public sealed class CommandExecutionFinalCoverageTests
{
    private static BinaryBinding TestBinding => new()
    {
        Identifier = new BinaryIdentifier("test"),
        ExecutablePath = "/bin/test"
    };

    // GetAsyncEnumerator with explicit CancellationToken
    [Fact]
    public async Task GetAsyncEnumerator_WithCancellationToken_Works()
    {
        var lines = new[] { new OutputLine("EVENT:val", OutputSource.StdOut) };
        var runner = new FakeProcessRunner(lines);
        var executor = new CommandExecutor(new DictionaryBinaryResolver([TestBinding]), runner);
        var execution = new CommandExecution<TestEvent>(
            executor, new BinaryIdentifier("test"), new FakeCommand(), new TestOutputParser());

        using var cts = new CancellationTokenSource();
        var enumerator = execution.GetAsyncEnumerator(cts.Token);
        var events = new List<TestEvent>();
        while (await enumerator.MoveNextAsync())
            events.Add(enumerator.Current);

        events.Count.ShouldBe(2); // EVENT:val + EXIT:0
    }
}

// ── DictionaryBinaryResolver Final Coverage ──────────────────────────────────

public sealed class DictionaryBinaryResolverFinalCoverageTests
{
    // Constructor with IReadOnlyDictionary (the first overload)
    [Fact]
    public async Task Constructor_WithReadOnlyDictionary_Works()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("test"),
            ExecutablePath = "/bin/test"
        };
        IReadOnlyDictionary<string, BinaryBinding> dict = new Dictionary<string, BinaryBinding>
        {
            ["test"] = binding
        };
        var resolver = new DictionaryBinaryResolver(dict);
        var result = await resolver.ResolveAsync(new BinaryIdentifier("test"));
        result.IsSuccess.ShouldBeTrue();
        result.Value!.ExecutablePath.ShouldBe("/bin/test");
    }

    // Constructor with params IEnumerable<BinaryBinding> (the second overload)
    [Fact]
    public async Task Constructor_WithEnumerable_Works()
    {
        var bindings = new List<BinaryBinding>
        {
            new() { Identifier = new BinaryIdentifier("a"), ExecutablePath = "/bin/a" },
            new() { Identifier = new BinaryIdentifier("b"), ExecutablePath = "/bin/b" }
        };
        var resolver = new DictionaryBinaryResolver(bindings);
        (await resolver.ResolveAsync(new BinaryIdentifier("a"))).IsSuccess.ShouldBeTrue();
        (await resolver.ResolveAsync(new BinaryIdentifier("b"))).IsSuccess.ShouldBeTrue();
    }

    // ResolveAsync with CancellationToken
    [Fact]
    public async Task ResolveAsync_WithCancellationToken_Works()
    {
        var resolver = new DictionaryBinaryResolver([
            new BinaryBinding
            {
                Identifier = new BinaryIdentifier("x"),
                ExecutablePath = "/bin/x"
            }
        ]);
        using var cts = new CancellationTokenSource();
        var result = await resolver.ResolveAsync(new BinaryIdentifier("x"), cts.Token);
        result.IsSuccess.ShouldBeTrue();
    }
}

// ── ProcessSpec Final Coverage ───────────────────────────────────────────────

public sealed class ProcessSpecFinalCoverageTests
{
    // Default values for all fields
    [Fact]
    public void AllDefaults_AreCorrect()
    {
        var spec = new ProcessSpec { ExecutablePath = "/bin/test" };
        spec.ExecutablePath.ShouldBe("/bin/test");
        spec.Arguments.ShouldBeEmpty();
        spec.WorkingDirectory.ShouldBeNull();
        spec.EnvironmentVariables.ShouldBeEmpty();
        spec.Timeout.ShouldBeNull();
    }

    // With all fields set
    [Fact]
    public void AllFieldsSet_ReturnCorrectValues()
    {
        var spec = new ProcessSpec
        {
            ExecutablePath = "/bin/test",
            Arguments = ["--flag", "val"],
            WorkingDirectory = "/tmp",
            EnvironmentVariables = new Dictionary<string, string> { ["K"] = "V" },
            Timeout = TimeSpan.FromMilliseconds(500)
        };
        spec.Arguments.Count.ShouldBe(2);
        spec.WorkingDirectory.ShouldBe("/tmp");
        spec.EnvironmentVariables.Count.ShouldBe(1);
        spec.Timeout!.Value.TotalMilliseconds.ShouldBe(500);
    }
}

// ── BinaryBinding Final Coverage ─────────────────────────────────────────────

public sealed class BinaryBindingFinalCoverageTests
{
    [Fact]
    public void ToString_ContainsIdentifierInfo()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("docker"),
            ExecutablePath = "/usr/bin/docker"
        };
        var str = binding.ToString();
        str.ShouldContain("docker");
    }

    [Fact]
    public void With_ModifiesDetectedVersion()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("docker"),
            ExecutablePath = "/usr/bin/docker"
        };
        var modified = binding with { DetectedVersion = new SemanticVersion(24, 0, 7) };
        modified.DetectedVersion.ShouldNotBeNull();
        modified.DetectedVersion!.Major.ShouldBe(24);
        binding.DetectedVersion.ShouldBeNull();
    }
}

// ── OutputLine Final Coverage ────────────────────────────────────────────────

public sealed class OutputLineFinalCoverageTests
{
    [Fact]
    public void ToString_ContainsTextAndSource()
    {
        var line = new OutputLine("hello", OutputSource.StdOut);
        var str = line.ToString();
        str.ShouldContain("hello");
        str.ShouldContain("StdOut");
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var line = new OutputLine("hello", OutputSource.StdOut);
        var modified = line with { Source = OutputSource.StdErr };
        modified.Source.ShouldBe(OutputSource.StdErr);
        modified.Text.ShouldBe("hello");
        line.Source.ShouldBe(OutputSource.StdOut);
    }

    [Fact]
    public void Deconstruct_Works()
    {
        var (text, source) = new OutputLine("test", OutputSource.StdErr);
        text.ShouldBe("test");
        source.ShouldBe(OutputSource.StdErr);
    }
}

// ── CommandError Final Coverage ──────────────────────────────────────────────

public sealed class CommandErrorFinalCoverageTests
{
    [Fact]
    public void ToString_ContainsAllFields()
    {
        var error = new CommandError(1, "stderr", "msg");
        var str = error.ToString();
        str.ShouldContain("1");
        str.ShouldContain("stderr");
        str.ShouldContain("msg");
    }
}

// ── BinaryResolutionError Final Coverage ─────────────────────────────────────

public sealed class BinaryResolutionErrorFinalCoverageTests
{
    [Fact]
    public void ToString_ContainsIdentifierAndMessage()
    {
        var id = new BinaryIdentifier("docker");
        var error = new BinaryResolutionError(id, "not found");
        var str = error.ToString();
        str.ShouldContain("docker");
        str.ShouldContain("not found");
    }
}
