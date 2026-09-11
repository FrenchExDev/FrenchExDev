using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.Dotnet.Tests;

public sealed class DotnetCommandTests
{
    [Fact]
    public async Task Build_PreservesFlagsAndProjectPathBoundaries()
    {
        var command = new DotnetBuildCommand
        {
            Framework = "net8.0",
            Configuration = "Release",
            NoRestore = true,
            ProjectOrSolution = ["directory with spaces/project.csproj"],
        };
        command.ToArguments().ShouldBe(new[]
        {
            "--framework", "net8.0", "--configuration", "Release", "--no-restore",
            "directory with spaces/project.csproj",
        });
        var binding = new BinaryBinding { Identifier = new BinaryIdentifier("dotnet"), ExecutablePath = "dotnet" };
        var process = new FakeProcessRunner(stdout: "Build succeeded.");
        var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]), process);
        (await executor.ExecuteAsync(binding.Identifier, command)).IsSuccess.ShouldBeTrue();
        process.LastSpec.ShouldNotBeNull();
        process.LastSpec.Arguments.ShouldBe(new[]
        {
            "build", "--framework", "net8.0", "--configuration", "Release", "--no-restore",
            "directory with spaces/project.csproj",
        });
    }

    [Fact]
    public void NugetSource_SerializesValueOptionsWithoutHelpPlaceholders()
    {
        var command = new DotnetNugetAddSourceCommand
        {
            Name = "local-feed",
            Configfile = "config with spaces/NuGet.Config",
            PackageSourcePath = "packages with spaces",
        };
        command.CommandPath.ShouldBe(new[] { "nuget", "add", "source" });
        command.ToArguments().ShouldBe(new[]
        {
            "--name", "local-feed", "--configfile", "config with spaces/NuGet.Config", "packages with spaces",
        });
    }

    [Fact]
    public void Msbuild_PreservesItsNativeColonSyntax()
    {
        var command = new DotnetMsbuildCommand
        {
            Arguments = ["project with spaces.csproj", "-property:Configuration=Release", "-target:Build"],
        };
        command.CommandPath.ShouldBe(new[] { "msbuild" });
        command.ToArguments().ShouldBe(new[]
        {
            "project with spaces.csproj", "-property:Configuration=Release", "-target:Build",
        });
    }

    [Fact]
    public void UserSecrets_IdIsAValueAndPositionalsRemainOrdered()
    {
        var command = new DotnetUserSecretsSetCommand { Id = "test-store", Name = "Key", Value = "test value" };
        command.CommandPath.ShouldBe(new[] { "user-secrets", "set" });
        command.ToArguments().ShouldBe(new[] { "--id", "test-store", "Key", "test value" });
    }
}
