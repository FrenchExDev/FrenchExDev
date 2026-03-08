using CsCheck;
using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.Vagrant.Tests;

// ── Output Parser Tests ──────────────────────────────────────────────────────

public sealed class VagrantOutputParserTests
{
    private readonly VagrantOutputParser _parser = new();

    [Fact]
    public void ParseLine_MachineOutput_YieldsMachineOutput()
    {
        var line = new OutputLine("==> default: Importing base box...", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantMachineOutput>();
        evt.MachineName.ShouldBe("default");
        evt.Message.ShouldBe("Importing base box...");
    }

    [Fact]
    public void ParseLine_MachineError_YieldsMachineError()
    {
        var line = new OutputLine("==> default (error): VM failed to start", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantMachineError>();
        evt.MachineName.ShouldBe("default");
        evt.Message.ShouldBe("VM failed to start");
    }

    [Fact]
    public void ParseLine_ProvisionerOutput_YieldsProvisionerOutput()
    {
        var line = new OutputLine("    default: Running provisioner: shell...", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantProvisionerOutput>();
        evt.MachineName.ShouldBe("default");
        evt.Message.ShouldBe("Running provisioner: shell...");
    }

    [Fact]
    public void ParseLine_MachineReady_YieldsActionCompleted()
    {
        var line = new OutputLine("==> default: Machine booted and ready!", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantActionCompleted>();
        evt.MachineName.ShouldBe("default");
        evt.Success.ShouldBeTrue();
    }

    [Fact]
    public void ParseLine_UnrecognizedText_YieldsOutputLine()
    {
        var line = new OutputLine("Some random text", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantOutputLine>();
        evt.Text.ShouldBe("Some random text");
        evt.Source.ShouldBe(OutputSource.StdOut);
    }

    [Fact]
    public void ParseLine_EmptyText_YieldsNothing()
    {
        var line = new OutputLine("", OutputSource.StdOut);
        _parser.ParseLine(line).ShouldBeEmpty();
    }

    [Fact]
    public void ParseLine_WhitespaceOnly_YieldsNothing()
    {
        var line = new OutputLine("   \t  ", OutputSource.StdOut);
        _parser.ParseLine(line).ShouldBeEmpty();
    }

    [Fact]
    public void ParseLine_DottedMachineName_YieldsMachineOutput()
    {
        var line = new OutputLine("==> web.server: Forwarding ports...", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantMachineOutput>();
        evt.MachineName.ShouldBe("web.server");
    }

    [Fact]
    public void Complete_ZeroExitCode_YieldsNothing()
    {
        _parser.Complete(0).ShouldBeEmpty();
    }

    [Fact]
    public void Complete_NonZeroExitCode_YieldsError()
    {
        var events = _parser.Complete(1).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantMachineError>();
        evt.MachineName.ShouldBe("vagrant");
        evt.Message.ShouldBe("Process exited with code 1");
    }

    [Fact]
    public void Complete_NegativeExitCode_YieldsError()
    {
        var events = _parser.Complete(-1).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantMachineError>();
        evt.Message.ShouldBe("Process exited with code -1");
    }
}

// ── Machine-Readable Parser Tests ────────────────────────────────────────────

public sealed class VagrantMachineReadableParserTests
{
    private readonly VagrantMachineReadableParser _parser = new();

    [Fact]
    public void ParseLine_ValidCsv_YieldsMachineReadableEvent()
    {
        var line = new OutputLine("1234567890,default,state,running", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantMachineReadableEvent>();
        evt.Timestamp.ShouldBe(1234567890L);
        evt.Target.ShouldBe("default");
        evt.EventType.ShouldBe("state");
        evt.Data.ShouldBe(new[] { "running" });
    }

    [Fact]
    public void ParseLine_VagrantCommaEscape_Unescaped()
    {
        var line = new OutputLine("1234567890,default,type,a%!(VAGRANT_COMMA)b", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        var evt = events[0].ShouldBeOfType<VagrantMachineReadableEvent>();
        evt.Data[0].ShouldBe("a,b");
    }

    [Fact]
    public void ParseLine_MultipleVagrantCommaEscapes_AllUnescaped()
    {
        var line = new OutputLine("1234567890,default,type,a%!(VAGRANT_COMMA)b%!(VAGRANT_COMMA)c", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        var evt = events[0].ShouldBeOfType<VagrantMachineReadableEvent>();
        evt.Data[0].ShouldBe("a,b,c");
    }

    [Fact]
    public void ParseLine_StdErr_YieldsOutputLine()
    {
        var line = new OutputLine("error text", OutputSource.StdErr);
        var events = _parser.ParseLine(line).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantOutputLine>();
        evt.Source.ShouldBe(OutputSource.StdErr);
    }

    [Fact]
    public void ParseLine_TooFewFields_YieldsOutputLine()
    {
        var line = new OutputLine("1234567890,default", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events[0].ShouldBeOfType<VagrantOutputLine>();
    }

    [Fact]
    public void ParseLine_InvalidTimestamp_YieldsOutputLine()
    {
        var line = new OutputLine("notanumber,default,state,running", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        events[0].ShouldBeOfType<VagrantOutputLine>();
    }

    [Fact]
    public void ParseLine_EmptyText_YieldsNothing()
    {
        var line = new OutputLine("", OutputSource.StdOut);
        _parser.ParseLine(line).ShouldBeEmpty();
    }

    [Fact]
    public void ParseLine_WhitespaceOnly_YieldsNothing()
    {
        var line = new OutputLine("   ", OutputSource.StdOut);
        _parser.ParseLine(line).ShouldBeEmpty();
    }

    [Fact]
    public void ParseLine_EmptyFieldsBetweenCommas_PreservesStructure()
    {
        var line = new OutputLine("1234567890,,,type,", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        var evt = events[0].ShouldBeOfType<VagrantMachineReadableEvent>();
        evt.Target.ShouldBe("");
        evt.EventType.ShouldBe("");
        evt.Data.Length.ShouldBe(2);
    }

    [Fact]
    public void ParseLine_NegativeTimestamp_YieldsMachineReadableEvent()
    {
        var line = new OutputLine("-1,default,state,running", OutputSource.StdOut);
        var events = _parser.ParseLine(line).ToList();
        var evt = events[0].ShouldBeOfType<VagrantMachineReadableEvent>();
        evt.Timestamp.ShouldBe(-1L);
    }

    [Fact]
    public void Complete_NonZeroExitCode_YieldsError()
    {
        var events = _parser.Complete(1).ToList();
        events.ShouldHaveSingleItem();
        var evt = events[0].ShouldBeOfType<VagrantMachineError>();
        evt.MachineName.ShouldBe("vagrant");
    }

    [Fact]
    public void Complete_ZeroExitCode_YieldsNothing()
    {
        _parser.Complete(0).ShouldBeEmpty();
    }
}

// ── Collector Tests ──────────────────────────────────────────────────────────

public sealed class VagrantUpCollectorTests
{
    [Fact]
    public void Complete_NoEvents_ReturnsSuccess()
    {
        var collector = new VagrantUpCollector();
        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.MachinesReady.ShouldBeEmpty();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_MachineReady_AddedToList()
    {
        var collector = new VagrantUpCollector();
        collector.OnEvent(new VagrantActionCompleted("default", Success: true));
        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.MachinesReady.ShouldBe(new[] { "default" });
    }

    [Fact]
    public void Complete_MachineError_FailsWithMessage()
    {
        var collector = new VagrantUpCollector();
        collector.OnEvent(new VagrantMachineError("default", "Box not found"));
        var result = collector.Complete();
        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain("default: Box not found");
    }

    [Fact]
    public void Complete_ActionFailed_StillFails()
    {
        var collector = new VagrantUpCollector();
        collector.OnEvent(new VagrantActionCompleted("default", Success: false));
        var result = collector.Complete();
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void Complete_FailedAction_NoError_StillFails()
    {
        // Catches && vs || mutation in !_hasFailure && _errors.Count == 0
        var collector = new VagrantUpCollector();
        collector.OnEvent(new VagrantActionCompleted("default", Success: false));
        var result = collector.Complete();
        result.Success.ShouldBeFalse();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_MultipleMachines_AllTracked()
    {
        var collector = new VagrantUpCollector();
        collector.OnEvent(new VagrantActionCompleted("web", Success: true));
        collector.OnEvent(new VagrantActionCompleted("db", Success: true));
        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.MachinesReady.Count.ShouldBe(2);
    }

    [Fact]
    public void Complete_MixedSuccess_OneFails()
    {
        var collector = new VagrantUpCollector();
        collector.OnEvent(new VagrantActionCompleted("web", Success: true));
        collector.OnEvent(new VagrantActionCompleted("db", Success: false));
        collector.OnEvent(new VagrantMachineError("db", "Timeout"));
        var result = collector.Complete();
        result.Success.ShouldBeFalse();
        result.MachinesReady.ShouldBe(new[] { "web" });
        result.Errors.ShouldContain("db: Timeout");
    }

    [Fact]
    public void OnEvent_IgnoresNonRelevantEvents()
    {
        var collector = new VagrantUpCollector();
        collector.OnEvent(new VagrantMachineOutput("default", "Starting..."));
        collector.OnEvent(new VagrantProvisionerOutput("default", "Installing packages..."));
        collector.OnEvent(new VagrantOutputLine("some text", OutputSource.StdOut));
        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.MachinesReady.ShouldBeEmpty();
        result.Errors.ShouldBeEmpty();
    }
}

// ── Record Equality Tests ────────────────────────────────────────────────────

public sealed class VagrantEventEqualityTests
{
    [Fact]
    public void VagrantMachineOutput_Equality()
    {
        var a = new VagrantMachineOutput("default", "msg");
        var b = new VagrantMachineOutput("default", "msg");
        a.ShouldBe(b);
    }

    [Fact]
    public void VagrantMachineOutput_Inequality()
    {
        var a = new VagrantMachineOutput("default", "msg1");
        var b = new VagrantMachineOutput("default", "msg2");
        a.ShouldNotBe(b);
    }

    [Fact]
    public void VagrantActionCompleted_Equality()
    {
        var a = new VagrantActionCompleted("default", true);
        var b = new VagrantActionCompleted("default", true);
        a.ShouldBe(b);
    }

    [Fact]
    public void VagrantActionCompleted_Inequality_DifferentSuccess()
    {
        var a = new VagrantActionCompleted("default", true);
        var b = new VagrantActionCompleted("default", false);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void VagrantMachineReadableEvent_Equality()
    {
        var a = new VagrantMachineReadableEvent(123, "default", "state", ["running"]);
        var b = new VagrantMachineReadableEvent(123, "default", "state", ["running"]);
        // Array reference equality — different instances are not equal
        a.ShouldNotBe(b);
    }
}

// ── Fuzz Tests ───────────────────────────────────────────────────────────────

public sealed class VagrantOutputParserFuzzTests
{
    [Fact]
    public void ParseLine_ArbitraryInput_NeverThrows()
    {
        var parser = new VagrantOutputParser();
        Gen.Select(Gen.String, Gen.Bool)
           .Select((t, isErr) => new OutputLine(t ?? "", isErr ? OutputSource.StdErr : OutputSource.StdOut))
           .Sample(line =>
           {
               var events = parser.ParseLine(line).ToList();
               events.ShouldNotBeNull();
           });
    }

    [Fact]
    public void Complete_ArbitraryExitCode_NeverThrows()
    {
        var parser = new VagrantOutputParser();
        Gen.Int.Sample(code =>
        {
            var events = parser.Complete(code).ToList();
            events.ShouldNotBeNull();
        });
    }
}

public sealed class VagrantMachineReadableParserFuzzTests
{
    [Fact]
    public void ParseLine_ArbitraryInput_NeverThrows()
    {
        var parser = new VagrantMachineReadableParser();
        Gen.Select(Gen.String, Gen.Bool)
           .Select((t, isErr) => new OutputLine(t ?? "", isErr ? OutputSource.StdErr : OutputSource.StdOut))
           .Sample(line =>
           {
               var events = parser.ParseLine(line).ToList();
               events.ShouldNotBeNull();
           });
    }

    [Fact]
    public void Complete_ArbitraryExitCode_NeverThrows()
    {
        var parser = new VagrantMachineReadableParser();
        Gen.Int.Sample(code =>
        {
            var events = parser.Complete(code).ToList();
            events.ShouldNotBeNull();
        });
    }
}

public sealed class VagrantUpCollectorFuzzTests
{
    private static readonly Gen<VagrantEvent> AnyEvent = Gen.OneOf(
        Gen.Select(Gen.String, Gen.String)
           .Select((n, m) => (VagrantEvent)new VagrantMachineOutput(n ?? "x", m ?? "")),
        Gen.Select(Gen.String, Gen.String)
           .Select((n, m) => (VagrantEvent)new VagrantMachineError(n ?? "x", m ?? "")),
        Gen.Select(Gen.String, Gen.Bool)
           .Select((n, s) => (VagrantEvent)new VagrantActionCompleted(n ?? "x", s)),
        Gen.Select(Gen.String, Gen.String)
           .Select((n, m) => (VagrantEvent)new VagrantProvisionerOutput(n ?? "x", m ?? "")),
        Gen.Select(Gen.String, Gen.Bool)
           .Select((t, isErr) => (VagrantEvent)new VagrantOutputLine(
               t ?? "", isErr ? OutputSource.StdErr : OutputSource.StdOut))
    );

    [Fact]
    public void OnEvent_ArbitraryEvents_NeverThrows()
    {
        AnyEvent.List[0, 50].Sample(events =>
        {
            var collector = new VagrantUpCollector();
            foreach (var evt in events)
                collector.OnEvent(evt);
            var result = collector.Complete();
            result.ShouldNotBeNull();
        });
    }
}

// ── Round-Trip Fuzz Tests ────────────────────────────────────────────────────

public sealed class VagrantOutputParserRoundTripFuzzTests
{
    private static readonly Gen<string> AnyMachineName =
        Gen.Char['a', 'z'].Array[1, 20].Select(cs => new string(cs));

    private static readonly Gen<string> AnyMessage =
        Gen.Char['a', 'z'].Array[1, 30].Select(cs => new string(cs));

    [Fact]
    public void MachineOutput_RoundTrips()
    {
        var parser = new VagrantOutputParser();
        Gen.Select(AnyMachineName, AnyMessage).Sample((name, msg) =>
        {
            var line = new OutputLine($"==> {name}: {msg}", OutputSource.StdOut);
            var events = parser.ParseLine(line).ToList();
            events.ShouldHaveSingleItem();
            var evt = events[0].ShouldBeOfType<VagrantMachineOutput>();
            evt.MachineName.ShouldBe(name);
            evt.Message.ShouldBe(msg);
        });
    }

    [Fact]
    public void MachineError_RoundTrips()
    {
        var parser = new VagrantOutputParser();
        Gen.Select(AnyMachineName, AnyMessage).Sample((name, msg) =>
        {
            var line = new OutputLine($"==> {name} (error): {msg}", OutputSource.StdOut);
            var events = parser.ParseLine(line).ToList();
            events.ShouldHaveSingleItem();
            var evt = events[0].ShouldBeOfType<VagrantMachineError>();
            evt.MachineName.ShouldBe(name);
            evt.Message.ShouldBe(msg);
        });
    }

    [Fact]
    public void ProvisionerOutput_RoundTrips()
    {
        var parser = new VagrantOutputParser();
        Gen.Select(AnyMachineName, AnyMessage).Sample((name, msg) =>
        {
            var line = new OutputLine($"    {name}: {msg}", OutputSource.StdOut);
            var events = parser.ParseLine(line).ToList();
            events.ShouldHaveSingleItem();
            var evt = events[0].ShouldBeOfType<VagrantProvisionerOutput>();
            evt.MachineName.ShouldBe(name);
            evt.Message.ShouldBe(msg);
        });
    }
}

public sealed class VagrantMachineReadableParserRoundTripFuzzTests
{
    private static readonly Gen<string> AnyTarget =
        Gen.Char['a', 'z'].Array[1, 15].Select(cs => new string(cs));

    private static readonly Gen<string> AnyEventType =
        Gen.Char['a', 'z'].Array[1, 15].Select(cs => new string(cs));

    private static readonly Gen<string> AnyDataField =
        Gen.Char['a', 'z'].Array[1, 20].Select(cs => new string(cs));

    [Fact]
    public void MachineReadable_RoundTrips()
    {
        var parser = new VagrantMachineReadableParser();
        Gen.Select(Gen.Long[0, 9999999999], AnyTarget, AnyEventType, AnyDataField)
           .Sample((ts, target, evType, data) =>
           {
               var line = new OutputLine($"{ts},{target},{evType},{data}", OutputSource.StdOut);
               var events = parser.ParseLine(line).ToList();
               events.ShouldHaveSingleItem();
               var evt = events[0].ShouldBeOfType<VagrantMachineReadableEvent>();
               evt.Timestamp.ShouldBe(ts);
               evt.Target.ShouldBe(target);
               evt.EventType.ShouldBe(evType);
               evt.Data.ShouldBe(new[] { data });
           });
    }
}

// ── Integration / Lifecycle Tests ────────────────────────────────────────────

public sealed class VagrantLifecycleTests
{
    [Fact]
    public void VagrantUp_SuccessfulBoot_FullLifecycle()
    {
        var parser = new VagrantOutputParser();
        var collector = new VagrantUpCollector();

        var lines = new[]
        {
            "==> default: Importing base box 'hashicorp/bionic64'...",
            "==> default: Matching MAC address for NAT networking...",
            "==> default: Setting the name of the VM...",
            "==> default: Clearing any previously set network interfaces...",
            "==> default: Preparing network interfaces based on configuration...",
            "==> default: Forwarding ports...",
            "    default: 22 (guest) => 2222 (host) (adapter 1)",
            "==> default: Booting VM...",
            "==> default: Waiting for machine to boot...",
            "    default: SSH address: 127.0.0.1:2222",
            "    default: SSH username: vagrant",
            "==> default: Machine booted and ready!",
            "==> default: Mounting shared folders...",
            "    default: /vagrant => /home/user/project",
        };

        foreach (var text in lines)
        {
            var outputLine = new OutputLine(text, OutputSource.StdOut);
            foreach (var evt in parser.ParseLine(outputLine))
                collector.OnEvent(evt);
        }

        foreach (var evt in parser.Complete(0))
            collector.OnEvent(evt);

        var result = collector.Complete();
        result.Success.ShouldBeTrue();
        result.MachinesReady.ShouldContain("default");
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void VagrantUp_FailedBoot_FullLifecycle()
    {
        var parser = new VagrantOutputParser();
        var collector = new VagrantUpCollector();

        var lines = new[]
        {
            "==> default: Importing base box 'hashicorp/bionic64'...",
            "==> default: Booting VM...",
            "==> default (error): VBoxManage error: machine failed to start",
        };

        foreach (var text in lines)
        {
            var outputLine = new OutputLine(text, OutputSource.StdOut);
            foreach (var evt in parser.ParseLine(outputLine))
                collector.OnEvent(evt);
        }

        foreach (var evt in parser.Complete(1))
            collector.OnEvent(evt);

        var result = collector.Complete();
        result.Success.ShouldBeFalse();
        result.MachinesReady.ShouldBeEmpty();
        result.Errors.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void MultiMachine_OneSucceeds_OneFails()
    {
        var parser = new VagrantOutputParser();
        var collector = new VagrantUpCollector();

        var lines = new[]
        {
            "==> web: Importing base box...",
            "==> web: Booting VM...",
            "==> web: Machine booted and ready!",
            "==> db: Importing base box...",
            "==> db: Booting VM...",
            "==> db (error): Failed to boot",
        };

        foreach (var text in lines)
        {
            var outputLine = new OutputLine(text, OutputSource.StdOut);
            foreach (var evt in parser.ParseLine(outputLine))
                collector.OnEvent(evt);
        }

        foreach (var evt in parser.Complete(1))
            collector.OnEvent(evt);

        var result = collector.Complete();
        result.Success.ShouldBeFalse();
        result.MachinesReady.ShouldContain("web");
        result.Errors.ShouldContain(e => e.Contains("db"));
    }
}
