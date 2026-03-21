using CsCheck;
using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Tests;

// ── FakeProcessRunner Tests ─────────────────────────────────────────────────

public sealed class FakeProcessRunnerTests
{
    [Fact]
    public async Task StringConstructor_DefaultValues_ReturnEmptyStrings()
    {
        var runner = new FakeProcessRunner();
        var result = await runner.RunAsync(new ProcessSpec { ExecutablePath = "/bin/test" });

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe("");
        result.StandardError.ShouldBe("");
    }

    [Fact]
    public async Task StringConstructor_WithStdout_ReturnsStdout()
    {
        var runner = new FakeProcessRunner(stdout: "hello world\n");
        var result = await runner.RunAsync(new ProcessSpec { ExecutablePath = "/bin/test" });

        result.StandardOutput.ShouldBe("hello world\n");
    }

    [Fact]
    public async Task StringConstructor_WithStderr_ReturnsStderr()
    {
        var runner = new FakeProcessRunner(stderr: "error message");
        var result = await runner.RunAsync(new ProcessSpec { ExecutablePath = "/bin/test" });

        result.StandardError.ShouldBe("error message");
    }

    [Fact]
    public async Task StringConstructor_WithExitCode_ReturnsExitCode()
    {
        var runner = new FakeProcessRunner(exitCode: 42);
        var result = await runner.RunAsync(new ProcessSpec { ExecutablePath = "/bin/test" });

        result.ExitCode.ShouldBe(42);
    }

    [Fact]
    public async Task LinesConstructor_StreamAsync_YieldsAllLines()
    {
        var lines = new[]
        {
            new OutputLine("line1", OutputSource.StdOut),
            new OutputLine("err1", OutputSource.StdErr),
            new OutputLine("line2", OutputSource.StdOut),
        };
        var runner = new FakeProcessRunner(lines);

        var collected = new List<OutputLine>();
        await foreach (var line in runner.StreamAsync(new ProcessSpec { ExecutablePath = "/bin/test" }))
            collected.Add(line);

        collected.Count.ShouldBe(3);
        collected[0].ShouldBe(new OutputLine("line1", OutputSource.StdOut));
        collected[1].ShouldBe(new OutputLine("err1", OutputSource.StdErr));
        collected[2].ShouldBe(new OutputLine("line2", OutputSource.StdOut));
    }

    [Fact]
    public async Task LinesConstructor_RunAsync_JoinsOutputCorrectly()
    {
        var lines = new[]
        {
            new OutputLine("out1", OutputSource.StdOut),
            new OutputLine("err1", OutputSource.StdErr),
            new OutputLine("out2", OutputSource.StdOut),
        };
        var runner = new FakeProcessRunner(lines, exitCode: 7);

        var result = await runner.RunAsync(new ProcessSpec { ExecutablePath = "/bin/test" });

        result.ExitCode.ShouldBe(7);
        result.StandardOutput.ShouldContain("out1");
        result.StandardOutput.ShouldContain("out2");
        result.StandardError.ShouldContain("err1");
    }

    [Fact]
    public async Task RunAsync_TracksLastSpec()
    {
        var runner = new FakeProcessRunner();
        runner.LastSpec.ShouldBeNull();

        var spec = new ProcessSpec { ExecutablePath = "/bin/test", Arguments = ["--flag"] };
        await runner.RunAsync(spec);

        runner.LastSpec.ShouldBe(spec);
    }

    [Fact]
    public async Task StreamAsync_TracksLastSpec()
    {
        var runner = new FakeProcessRunner(Array.Empty<OutputLine>());
        var spec = new ProcessSpec { ExecutablePath = "/bin/other" };

        await foreach (var _ in runner.StreamAsync(spec)) { }

        runner.LastSpec.ShouldBe(spec);
    }

    [Fact]
    public async Task StreamAsync_EmptyLines_YieldsNothing()
    {
        var runner = new FakeProcessRunner(Array.Empty<OutputLine>());

        var collected = new List<OutputLine>();
        await foreach (var line in runner.StreamAsync(new ProcessSpec { ExecutablePath = "/bin/test" }))
            collected.Add(line);

        collected.ShouldBeEmpty();
    }

    [Fact]
    public async Task StringConstructor_StreamAsync_SplitsIntoLines()
    {
        var runner = new FakeProcessRunner(stdout: "line1\nline2", stderr: "err1");

        var collected = new List<OutputLine>();
        await foreach (var line in runner.StreamAsync(new ProcessSpec { ExecutablePath = "/bin/test" }))
            collected.Add(line);

        var stdoutLines = collected.Where(l => l.Source == OutputSource.StdOut).Select(l => l.Text).ToList();
        var stderrLines = collected.Where(l => l.Source == OutputSource.StdErr).Select(l => l.Text).ToList();
        stdoutLines.ShouldContain("line1");
        stdoutLines.ShouldContain("line2");
        stderrLines.ShouldContain("err1");
    }

    [Fact]
    public async Task RunAsync_SecondCall_OverwritesLastSpec()
    {
        var runner = new FakeProcessRunner();
        var spec1 = new ProcessSpec { ExecutablePath = "/bin/a" };
        var spec2 = new ProcessSpec { ExecutablePath = "/bin/b" };

        await runner.RunAsync(spec1);
        runner.LastSpec!.ExecutablePath.ShouldBe("/bin/a");

        await runner.RunAsync(spec2);
        runner.LastSpec!.ExecutablePath.ShouldBe("/bin/b");
    }
}

// ── FakeCommand Tests ───────────────────────────────────────────────────────

public sealed class FakeCommandTests
{
    [Fact]
    public void DefaultCommandPath_IsTest()
    {
        new FakeCommand().CommandPath.ShouldBe(new[] { "test" });
    }

    [Fact]
    public void DefaultArgs_IsEmpty()
    {
        new FakeCommand().Args.ShouldBeEmpty();
    }

    [Fact]
    public void ToArguments_ReturnsArgs()
    {
        var cmd = new FakeCommand { Args = ["--flag", "value"] };
        cmd.ToArguments().ShouldBe(new[] { "--flag", "value" });
    }

    [Fact]
    public void CommandPath_CanBeCustomized()
    {
        var cmd = new FakeCommand { CommandPath = ["container", "run"] };
        cmd.CommandPath.ShouldBe(new[] { "container", "run" });
    }

    [Fact]
    public void ImplementsICliCommand()
    {
        new FakeCommand().ShouldBeAssignableTo<ICliCommand>();
    }
}

// ── TestOutputParser Tests ──────────────────────────────────────────────────

public sealed class TestOutputParserTests
{
    [Fact]
    public void ParseLine_EventLine_YieldsEvent()
    {
        var parser = new TestOutputParser();
        var events = parser.ParseLine(new OutputLine("EVENT:hello", OutputSource.StdOut)).ToList();

        events.Count.ShouldBe(1);
        events[0].Value.ShouldBe("hello");
    }

    [Fact]
    public void ParseLine_NonEventLine_YieldsNothing()
    {
        var parser = new TestOutputParser();
        var events = parser.ParseLine(new OutputLine("just a line", OutputSource.StdOut)).ToList();

        events.ShouldBeEmpty();
    }

    [Fact]
    public void ParseLine_StderrEventLine_YieldsEvent()
    {
        var parser = new TestOutputParser();
        var events = parser.ParseLine(new OutputLine("EVENT:from-stderr", OutputSource.StdErr)).ToList();

        events.Count.ShouldBe(1);
        events[0].Value.ShouldBe("from-stderr");
    }

    [Fact]
    public void Complete_YieldsExitEvent()
    {
        var parser = new TestOutputParser();
        var events = parser.Complete(0).ToList();

        events.Count.ShouldBe(1);
        events[0].Value.ShouldBe("EXIT:0");
    }

    [Fact]
    public void Complete_NonZeroExit_IncludesExitCode()
    {
        var parser = new TestOutputParser();
        var events = parser.Complete(127).ToList();

        events[0].Value.ShouldBe("EXIT:127");
    }
}

// ── EmptyCompleteParser Tests ───────────────────────────────────────────────

public sealed class EmptyCompleteParserTests
{
    [Fact]
    public void ParseLine_EventLine_YieldsEvent()
    {
        var parser = new EmptyCompleteParser();
        var events = parser.ParseLine(new OutputLine("EVENT:data", OutputSource.StdOut)).ToList();

        events.Count.ShouldBe(1);
        events[0].Value.ShouldBe("data");
    }

    [Fact]
    public void ParseLine_NonEventLine_YieldsNothing()
    {
        var parser = new EmptyCompleteParser();
        parser.ParseLine(new OutputLine("ignored", OutputSource.StdOut)).ToList().ShouldBeEmpty();
    }

    [Fact]
    public void Complete_ReturnsEmpty()
    {
        var parser = new EmptyCompleteParser();
        parser.Complete(0).ToList().ShouldBeEmpty();
    }

    [Fact]
    public void Complete_AnyExitCode_ReturnsEmpty()
    {
        var parser = new EmptyCompleteParser();
        parser.Complete(42).ToList().ShouldBeEmpty();
    }
}

// ── TestCollector Tests ─────────────────────────────────────────────────────

public sealed class TestCollectorTests
{
    [Fact]
    public void Complete_NoEvents_ReturnsEmpty()
    {
        var collector = new TestCollector();
        collector.Complete().ShouldBe("");
    }

    [Fact]
    public void OnEvent_SingleEvent_CompletesToSingleValue()
    {
        var collector = new TestCollector();
        collector.OnEvent(new TestEvent("hello"));
        collector.Complete().ShouldBe("hello");
    }

    [Fact]
    public void OnEvent_MultipleEvents_JoinsWithComma()
    {
        var collector = new TestCollector();
        collector.OnEvent(new TestEvent("a"));
        collector.OnEvent(new TestEvent("b"));
        collector.OnEvent(new TestEvent("c"));
        collector.Complete().ShouldBe("a,b,c");
    }
}

// ── TestBindings Extended Tests ─────────────────────────────────────────────

public sealed class TestBindingsExtendedTests
{
    [Fact]
    public void Create_NameOnly_HasNullDetectedVersion()
    {
        var binding = TestBindings.Create("docker");
        binding.DetectedVersion.ShouldBeNull();
    }

    [Fact]
    public void Create_WithVersion_SetsCorrectIdentifier()
    {
        var v = new SemanticVersion(1, 2, 3);
        var binding = TestBindings.Create("packer", v);
        binding.Identifier.Name.ShouldBe("packer");
        binding.ExecutablePath.ShouldBe("/usr/bin/packer");
        binding.DetectedVersion.ShouldBe(v);
    }

    [Fact]
    public async Task ResolverFor_SingleName_FailsForOther()
    {
        var resolver = TestBindings.ResolverFor("docker");
        var result = await resolver.ResolveAsync(new BinaryIdentifier("vagrant"));
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolverFor_ParamsBindings_AllResolve()
    {
        var resolver = TestBindings.ResolverFor(
            TestBindings.Create("a"),
            TestBindings.Create("b"),
            TestBindings.Create("c"));

        (await resolver.ResolveAsync(new BinaryIdentifier("a"))).IsSuccess.ShouldBeTrue();
        (await resolver.ResolveAsync(new BinaryIdentifier("b"))).IsSuccess.ShouldBeTrue();
        (await resolver.ResolveAsync(new BinaryIdentifier("c"))).IsSuccess.ShouldBeTrue();
    }
}

// ── MockContainerRuntime Tests ──────────────────────────────────────────────

public sealed class MockContainerRuntimeTests
{
    [Fact]
    public async Task CustomCallbacks_AreCalled()
    {
        var buildCalled = false;
        var runCalled = false;
        var removeCalled = false;

        var runtime = new MockContainerRuntime(
            onBuild: (tag, _) => { buildCalled = true; return Task.FromResult(tag); },
            onRun: (tag, _) => { runCalled = true; return Task.FromResult("output"); },
            onRemove: _ => { removeCalled = true; return Task.CompletedTask; });

        await runtime.BuildAsync("img:1", "FROM alpine");
        buildCalled.ShouldBeTrue();

        await runtime.RunAsync("img:1", ["--help"]);
        runCalled.ShouldBeTrue();

        await runtime.RemoveImageAsync("img:1");
        removeCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task WithHelpText_RunAsync_ReturnsCannedText()
    {
        var runtime = MockContainerRuntime.WithHelpText("Usage: packer build [flags]");

        var result = await runtime.RunAsync("img:1", ["--help"]);
        result.ShouldBe("Usage: packer build [flags]");
    }

    [Fact]
    public async Task WithHelpText_BuildAsync_ReturnsBuilt()
    {
        var runtime = MockContainerRuntime.WithHelpText("help text");
        var result = await runtime.BuildAsync("img:1", "FROM alpine");
        result.ShouldBe("built");
    }

    [Fact]
    public async Task WithHelpText_RemoveAsync_CompletesSuccessfully()
    {
        var runtime = MockContainerRuntime.WithHelpText("help");
        await runtime.RemoveImageAsync("img:1"); // should not throw
    }

    [Fact]
    public async Task NoOp_AllMethods_ReturnDefaults()
    {
        var runtime = MockContainerRuntime.NoOp;

        var buildResult = await runtime.BuildAsync("tag", "content");
        buildResult.ShouldBe("");

        var runResult = await runtime.RunAsync("tag", ["cmd"]);
        runResult.ShouldBe("");

        await runtime.RemoveImageAsync("tag"); // should not throw
    }
}

// ── FakeHttpHandler Tests ───────────────────────────────────────────────────

public sealed class FakeHttpHandlerTests
{
    [Fact]
    public async Task SendAsync_ReturnsJsonContent()
    {
        var handler = new FakeHttpHandler("[{\"id\": 1}]");
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com/api");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        body.ShouldBe("[{\"id\": 1}]");
    }

    [Fact]
    public async Task SendAsync_WithLinkHeader_IncludesHeader()
    {
        var handler = new FakeHttpHandler("[]", linkHeader: "<https://example.com/api?page=2>; rel=\"next\"");
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com/api");

        response.Headers.Contains("Link").ShouldBeTrue();
        response.Headers.GetValues("Link").First().ShouldContain("page=2");
    }

    [Fact]
    public async Task SendAsync_WithoutLinkHeader_NoLinkHeader()
    {
        var handler = new FakeHttpHandler("{}");
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com/api");

        response.Headers.Contains("Link").ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_ContentType_IsJson()
    {
        var handler = new FakeHttpHandler("{}");
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com/api");

        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }
}

// ── Gens Tests ──────────────────────────────────────────────────────────────

public sealed class GensTests
{
    [Fact]
    public void NonEmpty_NeverProducesNullOrWhitespace()
    {
        Gens.NonEmpty.Sample(s =>
        {
            s.ShouldNotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void AnyBinaryId_ProducesValidIdentifiers()
    {
        Gens.AnyBinaryId.Sample(id =>
        {
            id.Name.ShouldNotBeNullOrWhiteSpace();
            id.Name.ShouldNotContain(":");
        });
    }

    [Fact]
    public void AnyOutputLine_ProducesValidLines()
    {
        Gens.AnyOutputLine.Sample(line =>
        {
            line.Text.ShouldNotBeNull();
            (line.Source == OutputSource.StdOut || line.Source == OutputSource.StdErr).ShouldBeTrue();
        });
    }

    [Fact]
    public void AnyVersion_ProducesNonNegativeValues()
    {
        Gens.AnyVersion.Sample(v =>
        {
            v.Major.ShouldBeGreaterThanOrEqualTo(0);
            v.Minor.ShouldBeGreaterThanOrEqualTo(0);
            v.Patch.ShouldBeGreaterThanOrEqualTo(0);
            v.Major.ShouldBeLessThanOrEqualTo(100);
            v.Minor.ShouldBeLessThanOrEqualTo(100);
            v.Patch.ShouldBeLessThanOrEqualTo(100);
        });
    }
}

// ── TestEvent Tests ─────────────────────────────────────────────────────────

public sealed class TestEventTests
{
    [Fact]
    public void RecordEquality_SameValue_AreEqual()
    {
        new TestEvent("a").ShouldBe(new TestEvent("a"));
    }

    [Fact]
    public void RecordEquality_DifferentValue_AreNotEqual()
    {
        new TestEvent("a").ShouldNotBe(new TestEvent("b"));
    }

    [Fact]
    public void Value_ReturnsConstructorValue()
    {
        new TestEvent("hello").Value.ShouldBe("hello");
    }
}
