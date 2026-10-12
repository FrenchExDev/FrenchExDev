using FrenchExDev.Net.BinaryWrapper.SourceGenerator;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.SourceGenerator.Tests;

// ── NamingHelper Final Coverage ──────────────────────────────────────────────

public sealed class NamingHelperFinalCoverageTests
{
    // ToPascalCase: name that becomes empty after bracket/space stripping
    [Fact]
    public void ToPascalCase_OnlyBrackets_ReturnsEmpty()
    {
        // "[]-[]" strips brackets => "--", splits by '-' => empty parts only
        NamingHelper.ToPascalCase("[]").ShouldBe("");
    }

    [Fact]
    public void ToPascalCase_SpaceInMiddle_TruncatesAtSpace()
    {
        // "flag extra" has space at index 4, so spaceIdx > 0 is true => truncated to "flag"
        NamingHelper.ToPascalCase("flag extra").ShouldBe("Flag");
    }

    [Fact]
    public void ToPascalCase_NameWithBracketsAndSpace_SanitizedAndTruncated()
    {
        // "[no-]color stuff" => strip brackets => "no-color stuff" => space at 8 > 0 => "no-color" => split => "NoColor"
        NamingHelper.ToPascalCase("[no-]color stuff").ShouldBe("NoColor");
    }

    [Fact]
    public void ToPascalCase_SingleCharParts_HandlesCorrectly()
    {
        // Single char part: part.Length == 1, so `if (part.Length > 1)` is false
        NamingHelper.ToPascalCase("a-b-c").ShouldBe("ABC");
    }

    // DeduplicateOptions: option whose PascalCase is empty (after bracket/space stripping)
    [Fact]
    public void DeduplicateOptions_EmptyPascalCase_IsSkipped()
    {
        var options = new List<UnifiedOption>
        {
            new() { LongName = "[]" },  // PascalCase => ""
            new() { LongName = "valid" }
        };

        var result = NamingHelper.DeduplicateOptions(options);
        result.Count.ShouldBe(1);
        result[0].LongName.ShouldBe("valid");
    }

    // SemanticVersionCtor: version with more than 3 parts (extra parts ignored)
    [Fact]
    public void SemanticVersionCtor_FourParts_UsesFirstThree()
    {
        var result = NamingHelper.SemanticVersionCtor("1.2.3.4");
        result.ShouldBe("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(1, 2, 3)");
    }

    // MapClrType: verify the default arm is exercised for non-standard types
    [Theory]
    [InlineData("uint", "string")]
    [InlineData("byte", "string")]
    [InlineData("decimal", "string")]
    [InlineData("", "string")]
    public void MapClrType_UnknownTypes_DefaultToString(string input, string expected)
    {
        NamingHelper.MapClrType(input).ShouldBe(expected);
    }

    // NullableType: verify the default arm for non-standard types
    [Theory]
    [InlineData("uint", "uint?")]
    [InlineData("byte", "byte?")]
    public void NullableType_UnknownTypes_StillAppendQuestionMark(string input, string expected)
    {
        NamingHelper.NullableType(input).ShouldBe(expected);
    }

    // IsValueType: verify false for non-standard types
    [Theory]
    [InlineData("byte")]
    [InlineData("decimal")]
    [InlineData("")]
    public void IsValueType_NonStandardTypes_ReturnFalse(string input)
    {
        NamingHelper.IsValueType(input).ShouldBeFalse();
    }
}

// ── CommandTreeReader Final Coverage ──────────────────────────────────────────

public sealed class CommandTreeReaderFinalCoverageTests
{
    // Parse: valid JSON but empty root (no sub-commands)
    [Fact]
    public void Parse_EmptyRoot_ReturnsTreeWithEmptySubCommands()
    {
        var json = """
        {
            "binaryName": "tool",
            "root": {
                "name": "tool",
                "options": [],
                "arguments": [],
                "subCommands": []
            }
        }
        """;
        var tree = CommandTreeReader.Parse(json);
        tree.ShouldNotBeNull();
        tree!.Root.SubCommands.Count.ShouldBe(0);
    }

    // Parse: JSON with nested sub-commands at 3 levels
    [Fact]
    public void Parse_DeepNesting_ParsesAllLevels()
    {
        var json = """
        {
            "binaryName": "tool",
            "root": {
                "name": "tool", "options": [], "arguments": [],
                "subCommands": [{
                    "name": "a", "options": [], "arguments": [],
                    "subCommands": [{
                        "name": "b", "options": [], "arguments": [],
                        "subCommands": [{
                            "name": "c", "options": [], "arguments": [],
                            "subCommands": []
                        }]
                    }]
                }]
            }
        }
        """;
        var tree = CommandTreeReader.Parse(json);
        tree!.Root.SubCommands[0].SubCommands[0].SubCommands[0].Name.ShouldBe("c");
    }

    // Parse: malformed JSON (missing closing brace)
    [Fact]
    public void Parse_TruncatedJson_ReturnsNull()
    {
        CommandTreeReader.Parse("{\"binaryName\": \"t").ShouldBeNull();
    }

    // ExtractVersionFromFileName: binary name with dash in it
    [Fact]
    public void ExtractVersionFromFileName_BinaryNameWithDash()
    {
        CommandTreeReader.ExtractVersionFromFileName("my-tool-1.2.3.json", "my-tool")
            .ShouldBe("1.2.3");
    }

    // ExtractVersionFromFileName: empty version part
    [Fact]
    public void ExtractVersionFromFileName_EmptyVersionPart()
    {
        // "tool-.json" => version would be empty string
        CommandTreeReader.ExtractVersionFromFileName("tool-.json", "tool")
            .ShouldBe("");
    }

    // Parse: option with all fields populated
    [Fact]
    public void Parse_OptionAllFields_ParsesCorrectly()
    {
        var json = """
        {
            "binaryName": "tool",
            "root": {
                "name": "tool", "options": [], "arguments": [],
                "subCommands": [{
                    "name": "run",
                    "options": [{
                        "longName": "output",
                        "shortName": "o",
                        "description": "Output path",
                        "valueKind": "single",
                        "clrType": "string",
                        "defaultValue": "/tmp",
                        "isRequired": true
                    }],
                    "arguments": [],
                    "subCommands": []
                }]
            }
        }
        """;
        var tree = CommandTreeReader.Parse(json)!;
        var opt = tree.Root.SubCommands[0].Options[0];
        opt.LongName.ShouldBe("output");
        opt.ShortName.ShouldBe("o");
        opt.Description.ShouldBe("Output path");
        opt.DefaultValue.ShouldBe("/tmp");
        opt.IsRequired.ShouldBeTrue();
    }

    // Parse: argument with all fields populated
    [Fact]
    public void Parse_ArgumentAllFields_ParsesCorrectly()
    {
        var json = """
        {
            "binaryName": "tool",
            "root": {
                "name": "tool", "options": [], "arguments": [],
                "subCommands": [{
                    "name": "run",
                    "options": [],
                    "arguments": [{
                        "name": "files",
                        "position": 1,
                        "description": "Input files",
                        "clrType": "string",
                        "isRequired": false,
                        "isVariadic": true,
                        "defaultValue": "*.txt"
                    }],
                    "subCommands": []
                }]
            }
        }
        """;
        var tree = CommandTreeReader.Parse(json)!;
        var arg = tree.Root.SubCommands[0].Arguments[0];
        arg.Name.ShouldBe("files");
        arg.Position.ShouldBe(1);
        arg.Description.ShouldBe("Input files");
        arg.IsRequired.ShouldBeFalse();
        arg.IsVariadic.ShouldBeTrue();
        arg.DefaultValue.ShouldBe("*.txt");
    }
}

// ── VersionDiffer Final Coverage ─────────────────────────────────────────────

public sealed class VersionDifferFinalCoverageTests
{
    private static CommandTreeModel MakeTree(string binaryName,
        params (string name, OptionModel[] opts, ArgumentModel[] args)[] commands)
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

    private static OptionModel Opt(string name) =>
        new() { LongName = name, ValueKind = "single", ClrType = "string" };

    private static ArgumentModel Arg(string name, bool variadic = false) =>
        new() { Name = name, ClrType = "string", IsVariadic = variadic };

    // NextVersion: when the version is not found in the list, NextVersion returns null
    [Fact]
    public void Merge_CommandLastVersionIsLastGlobal_UntilIsNull()
    {
        var v1 = MakeTree("tool", ("run", [Opt("a")], []));
        var v2 = MakeTree("tool", ("run", [Opt("a")], []));

        var unified = VersionDiffer.Merge([("1.0.0", v1), ("2.0.0", v2)]);

        // Command present in the last global version => untilVersion is null
        unified.Commands[0].UntilVersion.ShouldBeNull();
    }

    // Merge with deeply nested sub-commands (tests CollectLeafCommands recursion)
    [Fact]
    public void Merge_NestedSubCommands_CollectsLeaves()
    {
        var root1 = new CommandNodeModel
        {
            Name = "tool",
            SubCommands =
            [
                new CommandNodeModel
                {
                    Name = "config",
                    SubCommands =
                    [
                        new CommandNodeModel
                        {
                            Name = "set",
                            Options = [new OptionModel { LongName = "key", ValueKind = "single", ClrType = "string" }],
                            SubCommands = []
                        }
                    ]
                }
            ]
        };
        var tree1 = new CommandTreeModel { BinaryName = "tool", Root = root1 };

        var unified = VersionDiffer.Merge([("1.0.0", tree1)]);
        unified.Commands.Count.ShouldBe(1);
        unified.Commands[0].PathSegments.ShouldBe(new List<string> { "tool", "config", "set" });
    }

    // Merge: option exists in v1 and v2 but not v3 (last version) => has UntilVersion
    [Fact]
    public void Merge_OptionRemovedInLastVersion_HasUntilVersion()
    {
        var v1 = MakeTree("tool", ("run", [Opt("debug"), Opt("force")], []));
        var v2 = MakeTree("tool", ("run", [Opt("debug"), Opt("force")], []));
        var v3 = MakeTree("tool", ("run", [Opt("force")], []));

        var unified = VersionDiffer.Merge([("1.0.0", v1), ("2.0.0", v2), ("3.0.0", v3)]);

        var debug = unified.Commands[0].Options.First(o => o.LongName == "debug");
        debug.SinceVersion.ShouldBe("1.0.0");
        debug.UntilVersion.ShouldBe("3.0.0"); // NextVersion("2.0.0", ["1.0.0","2.0.0","3.0.0"]) = "3.0.0"
    }

    // FromSingle: tree with arguments and options
    [Fact]
    public void FromSingle_WithOptionsAndArguments_BothPreserved()
    {
        var tree = MakeTree("tool",
            ("run",
                [new OptionModel { LongName = "verbose", ShortName = "v", ValueKind = "flag", ClrType = "bool", Description = "Verbose" }],
                [new ArgumentModel { Name = "input", Position = 0, ClrType = "string", IsRequired = true, IsVariadic = false, Description = "Input file" }]));

        var unified = VersionDiffer.FromSingle("1.0.0", tree);
        var cmd = unified.Commands[0];

        cmd.Options.Count.ShouldBe(1);
        cmd.Options[0].ShortName.ShouldBe("v");
        cmd.Options[0].Description.ShouldBe("Verbose");
        cmd.Options[0].ClrType.ShouldBe("bool");

        cmd.Arguments.Count.ShouldBe(1);
        cmd.Arguments[0].Description.ShouldBe("Input file");
        cmd.Arguments[0].IsRequired.ShouldBeTrue();
    }

    // FromSingle: option with default value is preserved
    [Fact]
    public void FromSingle_OptionDefaultValue_Preserved()
    {
        var tree = MakeTree("tool",
            ("run",
                [new OptionModel { LongName = "count", ValueKind = "single", ClrType = "int", DefaultValue = "5", IsRequired = false }],
                []));

        var unified = VersionDiffer.FromSingle("1.0.0", tree);
        unified.Commands[0].Options[0].DefaultValue.ShouldBe("5");
        unified.Commands[0].Options[0].IsRequired.ShouldBeFalse();
    }

    // Merge: command only in middle version (removed in last)
    [Fact]
    public void Merge_CommandOnlyInMiddleVersion_HasSinceAndUntil()
    {
        var v1 = MakeTree("tool", ("run", [], []));
        var v2 = MakeTree("tool", ("run", [], []), ("temp", [], []));
        var v3 = MakeTree("tool", ("run", [], []));

        var unified = VersionDiffer.Merge([("1.0.0", v1), ("2.0.0", v2), ("3.0.0", v3)]);

        var temp = unified.Commands.First(c => c.Name == "temp");
        temp.SinceVersion.ShouldBe("2.0.0");
        temp.UntilVersion.ShouldBe("3.0.0");
    }

    // Merge: option existsInAll is true => since/until are null on option
    [Fact]
    public void Merge_OptionExistsInAllVersions_NullVersionBounds()
    {
        var v1 = MakeTree("tool", ("run", [Opt("force")], []));
        var v2 = MakeTree("tool", ("run", [Opt("force")], []));
        var v3 = MakeTree("tool", ("run", [Opt("force")], []));

        var unified = VersionDiffer.Merge([("1.0.0", v1), ("2.0.0", v2), ("3.0.0", v3)]);

        var force = unified.Commands[0].Options[0];
        force.SinceVersion.ShouldBeNull();
        force.UntilVersion.ShouldBeNull();
    }

    // FromSingle: tree with no sub-commands (root is itself a leaf)
    [Fact]
    public void FromSingle_RootIsLeaf_CollectsRootAsCommand()
    {
        var tree = new CommandTreeModel
        {
            BinaryName = "tool",
            Root = new CommandNodeModel
            {
                Name = "tool",
                Options = [new OptionModel { LongName = "version", ValueKind = "flag", ClrType = "bool" }],
                Arguments = [],
                SubCommands = []
            }
        };

        var unified = VersionDiffer.FromSingle("1.0.0", tree);
        unified.Commands.Count.ShouldBe(1);
        unified.Commands[0].PathSegments.ShouldBe(new List<string> { "tool" });
    }

    // Merge: argument DefaultValue preserved from latest version
    [Fact]
    public void Merge_ArgumentDefaultValue_FromLatestVersion()
    {
        var v1 = MakeTree("tool", ("run", [], [new ArgumentModel { Name = "output", ClrType = "string", DefaultValue = "old" }]));
        var v2 = MakeTree("tool", ("run", [], [new ArgumentModel { Name = "output", ClrType = "string", DefaultValue = "new" }]));

        var unified = VersionDiffer.Merge([("1.0.0", v1), ("2.0.0", v2)]);
        unified.Commands[0].Arguments[0].DefaultValue.ShouldBe("new");
    }
}

// ── CommandClassEmitter Final Coverage ────────────────────────────────────────

public sealed class CommandClassEmitterFinalCoverageTests
{
    private static DescriptorModel MakeDescriptor(
        string ns = "TestNs", string flagPrefix = "--",
        string flagValueSeparator = " ", bool useBoolEquals = false)
        => new(ns, "TestDescriptor", "tool", flagPrefix, flagValueSeparator, useBoolEquals, null);

    // Multiple path segments (4 deep) verifying command path in generated code
    [Fact]
    public void Emit_FourSegmentCommand_AllSegmentsInPath()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.a.b.c",
            PathSegments = ["tool", "a", "b", "c"],
            Name = "c",
            Options = [], Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("\"a\", \"b\", \"c\"");
    }

    // Single-value option with equals separator and non-string type
    [Fact]
    public void Emit_LongValueOption_EqualsSeparator()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "weight", ValueKind = "single", ClrType = "long" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(flagValueSeparator: "="), cmd);
        source.ShouldContain("public long? Weight { get; init; }");
        source.ShouldContain("Weight.HasValue");
        source.ShouldContain("\"--weight=\" + Weight");
    }

    // Multiple-value option with non-string type and equals separator
    [Fact]
    public void Emit_MultipleDoubleOption_EqualsSeparator()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "weights", ValueKind = "multiple", ClrType = "double" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(flagValueSeparator: "="), cmd);
        source.ShouldContain("IReadOnlyList<double>? Weights { get; init; }");
        source.ShouldContain("\"--weights=\" + __v");
    }

    // Non-variadic argument with non-string type
    [Fact]
    public void Emit_NonVariadicIntArgument_UsesNullableInt()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [],
            Arguments = [new() { Name = "count", Position = 0, ClrType = "int", IsVariadic = false }]
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("public int? Count { get; init; }");
        source.ShouldContain("if (Count is not null)");
    }

    // UseBoolEqualsFormat false + flag value kind (covers the `else` in flag handling)
    [Fact]
    public void Emit_FlagWithFalseValue_NotSerialized()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "verbose", ValueKind = "flag", ClrType = "bool" }],
            Arguments = []
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(useBoolEquals: false), cmd);
        // With useBoolEquals false, only emits when value is true
        source.ShouldContain("if (Verbose == true)");
        source.ShouldContain("__args.Add(\"--verbose\");");
    }

    // Multiple options + arguments together in one command
    [Fact]
    public void Emit_MixedOptionsAndArguments_AllEmitted()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.deploy", PathSegments = ["tool", "deploy"], Name = "deploy",
            Description = "Deploy app",
            Options =
            [
                new() { LongName = "force", ValueKind = "flag", ClrType = "bool" },
                new() { LongName = "tag", ValueKind = "multiple", ClrType = "string" },
                new() { LongName = "replicas", ValueKind = "single", ClrType = "int" }
            ],
            Arguments =
            [
                new() { Name = "target", Position = 0, ClrType = "string", IsVariadic = false },
                new() { Name = "extra-args", Position = 1, ClrType = "string", IsVariadic = true }
            ]
        };
        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("Force { get; init; }");
        source.ShouldContain("IReadOnlyList<string>? Tag { get; init; }");
        source.ShouldContain("int? Replicas { get; init; }");
        source.ShouldContain("string? Target { get; init; }");
        source.ShouldContain("IReadOnlyList<string>? ExtraArgs { get; init; }");
        source.ShouldContain("foreach (var __v in Tag)");
        source.ShouldContain("foreach (var __v in ExtraArgs)");
    }
}

// ── BuilderClassEmitter Final Coverage ───────────────────────────────────────

public sealed class BuilderClassEmitterFinalCoverageTests
{
    private static DescriptorModel MakeDescriptor(string ns = "TestNs")
        => new(ns, "TestDescriptor", "tool", "--", " ", false, null);

    // Multiple value option with int type (covers non-string collection type mapping)
    [Fact]
    public void Emit_MultipleIntOption_HasListIntType()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new() { LongName = "ports", ValueKind = "multiple", ClrType = "int" }],
            Arguments = []
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("global::System.Collections.Generic.List<int>?");
        source.ShouldContain("Ports?.AsReadOnly()");
    }

    // Variadic argument with int type
    [Fact]
    public void Emit_VariadicIntArgument_HasListIntType()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [],
            Arguments = [new() { Name = "ids", Position = 0, ClrType = "int", IsVariadic = true }]
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("global::System.Collections.Generic.List<int>?");
        source.ShouldContain("Ids?.AsReadOnly()");
    }

    // Non-variadic argument with non-string type (no AsReadOnly, not collection)
    [Fact]
    public void Emit_NonVariadicIntArgument_NullableIntNoAsReadOnly()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [],
            Arguments = [new() { Name = "count", Position = 0, ClrType = "int", IsVariadic = false }]
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("protected int? Count { get; private set; }");
        source.ShouldNotContain("Count?.AsReadOnly()");
    }

    // Empty namespace
    [Fact]
    public void Emit_EmptyNamespace_OmitsNamespaceInOutput()
    {
        var descriptor = new DescriptorModel("", "TestDescriptor", "tool", "--", " ", false, null);
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [], Arguments = []
        };
        var source = BuilderClassEmitter.Emit(descriptor, cmd);
        source.ShouldNotContain("namespace ");
    }

    // Multiple path segments for commandPath in version guard
    [Fact]
    public void Emit_ThreeSegmentPath_VersionGuardUsesCorrectPath()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.config.set",
            PathSegments = ["tool", "config", "set"],
            Name = "set",
            Options = [new() { LongName = "key", ValueKind = "single", ClrType = "string", SinceVersion = "1.5.0" }],
            Arguments = []
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);
        source.ShouldContain("\"config.set\"");
        source.ShouldContain("\"key\"");
    }

    // Both since AND until on option: both SemanticVersion ctors in guard
    [Fact]
    public void Emit_BothVersionBoundsOnOption_BothCtorsInGuard()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "tool.run", PathSegments = ["tool", "run"], Name = "run",
            Options = [new()
            {
                LongName = "experimental", ValueKind = "flag", ClrType = "bool",
                SinceVersion = "1.2.0", UntilVersion = "3.5.0"
            }],
            Arguments = []
        };
        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);

        // Both version attributes
        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.SinceVersion(\"1.2.0\")]");
        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.UntilVersion(\"3.5.0\")]");

        // Guard with both non-null
        source.ShouldContain("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(1, 2, 0), new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(3, 5, 0)");
    }
}

// ── ClientClassEmitter Final Coverage ────────────────────────────────────────

public sealed class ClientClassEmitterFinalCoverageTests
{
    private static DescriptorModel MakeDescriptor(string ns = "TestNs")
        => new(ns, "TestDescriptor", "tool", "--", " ", false, null);

    // Command with single path segment (root-level command, pathSegments.Count == 1)
    [Fact]
    public void Emit_SingleSegmentCommand_UsesNameAsCommandPath()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                new()
                {
                    CommandPath = "tool",
                    PathSegments = ["tool"],
                    Name = "tool",
                    SinceVersion = "1.0.0",
                    Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);
        // commandPath should use the Name since there's only one segment
        source.ShouldContain("\"tool\"");
        source.ShouldContain("VersionGuard.EnsureCommandSupported(");
    }

    // Multiple sub-groups at same level (tests iteration over SubGroups)
    [Fact]
    public void Emit_MultipleSiblingGroups_AllEmitted()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                new()
                {
                    CommandPath = "tool.alpha.run",
                    PathSegments = ["tool", "alpha", "run"],
                    Name = "run", Options = [], Arguments = []
                },
                new()
                {
                    CommandPath = "tool.beta.build",
                    PathSegments = ["tool", "beta", "build"],
                    Name = "build", Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);
        source.ShouldContain("ToolClientAlphaGroup");
        source.ShouldContain("ToolClientBetaGroup");
        source.ShouldContain("RunAsync(");
        source.ShouldContain("BuildAsync(");
    }

    // PruneClashingLeaves: recursive pruning at nested level
    [Fact]
    public void Emit_NestedClashingLeaf_IsPruned()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                // "group > sub" as both leaf and parent of "sub > child"
                new()
                {
                    CommandPath = "tool.group.sub",
                    PathSegments = ["tool", "group", "sub"],
                    Name = "sub",
                    Options = [], Arguments = []
                },
                new()
                {
                    CommandPath = "tool.group.sub.child",
                    PathSegments = ["tool", "group", "sub", "child"],
                    Name = "child",
                    Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);
        source.ShouldNotContain("SubAsync(global::System.Action<ToolGroupSubCommandBuilder>");
        source.ShouldContain("ChildAsync(");
    }

    // Command at root level with both since and until
    [Fact]
    public void Emit_RootCommandWithBothVersionBounds_EmitsFullGuard()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "tool",
            Commands =
            [
                new()
                {
                    CommandPath = "tool.legacy",
                    PathSegments = ["tool", "legacy"],
                    Name = "legacy",
                    SinceVersion = "1.0.0",
                    UntilVersion = "5.0.0",
                    Options = [], Arguments = []
                }
            ]
        };
        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);
        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.SinceVersion(\"1.0.0\")]");
        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.UntilVersion(\"5.0.0\")]");
        source.ShouldContain("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(1, 0, 0), new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(5, 0, 0)");
    }
}

// ── DescriptorModel Tests ────────────────────────────────────────────────────

public sealed class DescriptorModelFinalCoverageTests
{
    [Fact]
    public void Constructor_AllProperties_SetCorrectly()
    {
        var model = new DescriptorModel("MyNs", "MyClass", "docker", "-", "=", true, null);
        model.Namespace.ShouldBe("MyNs");
        model.ClassName.ShouldBe("MyClass");
        model.BinaryName.ShouldBe("docker");
        model.FlagPrefix.ShouldBe("-");
        model.FlagValueSeparator.ShouldBe("=");
        model.UseBoolEqualsFormat.ShouldBeTrue();
        model.Location.ShouldBeNull();
    }

    [Fact]
    public void Constructor_EmptyNamespace_SetsEmpty()
    {
        var model = new DescriptorModel("", "MyClass", "tool", "--", " ", false, null);
        model.Namespace.ShouldBe("");
    }
}

// ── Unified Models Tests ─────────────────────────────────────────────────────

public sealed class UnifiedModelsFinalCoverageTests
{
    [Fact]
    public void UnifiedCommandTree_DefaultValues()
    {
        var tree = new UnifiedCommandTree();
        tree.BinaryName.ShouldBe("");
        tree.Commands.ShouldNotBeNull();
        tree.Commands.Count.ShouldBe(0);
    }

    [Fact]
    public void UnifiedCommand_DefaultValues()
    {
        var cmd = new UnifiedCommand();
        cmd.CommandPath.ShouldBe("");
        cmd.Name.ShouldBe("");
        cmd.Description.ShouldBeNull();
        cmd.SinceVersion.ShouldBeNull();
        cmd.UntilVersion.ShouldBeNull();
        cmd.Options.ShouldNotBeNull();
        cmd.Arguments.ShouldNotBeNull();
        cmd.PathSegments.ShouldNotBeNull();
    }

    [Fact]
    public void UnifiedOption_DefaultValues()
    {
        var opt = new UnifiedOption();
        opt.LongName.ShouldBe("");
        opt.ShortName.ShouldBeNull();
        opt.Description.ShouldBeNull();
        opt.ValueKind.ShouldBe("single");
        opt.ClrType.ShouldBe("string");
        opt.DefaultValue.ShouldBeNull();
        opt.IsRequired.ShouldBeFalse();
        opt.SinceVersion.ShouldBeNull();
        opt.UntilVersion.ShouldBeNull();
    }

    [Fact]
    public void UnifiedArgument_DefaultValues()
    {
        var arg = new UnifiedArgument();
        arg.Name.ShouldBe("");
        arg.Position.ShouldBe(0);
        arg.Description.ShouldBeNull();
        arg.ClrType.ShouldBe("string");
        arg.IsRequired.ShouldBeTrue();
        arg.IsVariadic.ShouldBeFalse();
        arg.DefaultValue.ShouldBeNull();
    }

    [Fact]
    public void CommandTreeModel_DefaultValues()
    {
        var model = new CommandTreeModel();
        model.BinaryName.ShouldBe("");
        model.Version.ShouldBeNull();
        model.Description.ShouldBeNull();
        model.Root.ShouldNotBeNull();
    }

    [Fact]
    public void CommandNodeModel_DefaultValues()
    {
        var node = new CommandNodeModel();
        node.Name.ShouldBe("");
        node.Description.ShouldBeNull();
        node.Options.ShouldNotBeNull();
        node.Arguments.ShouldNotBeNull();
        node.SubCommands.ShouldNotBeNull();
    }

    [Fact]
    public void OptionModel_DefaultValues()
    {
        var opt = new OptionModel();
        opt.LongName.ShouldBe("");
        opt.ShortName.ShouldBeNull();
        opt.Description.ShouldBeNull();
        opt.ValueKind.ShouldBe("single");
        opt.ClrType.ShouldBe("string");
        opt.DefaultValue.ShouldBeNull();
        opt.IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void ArgumentModel_DefaultValues()
    {
        var arg = new ArgumentModel();
        arg.Name.ShouldBe("");
        arg.Position.ShouldBe(0);
        arg.Description.ShouldBeNull();
        arg.ClrType.ShouldBe("string");
        arg.IsRequired.ShouldBeTrue();
        arg.IsVariadic.ShouldBeFalse();
        arg.DefaultValue.ShouldBeNull();
    }
}

// ── Diagnostics Final Coverage ───────────────────────────────────────────────

public sealed class DiagnosticsFinalCoverageTests
{
    [Fact]
    public void BW001_IsEnabledByDefault()
    {
        Diagnostics.BW001_NoFilesFound.IsEnabledByDefault.ShouldBeTrue();
    }

    [Fact]
    public void BW002_IsEnabledByDefault()
    {
        Diagnostics.BW002_JsonParseError.IsEnabledByDefault.ShouldBeTrue();
    }

    [Fact]
    public void BW004_IsEnabledByDefault()
    {
        Diagnostics.BW004_NonPartialClass.IsEnabledByDefault.ShouldBeTrue();
    }

    [Fact]
    public void BW001_CategoryIsBinaryWrapper()
    {
        Diagnostics.BW001_NoFilesFound.Category.ShouldBe("BinaryWrapper");
    }

    [Fact]
    public void BW002_CategoryIsBinaryWrapper()
    {
        Diagnostics.BW002_JsonParseError.Category.ShouldBe("BinaryWrapper");
    }

    [Fact]
    public void BW004_CategoryIsBinaryWrapper()
    {
        Diagnostics.BW004_NonPartialClass.Category.ShouldBe("BinaryWrapper");
    }

    [Fact]
    public void BW001_HasTitle()
    {
        Diagnostics.BW001_NoFilesFound.Title.ToString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BW002_HasTitle()
    {
        Diagnostics.BW002_JsonParseError.Title.ToString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BW004_HasTitle()
    {
        Diagnostics.BW004_NonPartialClass.Title.ToString().ShouldNotBeNullOrWhiteSpace();
    }
}
