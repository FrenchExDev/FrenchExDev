using FrenchExDev.Net.BinaryWrapper.SourceGenerator;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.SourceGenerator.Tests;

// ── BuilderClassEmitter Additional Tests ─────────────────────────────────────

public sealed class BuilderClassEmitterAdditionalTests
{
    private static DescriptorModel MakeDescriptor()
        => new("TestNs", "TestDescriptor", "packer", "--", " ", false, null);

    private static UnifiedCommand MakeSimpleCommand(
        string name = "build",
        List<UnifiedOption>? options = null,
        List<UnifiedArgument>? arguments = null) => new()
    {
        CommandPath = $"packer.{name}",
        PathSegments = new List<string> { "packer", name },
        Name = name,
        Options = options ?? new List<UnifiedOption>(),
        Arguments = arguments ?? new List<UnifiedArgument>()
    };

    [Fact]
    public void Emit_OptionWithSinceAndUntilVersion_EmitsBothAttributesOnWithMethod()
    {
        var cmd = MakeSimpleCommand(options: new List<UnifiedOption>
        {
            new() { LongName = "color", ValueKind = "flag", ClrType = "bool", SinceVersion = "1.2.0", UntilVersion = "3.0.0" }
        });

        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.SinceVersion(\"1.2.0\")]");
        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.UntilVersion(\"3.0.0\")]");
    }

    [Fact]
    public void Emit_OptionWithOnlyUntilVersion_EmitsUntilAttributeAndGuard()
    {
        var cmd = MakeSimpleCommand(options: new List<UnifiedOption>
        {
            new() { LongName = "debug", ValueKind = "flag", ClrType = "bool", UntilVersion = "2.0.0" }
        });

        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.UntilVersion(\"2.0.0\")]");
        source.ShouldContain("VersionGuard.EnsureOptionSupported(");
        source.ShouldContain("\"debug\"");
        // since is null in the guard
        source.ShouldContain("null, new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(2, 0, 0)");
    }

    [Fact]
    public void Emit_OptionWithSinceVersionOnly_EmitsSinceAttributeAndGuard()
    {
        var cmd = MakeSimpleCommand(options: new List<UnifiedOption>
        {
            new() { LongName = "json", ValueKind = "flag", ClrType = "bool", SinceVersion = "1.5.0" }
        });

        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.SinceVersion(\"1.5.0\")]");
        source.ShouldNotContain("UntilVersion");
        source.ShouldContain("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(1, 5, 0), null");
    }

    [Fact]
    public void Emit_VersionGuard_IncludesCommandPath()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.plugins.install",
            PathSegments = new List<string> { "packer", "plugins", "install" },
            Name = "install",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "force", ValueKind = "flag", ClrType = "bool", SinceVersion = "1.8.0" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);

        // Command path should be "plugins.install" (skipping first segment)
        source.ShouldContain("\"plugins.install\"");
    }

    [Fact]
    public void Emit_MultipleValueOption_HasAsReadOnlyInInstantiation()
    {
        var cmd = MakeSimpleCommand(options: new List<UnifiedOption>
        {
            new() { LongName = "tags", ValueKind = "multiple", ClrType = "string" }
        });

        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("Tags?.AsReadOnly()");
    }

    [Fact]
    public void Emit_VariadicArgument_HasAsReadOnlyInInstantiation()
    {
        var cmd = MakeSimpleCommand(arguments: new List<UnifiedArgument>
        {
            new() { Name = "files", Position = 0, ClrType = "string", IsVariadic = true }
        });

        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("Files?.AsReadOnly()");
    }

    [Fact]
    public void Emit_NonVariadicArgument_NoAsReadOnly()
    {
        var cmd = MakeSimpleCommand(arguments: new List<UnifiedArgument>
        {
            new() { Name = "template", Position = 0, ClrType = "string", IsVariadic = false }
        });

        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldNotContain("Template?.AsReadOnly()");
        source.ShouldContain("Template = Template,");
    }

    [Fact]
    public void Emit_SingleSegmentCommand_UsesNameAsCommandPath()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer",
            PathSegments = new List<string> { "packer" },
            Name = "packer",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "version", ValueKind = "flag", ClrType = "bool", SinceVersion = "1.0.0" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);

        // When only one path segment, command path falls back to Name
        source.ShouldContain("\"packer\"");
        source.ShouldContain("VersionGuard.EnsureOptionSupported(");
    }

    [Fact]
    public void Emit_DeduplicatesOptions_BracketedNames()
    {
        var cmd = MakeSimpleCommand(options: new List<UnifiedOption>
        {
            new() { LongName = "no-tty", ValueKind = "flag", ClrType = "bool" },
            new() { LongName = "[no-]tty", ValueKind = "flag", ClrType = "bool" }
        });

        var source = BuilderClassEmitter.Emit(MakeDescriptor(), cmd);

        // Both map to "NoTty" in PascalCase. Deduplication should keep only one.
        var count = CountOccurrences(source, "WithNoTty(");
        count.ShouldBe(1);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}

// ── ClientClassEmitter Additional Tests ──────────────────────────────────────

public sealed class ClientClassEmitterAdditionalTests
{
    private static DescriptorModel MakeDescriptor()
        => new("TestNs", "TestDescriptor", "packer", "--", " ", false, null);

    [Fact]
    public void Emit_NestedGroup_PassesClientNotThis()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "packer",
            Commands = new List<UnifiedCommand>
            {
                new()
                {
                    CommandPath = "packer.plugins.install",
                    PathSegments = new List<string> { "packer", "plugins", "install" },
                    Name = "install",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                },
                new()
                {
                    CommandPath = "packer.plugins.required.add",
                    PathSegments = new List<string> { "packer", "plugins", "required", "add" },
                    Name = "add",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                }
            }
        };

        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        // Root level: group property uses "this"
        source.ShouldContain("new PackerClientPluginsGroup(this)");
        // Nested level: sub-group property uses "_client"
        source.ShouldContain("new PackerClientRequiredGroup(_client)");
    }

    [Fact]
    public void Emit_PrunesClashingLeaves_WhenLeafAndGroupShareName()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "vagrant",
            Commands = new List<UnifiedCommand>
            {
                // "box" as a leaf (from old versions that don't expand sub-commands)
                new()
                {
                    CommandPath = "vagrant.box",
                    PathSegments = new List<string> { "vagrant", "box" },
                    Name = "box",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                },
                // "box add" as a nested command (from newer versions)
                new()
                {
                    CommandPath = "vagrant.box.add",
                    PathSegments = new List<string> { "vagrant", "box", "add" },
                    Name = "add",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                }
            }
        };

        var descriptor = new DescriptorModel("TestNs", "TestDescriptor", "vagrant", "--", " ", false, null);
        var source = ClientClassEmitter.Emit(descriptor, tree);

        // The leaf "box" should be pruned; only the group should remain
        source.ShouldContain("VagrantClientBoxGroup");
        source.ShouldContain("AddAsync(");
        // There should be no "BoxAsync" method since the leaf was pruned
        source.ShouldNotContain("BoxAsync(");
    }

    [Fact]
    public void Emit_CommandWithSinceVersion_EmitsSinceAttribute()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "packer",
            Commands = new List<UnifiedCommand>
            {
                new()
                {
                    CommandPath = "packer.init",
                    PathSegments = new List<string> { "packer", "init" },
                    Name = "init",
                    Description = "Initialize a config",
                    SinceVersion = "1.7.0",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                }
            }
        };

        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.SinceVersion(\"1.7.0\")]");
        source.ShouldContain("VersionGuard.EnsureCommandSupported(");
        source.ShouldContain("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(1, 7, 0)");
    }

    [Fact]
    public void Emit_CommandWithUntilVersion_EmitsUntilAttribute()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "packer",
            Commands = new List<UnifiedCommand>
            {
                new()
                {
                    CommandPath = "packer.fix",
                    PathSegments = new List<string> { "packer", "fix" },
                    Name = "fix",
                    UntilVersion = "2.0.0",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                }
            }
        };

        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.UntilVersion(\"2.0.0\")]");
        source.ShouldContain("null, new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(2, 0, 0)");
    }

    [Fact]
    public void Emit_CommandWithBothVersionBounds_EmitsBothAttributesAndGuard()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "packer",
            Commands = new List<UnifiedCommand>
            {
                new()
                {
                    CommandPath = "packer.legacy",
                    PathSegments = new List<string> { "packer", "legacy" },
                    Name = "legacy",
                    SinceVersion = "1.0.0",
                    UntilVersion = "2.0.0",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                }
            }
        };

        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.SinceVersion(\"1.0.0\")]");
        source.ShouldContain("[global::FrenchExDev.Net.BinaryWrapper.UntilVersion(\"2.0.0\")]");
        source.ShouldContain("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(1, 0, 0), new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(2, 0, 0)");
    }

    [Fact]
    public void Emit_DescriptionWithSpecialXmlChars_IsEscaped()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "packer",
            Commands = new List<UnifiedCommand>
            {
                new()
                {
                    CommandPath = "packer.build",
                    PathSegments = new List<string> { "packer", "build" },
                    Name = "build",
                    Description = "Build <template> & validate",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                }
            }
        };

        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        source.ShouldContain("Build &lt;template&gt; &amp; validate");
    }

    [Fact]
    public void Emit_NoNamespace_OmitsNamespaceLine()
    {
        var descriptor = new DescriptorModel("", "TestDescriptor", "packer", "--", " ", false, null);
        var tree = new UnifiedCommandTree
        {
            BinaryName = "packer",
            Commands = new List<UnifiedCommand>
            {
                new()
                {
                    CommandPath = "packer.build",
                    PathSegments = new List<string> { "packer", "build" },
                    Name = "build",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                }
            }
        };

        var source = ClientClassEmitter.Emit(descriptor, tree);

        source.ShouldNotContain("namespace ");
    }

    [Fact]
    public void Emit_NestedGroupInnerClass_HasClientFieldAndDetectedVersion()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "packer",
            Commands = new List<UnifiedCommand>
            {
                new()
                {
                    CommandPath = "packer.plugins.install",
                    PathSegments = new List<string> { "packer", "plugins", "install" },
                    Name = "install",
                    Options = new List<UnifiedOption>(),
                    Arguments = new List<UnifiedArgument>()
                }
            }
        };

        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        source.ShouldContain("private readonly PackerClient _client;");
        source.ShouldContain("internal PackerClientPluginsGroup(PackerClient client) => _client = client;");
        source.ShouldContain("private global::FrenchExDev.Net.BinaryWrapper.SemanticVersion? _detectedVersion => _client._detectedVersion;");
    }

    [Fact]
    public void Emit_EmptyCommandTree_EmitsClientWithNoMethods()
    {
        var tree = new UnifiedCommandTree
        {
            BinaryName = "packer",
            Commands = new List<UnifiedCommand>()
        };

        var source = ClientClassEmitter.Emit(MakeDescriptor(), tree);

        source.ShouldContain("public partial class PackerClient");
        source.ShouldNotContain("Async(");
    }
}

// ── CommandClassEmitter Additional Tests ─────────────────────────────────────

public sealed class CommandClassEmitterAdditionalTests
{
    private static DescriptorModel MakeDescriptor(
        string flagPrefix = "--", string flagValueSeparator = " ", bool useBoolEquals = false)
        => new("TestNs", "TestDescriptor", "packer", flagPrefix, flagValueSeparator, useBoolEquals, null);

    [Fact]
    public void Emit_MultipleValueOption_WithSpaceSeparator_EmitsMultipleAdds()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "var", ValueKind = "multiple", ClrType = "string" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(flagValueSeparator: " "), cmd);

        // Space separator: emits two separate Add calls
        source.ShouldContain("__args.Add(\"--var\");");
        source.ShouldContain("__args.Add(__v?.ToString()");
    }

    [Fact]
    public void Emit_MultipleValueOption_WithEqualsSeparator_EmitsEqualsFormat()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "var", ValueKind = "multiple", ClrType = "string" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(flagValueSeparator: "="), cmd);

        source.ShouldContain("\"--var=\" + __v");
    }

    [Fact]
    public void Emit_BoolEqualsFormat_EmitsTrueFalseString()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "color", ValueKind = "flag", ClrType = "bool" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(useBoolEquals: true), cmd);

        source.ShouldContain("Color.HasValue");
        source.ShouldContain("\"--color=\" + (Color.Value ? \"true\" : \"false\")");
    }

    [Fact]
    public void Emit_BoolNotEqualsFormat_EmitsSimpleFlagAdd()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "color", ValueKind = "flag", ClrType = "bool" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(useBoolEquals: false), cmd);

        source.ShouldContain("if (Color == true)");
        source.ShouldContain("__args.Add(\"--color\");");
    }

    [Fact]
    public void Emit_OptionWithBracketedName_SanitizesFlag()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "[no-]color", ValueKind = "flag", ClrType = "bool" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        // Brackets are stripped from the flag name in ToArguments
        source.ShouldContain("\"--no-color\"");
    }

    [Fact]
    public void Emit_OptionWithSinceAndUntilVersion_EmitsBothPropertyAttributes()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "debug", ValueKind = "flag", ClrType = "bool", SinceVersion = "1.2.0", UntilVersion = "3.0.0" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        var sinceIdx = source.IndexOf("[global::FrenchExDev.Net.BinaryWrapper.SinceVersion(\"1.2.0\")]");
        var untilIdx = source.IndexOf("[global::FrenchExDev.Net.BinaryWrapper.UntilVersion(\"3.0.0\")]");
        var propIdx = source.IndexOf("public bool? Debug { get; init; }");
        sinceIdx.ShouldBeGreaterThan(-1);
        untilIdx.ShouldBeGreaterThan(sinceIdx);
        propIdx.ShouldBeGreaterThan(untilIdx);
    }

    [Fact]
    public void Emit_SingleValueStringOption_UsesIsNotNullCheck()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "output", ValueKind = "single", ClrType = "string" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("if (Output is not null)");
    }

    [Fact]
    public void Emit_SingleValueIntOption_UsesHasValueCheck()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "count", ValueKind = "single", ClrType = "int" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("if (Count.HasValue)");
    }

    [Fact]
    public void Emit_SingleValueOption_WithSpaceSeparator_EmitsTwoAdds()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "output", ValueKind = "single", ClrType = "string" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(flagValueSeparator: " "), cmd);

        source.ShouldContain("__args.Add(\"--output\");");
        source.ShouldContain("__args.Add(Output?.ToString()");
    }

    [Fact]
    public void Emit_SingleValueOption_WithEqualsSeparator_EmitsEqualsFormat()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "output", ValueKind = "single", ClrType = "string" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(flagValueSeparator: "="), cmd);

        source.ShouldContain("\"--output=\" + Output");
    }

    [Fact]
    public void Emit_VariadicArgument_EmitsForEachLoop()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>(),
            Arguments = new List<UnifiedArgument>
            {
                new() { Name = "files", Position = 0, ClrType = "string", IsVariadic = true }
            }
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("foreach (var __v in Files)");
    }

    [Fact]
    public void Emit_NonVariadicArgument_EmitsSingleAdd()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>(),
            Arguments = new List<UnifiedArgument>
            {
                new() { Name = "template", Position = 0, ClrType = "string", IsVariadic = false }
            }
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("if (Template is not null)");
        source.ShouldContain("__args.Add(Template?.ToString()");
    }

    [Fact]
    public void Emit_EmptyCommandPath_EmitsEmptyArray()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer",
            PathSegments = new List<string> { "packer" },
            Name = "packer",
            Options = new List<UnifiedOption>(),
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("Array.Empty<string>()");
    }

    [Fact]
    public void Emit_OptionWithNameContainingSpace_TruncatesAtSpace()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "s DESCRIPTION", ValueKind = "single", ClrType = "string" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        // Property name should be just "S" (PascalCase of "s")
        source.ShouldContain("S { get; init; }");
        // Flag should be truncated at space
        source.ShouldContain("\"--s\"");
        source.ShouldNotContain("DESCRIPTION");
    }

    [Fact]
    public void Emit_DescriptionWithXmlSpecialChars_Escapes()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Description = "Build <image> & deploy",
            Options = new List<UnifiedOption>(),
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        source.ShouldContain("Build &lt;image&gt; &amp; deploy");
    }

    [Fact]
    public void Emit_DeduplicatesOptions_KeepsFirst()
    {
        var cmd = new UnifiedCommand
        {
            CommandPath = "packer.build",
            PathSegments = new List<string> { "packer", "build" },
            Name = "build",
            Options = new List<UnifiedOption>
            {
                new() { LongName = "no-tty", ValueKind = "flag", ClrType = "bool", Description = "first" },
                new() { LongName = "[no-]tty", ValueKind = "flag", ClrType = "bool", Description = "second" }
            },
            Arguments = new List<UnifiedArgument>()
        };

        var source = CommandClassEmitter.Emit(MakeDescriptor(), cmd);

        // Should only have one NoTty property
        var count = 0;
        var idx = 0;
        while ((idx = source.IndexOf("NoTty { get; init; }", idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += 1;
        }
        count.ShouldBe(1);
    }
}

// ── NamingHelper Additional Tests ────────────────────────────────────────────

public sealed class NamingHelperAdditionalTests
{
    [Fact]
    public void DeduplicateOptions_RemovesDuplicatePascalCaseNames()
    {
        var options = new List<UnifiedOption>
        {
            new() { LongName = "no-tty" },
            new() { LongName = "[no-]tty" }
        };

        var result = NamingHelper.DeduplicateOptions(options);

        result.Count.ShouldBe(1);
        result[0].LongName.ShouldBe("no-tty");
    }

    [Fact]
    public void DeduplicateOptions_KeepsDistinctOptions()
    {
        var options = new List<UnifiedOption>
        {
            new() { LongName = "force" },
            new() { LongName = "color" },
            new() { LongName = "debug" }
        };

        var result = NamingHelper.DeduplicateOptions(options);

        result.Count.ShouldBe(3);
    }

    [Fact]
    public void DeduplicateOptions_SkipsOptionsWithInvalidIdentifiers()
    {
        var options = new List<UnifiedOption>
        {
            new() { LongName = "force" },
            new() { LongName = "1invalid" }  // starts with digit after PascalCase → "1invalid" → skipped
        };

        var result = NamingHelper.DeduplicateOptions(options);

        result.Count.ShouldBe(1);
        result[0].LongName.ShouldBe("force");
    }

    [Fact]
    public void DeduplicateOptions_EmptyList_ReturnsEmpty()
    {
        var result = NamingHelper.DeduplicateOptions(new List<UnifiedOption>());
        result.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("[no-]color", "NoColor")]
    [InlineData("(deprecated)", "Deprecated")]
    [InlineData("s DESCRIPTION", "S")]
    [InlineData("my-flag extra-stuff", "MyFlag")]
    public void ToPascalCase_EdgeCases(string input, string expected)
    {
        NamingHelper.ToPascalCase(input).ShouldBe(expected);
    }

    [Fact]
    public void SemanticVersionCtor_Handles1Part()
    {
        var result = NamingHelper.SemanticVersionCtor("5");
        result.ShouldBe("new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion(5, 0, 0)");
    }

    [Theory]
    [InlineData("long", "long")]
    [InlineData("double", "double")]
    [InlineData("float", "float")]
    public void NullableType_ValueTypes(string input, string expected)
    {
        NamingHelper.NullableType(input).ShouldBe(expected + "?");
    }

    [Theory]
    [InlineData("long", true)]
    [InlineData("double", true)]
    [InlineData("float", true)]
    [InlineData("uint", false)]
    public void IsValueType_AdditionalTypes(string input, bool expected)
    {
        NamingHelper.IsValueType(input).ShouldBe(expected);
    }

    [Fact]
    public void CommandClassName_SingleSegment()
    {
        var cmd = new UnifiedCommand
        {
            PathSegments = new List<string> { "packer" },
            Name = "packer"
        };
        NamingHelper.CommandClassName("packer", cmd).ShouldBe("PackerCommand");
    }

    [Fact]
    public void CommandClassName_ThreeSegments()
    {
        var cmd = new UnifiedCommand
        {
            PathSegments = new List<string> { "packer", "plugins", "install" },
            Name = "install"
        };
        NamingHelper.CommandClassName("packer", cmd).ShouldBe("PackerPluginsInstallCommand");
    }
}
