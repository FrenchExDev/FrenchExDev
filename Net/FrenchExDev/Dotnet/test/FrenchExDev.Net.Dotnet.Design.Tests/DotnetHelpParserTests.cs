using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.Dotnet.Design;
using Shouldly;

namespace FrenchExDev.Net.Dotnet.Design.Tests;

public sealed class DotnetHelpParserTests
{
    private readonly DotnetHelpParser _parser = new();

    private CommandNode Fixture(string name, string command) => _parser.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".help.txt")), command)!;

    [Fact]
    public void Root_CollectsSdkAndBundledCommandsAndPipeAliases()
    {
        var root = Fixture("root.fr", "dotnet");
        root.SubCommands.Select(c => c.Name).ShouldContain("build");
        root.SubCommands.Select(c => c.Name).ShouldContain("tool");
        root.SubCommands.Select(c => c.Name).ShouldContain("dev-certs");
        root.SubCommands.Select(c => c.Name).ShouldNotContain("help");
        var diagnostics = root.Options.Single(o => o.LongName == "diagnostics");
        diagnostics.ShortName.ShouldBe("d");
        diagnostics.ValueKind.ShouldBe(OptionValueKind.Flag);
        root.Options.Single(o => o.LongName == "additionalprobingpath").ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Build_ParsesSpaceContainingArgumentAndUsageArity()
    {
        var build = Fixture("build.fr", "build");
        var project = build.Arguments.ShouldHaveSingleItem();
        project.Name.ShouldBe("project-or-solution-or-file");
        project.IsRequired.ShouldBeFalse();
        project.IsVariadic.ShouldBeTrue();
        build.Options.Single(o => o.LongName == "framework").ValueKind.ShouldBe(OptionValueKind.Single);
        build.Options.Single(o => o.LongName == "no-restore").ValueKind.ShouldBe(OptionValueKind.Flag);
        build.Options.Single(o => o.LongName == "verbosity").ShortName.ShouldBe("v");
        build.Options.Single(o => o.LongName == "help").ShortName.ShouldBe("h");
        build.Options.Single(o => o.LongName == "self-contained").Description!.ShouldContain(".NET 7");
    }

    [Fact]
    public void Tool_UsesCanonicalAliasWithoutCommandArgumentsInName()
    {
        var tool = Fixture("tool.fr", "tool");
        tool.SubCommands.Select(c => c.Name).ShouldBe(new[]
            { "install", "uninstall", "update", "list", "run", "search", "restore", "exec" });
    }

    [Fact]
    public void SolutionAdd_DoesNotAppendParentFilenameAfterTheCommand()
    {
        var add = Fixture("solution-add.fr", "add");
        var argument = add.Arguments.ShouldHaveSingleItem();
        argument.Name.ShouldBe("project_path");
        argument.IsRequired.ShouldBeFalse();
        argument.IsVariadic.ShouldBeTrue();
        add.Options.Single(o => o.LongName == "solution-folder").ShortName.ShouldBe("s");
    }

    [Fact]
    public void EnglishHeadingsAndNestedOptionalArguments_AreRecognized()
    {
        var node = _parser.Parse("""
            Description:
              Create a template.
            Usage:
              dotnet new [<template-short-name> [<template-args>...]] [options]
            Arguments:
              <template-short-name>  Template name.
              <template-args>        Template arguments.
            Options:
              -o, --output <output>  Output directory.
            Commands:
              create <template>     Create a template.
            """, "new")!;
        node.Arguments.Count.ShouldBe(2);
        node.Arguments[0].IsRequired.ShouldBeFalse();
        node.Arguments[1].IsRequired.ShouldBeFalse();
        node.Arguments[1].IsVariadic.ShouldBeTrue();
        node.SubCommands.ShouldHaveSingleItem().Name.ShouldBe("create");
    }

    [Fact]
    public void RequiredAndOptionalArguments_AreDistinguishedByUsage()
    {
        var node = _parser.Parse("""
            Usage:
              dotnet tool install <PACKAGE_ID> [options]
            Arguments:
              <PACKAGE_ID>  Package to install.
            Options:
              --tool-path <PATH>  Directory for the tool.
              --source <URI>     Source to use. Can be specified multiple times.
            """, "install")!;
        node.Arguments.ShouldHaveSingleItem().IsRequired.ShouldBeTrue();
        node.Options.Single(o => o.LongName == "source").ValueKind.ShouldBe(OptionValueKind.Multiple);
    }

    [Fact]
    public void WrappedOptionExamples_DoNotBecomeAdditionalOptions()
    {
        var node = _parser.Parse("""
            Options:
              -e, --environment <NAME="VALUE">  Set a variable.
                                                Can be specified multiple times.
                                                Examples:
                                                  -e NAME=first -e OTHER=second
              --no-build                        Do not build.
            """, "test")!;
        node.Options.Count.ShouldBe(2);
        node.Options[0].LongName.ShouldBe("environment");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
        node.Options[0].Description!.ShouldContain("multiple times");
    }

    [Theory]
    [InlineData("msbuild")]
    [InlineData("vstest")]
    [InlineData("fsi")]
    public void BundledNativeGrammars_PreserveRawArgumentTokens(string command)
    {
        var node = _parser.Parse("Usage: native tool\n  -property:<n>=<v>  A value.", command)!;
        node.Options.ShouldBeEmpty();
        var argument = node.Arguments.ShouldHaveSingleItem();
        argument.Name.ShouldBe("arguments");
        argument.IsVariadic.ShouldBeTrue();
        argument.IsRequired.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n\t")]
    public void EmptyHelp_IsNotACommand(string help) => _parser.Parse(help, "dotnet").ShouldBeNull();
}
