using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.Dotnet.Design;
using Shouldly;

namespace FrenchExDev.Net.Dotnet.Design.Tests;

public sealed class BundledHelpTests
{
    private static CommandNode Fixture(string name, string command) => new DotnetHelpParser().Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".en.help.txt")), command)!;

    [Fact]
    public void UserSecrets_OptionalPositionalsAndImplicitIdValue()
    {
        var node = Fixture("user-secrets-set", "set");
        node.Arguments.Select(a => a.Name).ShouldBe(new[] { "name", "value" });
        node.Arguments.All(a => !a.IsRequired).ShouldBeTrue();
        node.Options.Single(o => o.LongName == "id").ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void AlpineSdkRoot_RecognizesBothCommandSections()
    {
        var root = Fixture("root", "dotnet");
        root.SubCommands.Select(c => c.Name).ShouldContain("build");
        root.SubCommands.Select(c => c.Name).ShouldContain("watch");
        root.SubCommands.Select(c => c.Name).ShouldContain("nuget");
    }

    [Fact]
    public void Nuget_ValueOptionsWithoutPlaceholders_AreNotBooleanFlags()
    {
        var node = Fixture("nuget-add-source", "source");
        node.Arguments.ShouldHaveSingleItem().Name.ShouldBe("package-source-path");
        node.Options.Single(o => o.LongName == "name").ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options.Single(o => o.LongName == "configfile").ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options.Single(o => o.LongName == "store-password-in-clear-text").ValueKind.ShouldBe(OptionValueKind.Flag);
    }

    [Fact]
    public void NugetPush_RootRepresentsRawPositionalArguments()
    {
        var node = Fixture("nuget-push", "push");
        var argument = node.Arguments.ShouldHaveSingleItem();
        argument.Name.ShouldBe("root");
        argument.IsVariadic.ShouldBeTrue();
        argument.IsRequired.ShouldBeFalse();
        node.Options.Single(o => o.LongName == "symbol-source").ShortName.ShouldBeNull();
    }

    [Fact]
    public void Watch_UsesItsDashedUsageAndImplicitValueOptions()
    {
        var node = Fixture("watch", "watch");
        var forwarded = node.Arguments.ShouldHaveSingleItem();
        forwarded.IsVariadic.ShouldBeTrue();
        forwarded.IsRequired.ShouldBeFalse();
        node.Options.Single(o => o.LongName == "project").ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options.Single(o => o.LongName == "framework").ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options.Single(o => o.LongName == "no-hot-reload").ValueKind.ShouldBe(OptionValueKind.Flag);
    }

    [Fact]
    public void HelpOnStderr_WithNonzeroExit_IsStillHelp() =>
        DotnetHelpProcess.SelectHelp("", "Usage: dotnet user-secrets [options] [command]", 1)
            .ShouldStartWith("Usage:");

    [Fact]
    public void HelpOnStderr_IsPreferredOverUnrelatedStdout() =>
        DotnetHelpProcess.SelectHelp("Startup banner", "Usage: dotnet tool [command]", 1)
            .ShouldStartWith("Usage:");

    [Fact]
    public void FailedCommandWithoutHelp_IsNotAValidSnapshot() =>
        Should.Throw<InvalidOperationException>(() => DotnetHelpProcess.SelectHelp("", "SDK missing", 1));
}
