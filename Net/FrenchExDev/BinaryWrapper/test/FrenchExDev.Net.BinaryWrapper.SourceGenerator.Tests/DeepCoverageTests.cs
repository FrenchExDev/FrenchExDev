using FrenchExDev.Net.BinaryWrapper.SourceGenerator;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.SourceGenerator.Tests;

// ── CommandClassEmitter Deep Coverage ────────────────────────────────────────

public sealed class CommandClassEmitterDeepCoverageTests
{
    private static DescriptorModel MakeDescriptor(
        string flagPrefix = "--", string flagValueSeparator = " ", bool useBoolEquals = false)
        => new("TestNs", "TestDescriptor", "tool", flagPrefix, flagValueSeparator, useBoolEquals, null);

    // ── SinceVersion / UntilVersion on options: all combinations ──

    [Fact]
    public void Emit_OptionWithOnlySinceVersion_EmitsSinceAttributeOnly()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "verbose", ValueKind = "flag", ClrType = "bool", SinceVersion = "1.2.0" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("SinceVersion(\"1.2.0\")");
        source.ShouldNotContain("UntilVersion");
    }

    [Fact]
    public void Emit_OptionWithOnlyUntilVersion_EmitsUntilAttributeOnly()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "legacy", ValueKind = "flag", ClrType = "bool", UntilVersion = "2.0.0" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("UntilVersion(\"2.0.0\")");
        // There should be no SinceVersion attribute (the command-level one is absent too)
        source.ShouldNotContain("SinceVersion");
    }

    [Fact]
    public void Emit_OptionWithNoVersionBounds_NoAttributes()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "quiet", ValueKind = "flag", ClrType = "bool" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldNotContain("SinceVersion");
        source.ShouldNotContain("UntilVersion");
    }

    // ── Empty option and argument lists ──

    [Fact]
    public void Emit_NoOptions_NoArguments_EmitsEmptyToArguments()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [], Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("return __args;");
        source.ShouldNotContain("if ("); // no null checks since no properties
    }

    // ── SinceVersion + UntilVersion on command itself ──

    [Fact]
    public void Emit_CommandWithOnlySinceVersion_EmitsSinceAttributeOnly()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            SinceVersion = "1.0.0",
            Options = [], Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("SinceVersion(\"1.0.0\")");
        source.ShouldNotContain("UntilVersion");
    }

    [Fact]
    public void Emit_CommandWithOnlyUntilVersion_EmitsUntilAttributeOnly()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            UntilVersion = "3.0.0",
            Options = [], Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("UntilVersion(\"3.0.0\")");
        source.ShouldNotContain("SinceVersion");
    }

    // ── Null description on command ──

    [Fact]
    public void Emit_NullDescription_OmitsXmlDoc()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Description = null,
            Options = [], Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldNotContain("<summary>");
    }

    // ── Different CLR types for single-value options ──

    [Theory]
    [InlineData("long", "long?")]
    [InlineData("double", "double?")]
    [InlineData("float", "float?")]
    public void Emit_SingleValueOption_ValueTypes_UseHasValue(string clrType, string expectedType)
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "count", ValueKind = "single", ClrType = clrType }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain($"public {expectedType} Count {{ get; init; }}");
        source.ShouldContain("Count.HasValue");
    }

    [Fact]
    public void Emit_SingleValueOption_String_UsesIsNotNull()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "name", ValueKind = "single", ClrType = "string" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("if (Name is not null)");
    }

    // ── Unknown CLR type defaults to string ──

    [Fact]
    public void Emit_UnknownClrType_DefaultsToString()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "custom", ValueKind = "single", ClrType = "uint64" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("public string? Custom { get; init; }");
    }

    // ── Multiple CLR types for multiple-value options ──

    [Fact]
    public void Emit_MultipleValueOption_IntType()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "ports", ValueKind = "multiple", ClrType = "int" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("IReadOnlyList<int>? Ports { get; init; }");
    }

    // ── Variadic argument with non-string type ──

    [Fact]
    public void Emit_VariadicArgument_IntType()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [],
            Arguments = [new() { Name = "ids", Position = 0, ClrType = "int", IsVariadic = true }]
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("IReadOnlyList<int>? Ids { get; init; }");
        source.ShouldContain("foreach (var __v in Ids)");
    }

    // ── Multiple path segments (3+) ──

    [Fact]
    public void Emit_ThreeSegmentCommand_CorrectCommandPath()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.config.set",
            PathSegments = ["tool", "config", "set"],
            Name = "set",
            Options = [], Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("\"config\", \"set\"");
    }

    // ── Flag format branches with equals separator ──

    [Fact]
    public void Emit_SingleValueOption_EqualsFormat_NonStringType()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "count", ValueKind = "single", ClrType = "int" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(flagValueSeparator: "="), cmd);
        source.ShouldContain("\"--count=\" + Count");
    }

    // ── Deduplication: option with purely numeric name after PascalCase ──

    [Fact]
    public void Emit_OptionStartingWithDigit_IsSkipped()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options =
            [
                new() { LongName = "valid", ValueKind = "flag", ClrType = "bool" },
                new() { LongName = "123bad", ValueKind = "flag", ClrType = "bool" }
            ],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("Valid { get; init; }");
        source.ShouldNotContain("123");
    }

    // ── Option name with parentheses ──

    [Fact]
    public void Emit_OptionWithParentheses_Sanitized()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "replicas)", ValueKind = "single", ClrType = "int" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("Replicas { get; init; }");
        source.ShouldContain("\"--replicas\"");
    }
}

// ── BuilderClassEmitter Deep Coverage ────────────────────────────────────────

public sealed class BuilderClassEmitterDeepCoverageTests
{
    private static DescriptorModel MakeDescriptor()
        => new("TestNs", "TestDescriptor", "tool", "--", " ", false, null);

    // ── BuildVersionAttributes: all null/non-null combinations ──

    [Fact]
    public void Emit_NoVersionBounds_NoVersionAttributesOnWithMethod()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "force", ValueKind = "flag", ClrType = "bool" }],
            Arguments = []
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        // Between WithForce method and its body, no version attributes
        var withIdx = source.IndexOf("WithForce(");
        var bodyIdx = source.IndexOf("Force = value;");
        var section = source.Substring(withIdx, bodyIdx - withIdx);
        section.ShouldNotContain("SinceVersion");
        section.ShouldNotContain("UntilVersion");
    }

    [Fact]
    public void Emit_BothVersionBounds_BothVersionGuardArgs()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new()
            {
                LongName = "mode", ValueKind = "single", ClrType = "string",
                SinceVersion = "1.0.0", UntilVersion = "3.0.0"
            }],
            Arguments = []
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(1, 0, 0), new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(3, 0, 0)");
    }

    [Fact]
    public void Emit_OnlyUntilVersion_SinceIsNullInGuard()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new()
            {
                LongName = "old-flag", ValueKind = "flag", ClrType = "bool",
                UntilVersion = "2.0.0"
            }],
            Arguments = []
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("null, new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(2, 0, 0)");
    }

    [Fact]
    public void Emit_OnlySinceVersion_UntilIsNullInGuard()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new()
            {
                LongName = "new-flag", ValueKind = "flag", ClrType = "bool",
                SinceVersion = "2.5.0"
            }],
            Arguments = []
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(2, 5, 0), null");
    }

    // ── Single-segment path (no segments after binary name) ──

    [Fact]
    public void Emit_SingleSegmentPath_CommandPathFallsBackToName()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool", PathSegments = ["tool"], Name = "tool",
            Options = [new()
            {
                LongName = "version", ValueKind = "flag", ClrType = "bool",
                SinceVersion = "1.0.0"
            }],
            Arguments = []
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        // commandPath should be "tool" (Name fallback)
        source.ShouldContain("\"tool\", \"version\"");
    }

    // ── Multiple value option with version bounds ──

    [Fact]
    public void Emit_MultipleValueOption_WithVersionBounds_HasGuardAndAsReadOnly()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new()
            {
                LongName = "labels", ValueKind = "multiple", ClrType = "string",
                SinceVersion = "1.5.0"
            }],
            Arguments = []
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("VersionGuard.EnsureOptionSupported(");
        source.ShouldContain("Labels?.AsReadOnly()");
    }

    // ── Variadic argument: no version guard but has AsReadOnly ──

    [Fact]
    public void Emit_VariadicArgument_NoVersionGuard_HasAsReadOnly()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [],
            Arguments = [new() { Name = "paths", Position = 0, ClrType = "string", IsVariadic = true }]
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldNotContain("VersionGuard");
        source.ShouldContain("Paths?.AsReadOnly()");
    }

    // ── Non-variadic argument: no AsReadOnly ──

    [Fact]
    public void Emit_NonVariadicArgument_NoAsReadOnly()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [],
            Arguments = [new() { Name = "input", Position = 0, ClrType = "string", IsVariadic = false }]
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldNotContain("Input?.AsReadOnly()");
        source.ShouldContain("Input = Input,");
    }

    // ── Empty options + empty arguments ──

    [Fact]
    public void Emit_EmptyOptionsAndArguments_NoWithMethods()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [], Arguments = []
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldNotContain("public ToolRunCommandBuilder With");
    }
}

// ── ClientClassEmitter Deep Coverage ─────────────────────────────────────────

public sealed class ClientClassEmitterDeepCoverageTests
{
    private static DescriptorModel MakeDescriptor()
        => new("TestNs", "TestDescriptor", "tool", "--", " ", false, null);

    // ── Command with only SinceVersion ──

    [Fact]
    public void Emit_CommandWithOnlySinceVersion_EmitsSinceGuardWithNullUntil()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                new()
                {
                    CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
                    SinceVersion = "1.5.0",
                    Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);
        source.ShouldContain("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(1, 5, 0), null");
    }

    // ── Command with only UntilVersion ──

    [Fact]
    public void Emit_CommandWithOnlyUntilVersion_EmitsUntilGuardWithNullSince()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                new()
                {
                    CommandPath = "tool.old", PathSegments = ["tool", "old"], Name = "old",
                    UntilVersion = "2.0.0",
                    Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);
        source.ShouldContain("null, new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(2, 0, 0)");
    }

    // ── Null description on leaf command ──

    [Fact]
    public void Emit_NullDescription_OmitsXmlDocOnMethod()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                new()
                {
                    CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
                    Description = null,
                    Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);
        // The <summary> for the method should not appear (only the class-level ones exist)
        var methodIdx = source.IndexOf("RunAsync(");
        var beforeMethod = source.Substring(0, methodIdx);
        // There should be a <summary> for the client class itself, but not right before RunAsync
        var lastSummaryIdx = beforeMethod.LastIndexOf("<summary>");
        var clientClassIdx = beforeMethod.IndexOf("public partial class ToolClient");
        lastSummaryIdx.ShouldBeLessThan(clientClassIdx + 100);
    }

    // ── Deeply nested groups (3 levels: tool > a > b > leaf) ──

    [Fact]
    public void Emit_DeeplyNestedGroup_GeneratesNestedInnerClasses()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                new()
                {
                    CommandPath = "tool.config.advanced.set",
                    PathSegments = ["tool", "config", "advanced", "set"],
                    Name = "set",
                    Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        source.ShouldContain("ToolClientConfigGroup");
        source.ShouldContain("ToolClientAdvancedGroup");
        source.ShouldContain("SetAsync(");
    }

    // ── PruneClashingLeaves at nested level ──

    [Fact]
    public void Emit_ClashingLeafInNestedGroup_IsPruned()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                // "plugins" group has a leaf "install" AND a sub-group "install" with "add"
                new()
                {
                    CommandPath = "tool.plugins.install",
                    PathSegments = ["tool", "plugins", "install"],
                    Name = "install",
                    Options = [], Arguments = []
                },
                new()
                {
                    CommandPath = "tool.plugins.install.add",
                    PathSegments = ["tool", "plugins", "install", "add"],
                    Name = "add",
                    Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        // "install" leaf should be pruned since "install" is also a sub-group
        source.ShouldNotContain("InstallAsync(global::System.Action<ToolPluginsInstallCommandBuilder>");
        source.ShouldContain("ToolClientInstallGroup");
        source.ShouldContain("AddAsync(");
    }

    // ── Empty nested group (all leaves pruned by clashing) ──

    [Fact]
    public void Emit_AllLeavesPruned_GroupStillEmitted()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                new()
                {
                    CommandPath = "tool.box",
                    PathSegments = ["tool", "box"],
                    Name = "box",
                    Options = [], Arguments = []
                },
                new()
                {
                    CommandPath = "tool.box.add",
                    PathSegments = ["tool", "box", "add"],
                    Name = "add",
                    Options = [], Arguments = []
                },
                new()
                {
                    CommandPath = "tool.box.remove",
                    PathSegments = ["tool", "box", "remove"],
                    Name = "remove",
                    Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        source.ShouldNotContain("BoxAsync(");
        source.ShouldContain("ToolClientBoxGroup");
        source.ShouldContain("AddAsync(");
        source.ShouldContain("RemoveAsync(");
    }

    // ── Multi-segment command path for version guard ──

    [Fact]
    public void Emit_NestedCommandWithVersionGuard_UsesCorrectPath()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                new()
                {
                    CommandPath = "tool.plugins.install",
                    PathSegments = ["tool", "plugins", "install"],
                    Name = "install",
                    SinceVersion = "1.8.0",
                    Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        // The guard should use "plugins.install" as the command path
        source.ShouldContain("\"plugins.install\"");
    }
}

// ── VersionDiffer Deep Coverage ──────────────────────────────────────────────

public sealed class VersionDifferDeepCoverageTests
{
    private static CommandTreeModel MakeTree(string binaryName, params (string name, OptionModel[] opts, ArgumentModel[] args)[] commands)
    {
        var root = new CommandNodeModel
        {
            Name = binaryName,
            SubCommands = commands.Select(c => new CommandNodeModel
            {
                Name = c.name,
                Description = $"{c.name} command",
                Options = c.opts.ToList(),
                Arguments = c.args.ToList()
            }).ToList()
        };
        return new CommandTreeModel { BinaryName = binaryName, Root = root };
    }

    private static OptionModel Opt(string name) => new() { LongName = name, ValueKind = "single", ClrType = "string" };
    private static ArgumentModel Arg(string name, bool variadic = false) => new() { Name = name, ClrType = "string", IsVariadic = variadic };

    // ── FromSingle with arguments ──

    [Fact]
    public void FromSingle_WithArguments_PreservesArguments()
    {
        var tree = MakeTree("tool",
            ("run", [Opt("force")], [Arg("template"), Arg("files", variadic: true)]));

        var unified = VersionDiffer.FromSingle("1.0.0", tree);

        var run = unified.Commands[0];
        run.Arguments.Count.ShouldBe(2);
        run.Arguments[0].Name.ShouldBe("template");
        run.Arguments[0].IsVariadic.ShouldBeFalse();
        run.Arguments[1].Name.ShouldBe("files");
        run.Arguments[1].IsVariadic.ShouldBeTrue();
    }

    // ── Merge: option removed in later version ──

    [Fact]
    public void Merge_OptionRemovedInLaterVersion_HasUntilVersion()
    {
        var v1 = MakeTree("tool", ("run", [Opt("debug"), Opt("force")], []));
        var v2 = MakeTree("tool", ("run", [Opt("force")], []));

        var unified = VersionDiffer.Merge([("1.0.0", v1), ("2.0.0", v2)]);

        var debug = unified.Commands[0].Options.First(o => o.LongName == "debug");
        debug.SinceVersion.ShouldBe("1.0.0");
        debug.UntilVersion.ShouldBe("2.0.0");

        var force = unified.Commands[0].Options.First(o => o.LongName == "force");
        force.SinceVersion.ShouldBeNull(); // exists in all versions of the command
        force.UntilVersion.ShouldBeNull();
    }

    // ── Merge: three versions, option exists in v1 and v3 but not v2 ──

    [Fact]
    public void Merge_OptionGapInMiddleVersion_HasSinceVersionFromV1()
    {
        var v1 = MakeTree("tool", ("run", [Opt("debug"), Opt("force")], []));
        var v2 = MakeTree("tool", ("run", [Opt("force")], []));
        var v3 = MakeTree("tool", ("run", [Opt("debug"), Opt("force")], []));

        var unified = VersionDiffer.Merge([("1.0.0", v1), ("2.0.0", v2), ("3.0.0", v3)]);

        var debug = unified.Commands[0].Options.First(o => o.LongName == "debug");
        // debug exists in v1 and v3 but not v2 — since=1.0.0, until=null (present in latest)
        debug.SinceVersion.ShouldBe("1.0.0");
        debug.UntilVersion.ShouldBeNull();
    }

    // ── Merge: single version input ──

    [Fact]
    public void Merge_SingleVersion_HasSinceVersionEqualToVersion()
    {
        var v1 = MakeTree("tool", ("run", [Opt("force")], []));
        var unified = VersionDiffer.Merge([("1.0.0", v1)]);

        unified.Commands.Count.ShouldBe(1);
        unified.Commands[0].SinceVersion.ShouldBe("1.0.0");
        unified.Commands[0].UntilVersion.ShouldBeNull();
    }

    // ── Merge preserves latest version's arguments ──

    [Fact]
    public void Merge_UsesLatestVersionArguments()
    {
        var v1 = MakeTree("tool", ("run", [], [Arg("input")]));
        var v2 = MakeTree("tool", ("run", [], [Arg("input"), Arg("output")]));

        var unified = VersionDiffer.Merge([("1.0.0", v1), ("2.0.0", v2)]);

        unified.Commands[0].Arguments.Count.ShouldBe(2);
        unified.Commands[0].Arguments[1].Name.ShouldBe("output");
    }

    // ── Merge preserves latest version's description ──

    [Fact]
    public void Merge_UsesLatestVersionDescription()
    {
        var v1 = MakeTree("tool", ("run", [], []));
        var v2 = MakeTree("tool", ("run", [], []));
        v1.Root.SubCommands[0].Description = "Old description";
        v2.Root.SubCommands[0].Description = "New description";

        var unified = VersionDiffer.Merge([("1.0.0", v1), ("2.0.0", v2)]);

        unified.Commands[0].Description.ShouldBe("New description");
    }

    // ── Merge preserves latest version's option metadata ──

    [Fact]
    public void Merge_UsesLatestVersionOptionMetadata()
    {
        var v1Opt = new OptionModel { LongName = "output", ValueKind = "single", ClrType = "string", Description = "old desc" };
        var v2Opt = new OptionModel { LongName = "output", ValueKind = "single", ClrType = "string", Description = "new desc" };

        var v1 = MakeTree("tool", ("run", [v1Opt], []));
        var v2 = MakeTree("tool", ("run", [v2Opt], []));

        var unified = VersionDiffer.Merge([("1.0.0", v1), ("2.0.0", v2)]);

        unified.Commands[0].Options[0].Description.ShouldBe("new desc");
    }
}

// ── CommandTreeReader Deep Coverage ──────────────────────────────────────────

public sealed class CommandTreeReaderDeepCoverageTests
{
    [Fact]
    public void Parse_TreeWithNoVersion_ReturnsNullVersion()
    {
        var json = """
        {
            "binaryName": "tool",
            "root": { "name": "tool", "options": [], "arguments": [], "subCommands": [] }
        }
        """;
        var tree = CommandTreeReader.Parse(json);
        tree.ShouldNotBeNull();
        tree!.Version.ShouldBeNull();
    }

    [Fact]
    public void Parse_TreeWithDescription_ReturnsDescription()
    {
        var json = """
        {
            "binaryName": "tool",
            "description": "A test tool",
            "root": { "name": "tool", "options": [], "arguments": [], "subCommands": [] }
        }
        """;
        var tree = CommandTreeReader.Parse(json);
        tree!.Description.ShouldBe("A test tool");
    }

    [Fact]
    public void Parse_TreeWithNoDescription_ReturnsNullDescription()
    {
        var json = """
        {
            "binaryName": "tool",
            "root": { "name": "tool", "options": [], "arguments": [], "subCommands": [] }
        }
        """;
        var tree = CommandTreeReader.Parse(json);
        tree!.Description.ShouldBeNull();
    }

    [Fact]
    public void Parse_NestedSubCommands_ParsesRecursively()
    {
        var json = """
        {
            "binaryName": "tool",
            "root": {
                "name": "tool",
                "options": [],
                "arguments": [],
                "subCommands": [
                    {
                        "name": "config",
                        "options": [],
                        "arguments": [],
                        "subCommands": [
                            {
                                "name": "set",
                                "options": [{ "longName": "key", "valueKind": "single", "clrType": "string" }],
                                "arguments": [],
                                "subCommands": []
                            }
                        ]
                    }
                ]
            }
        }
        """;
        var tree = CommandTreeReader.Parse(json);
        tree!.Root.SubCommands[0].Name.ShouldBe("config");
        tree.Root.SubCommands[0].SubCommands[0].Name.ShouldBe("set");
        tree.Root.SubCommands[0].SubCommands[0].Options[0].LongName.ShouldBe("key");
    }

    [Fact]
    public void ExtractVersionFromFileName_PreReleaseVersion()
    {
        CommandTreeReader.ExtractVersionFromFileName("tool-1.0.0-beta.json", "tool")
            .ShouldBe("1.0.0-beta");
    }
}

// ── NamingHelper Deep Coverage ───────────────────────────────────────────────

public sealed class NamingHelperDeepCoverageTests
{
    [Fact]
    public void ToPascalCase_NullInput_ReturnsNull()
    {
        NamingHelper.ToPascalCase(null!).ShouldBeNull();
    }

    [Fact]
    public void ToPascalCase_MultipleSeparators_HandlesAll()
    {
        // Mix of dashes, underscores, and dots
        NamingHelper.ToPascalCase("my-flag_name.extra").ShouldBe("MyFlagNameExtra");
    }

    [Fact]
    public void ToPascalCase_ConsecutiveSeparators_SkipsEmpty()
    {
        NamingHelper.ToPascalCase("a--b__c").ShouldBe("ABC");
    }

    [Fact]
    public void DeduplicateOptions_ThreeDuplicates_KeepsFirst()
    {
        var options = new List<UnifiedOption>
        {
            new() { LongName = "no-tty" },
            new() { LongName = "[no-]tty" },
            new() { LongName = "no_tty" }  // underscore variant also maps to NoTty
        };

        var result = NamingHelper.DeduplicateOptions(options);
        result.Count.ShouldBe(1);
        result[0].LongName.ShouldBe("no-tty");
    }

    [Fact]
    public void SemanticVersionCtor_EmptyString_HandlesGracefully()
    {
        // Empty string splits to [""] which parses to "0" defaults
        var result = NamingHelper.SemanticVersionCtor("");
        result.ShouldContain("SemanticVersion(");
    }

    [Fact]
    public void MapClrType_AllSupportedTypes()
    {
        NamingHelper.MapClrType("bool").ShouldBe("bool");
        NamingHelper.MapClrType("int").ShouldBe("int");
        NamingHelper.MapClrType("long").ShouldBe("long");
        NamingHelper.MapClrType("double").ShouldBe("double");
        NamingHelper.MapClrType("float").ShouldBe("float");
        NamingHelper.MapClrType("string").ShouldBe("string");
        NamingHelper.MapClrType("anything_else").ShouldBe("string");
    }

    [Fact]
    public void NullableType_AllTypes()
    {
        NamingHelper.NullableType("bool").ShouldBe("bool?");
        NamingHelper.NullableType("int").ShouldBe("int?");
        NamingHelper.NullableType("long").ShouldBe("long?");
        NamingHelper.NullableType("double").ShouldBe("double?");
        NamingHelper.NullableType("float").ShouldBe("float?");
        NamingHelper.NullableType("string").ShouldBe("string?");
        NamingHelper.NullableType("custom").ShouldBe("custom?");
    }

    [Fact]
    public void IsValueType_AllTypes()
    {
        NamingHelper.IsValueType("bool").ShouldBeTrue();
        NamingHelper.IsValueType("int").ShouldBeTrue();
        NamingHelper.IsValueType("long").ShouldBeTrue();
        NamingHelper.IsValueType("double").ShouldBeTrue();
        NamingHelper.IsValueType("float").ShouldBeTrue();
        NamingHelper.IsValueType("string").ShouldBeFalse();
        NamingHelper.IsValueType("custom").ShouldBeFalse();
    }

    [Fact]
    public void EscapeString_NoSpecialChars_ReturnsUnchanged()
    {
        NamingHelper.EscapeString("simple").ShouldBe("simple");
    }

    [Fact]
    public void EscapeString_BackslashAndQuote_BothEscaped()
    {
        NamingHelper.EscapeString("a\\b\"c").ShouldBe("a\\\\b\\\"c");
    }
}
