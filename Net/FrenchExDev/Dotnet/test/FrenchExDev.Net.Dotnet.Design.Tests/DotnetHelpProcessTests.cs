using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.Dotnet.Design;
using Shouldly;

namespace FrenchExDev.Net.Dotnet.Design.Tests;

public sealed class DotnetHelpProcessTests
{
    [Theory]
    [InlineData("podman", "Cannot connect to Podman. Please verify your connection.")]
    [InlineData("podman.exe", "Error: unable to connect to Podman socket")]
    [InlineData("PODMAN", "ssh: handshake failed: read tcp: connection reset by peer")]
    public async Task TransientConnectionFailure_RetriesAndReturnsRecoveredHelp(string runtime, string error)
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();
        var help = await DotnetHelpProcess.RunAsync(runtime,
            () => Task.FromResult(++attempts < 3
                ? ("", error, 125)
                : ("", "Usage: dotnet nuget [command]", 1)),
            delay => { delays.Add(delay); return Task.CompletedTask; });

        help.ShouldBe("Usage: dotnet nuget [command]");
        attempts.ShouldBe(3);
        delays.ShouldBe(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) });
    }

    [Fact]
    public async Task PersistentConnectionFailure_StopsAfterThreeAttempts()
    {
        var attempts = 0;
        var error = await Should.ThrowAsync<ContainerRuntimeException>(() =>
            DotnetHelpProcess.RunAsync("podman",
                () => { attempts++; return Task.FromResult(("", "Cannot connect to Podman", 125)); },
                _ => Task.CompletedTask));

        attempts.ShouldBe(3);
        error.Message.ShouldContain("exit 125");
        error.Message.ShouldContain("Cannot connect to Podman");
    }

    [Theory]
    [InlineData("podman", "Error: no container with name or ID sdk found", 125)]
    [InlineData("docker", "Cannot connect to Podman", 125)]
    [InlineData("podman", "SDK missing", 1)]
    [InlineData("podman", "ssh: handshake failed", 1)]
    public async Task OtherFailures_AreNotRetried(string runtime, string stderr, int exitCode)
    {
        var attempts = 0;
        var delays = 0;
        await Should.ThrowAsync<InvalidOperationException>(() =>
            DotnetHelpProcess.RunAsync(runtime,
                () => { attempts++; return Task.FromResult(("", stderr, exitCode)); },
                _ => { delays++; return Task.CompletedTask; }));

        attempts.ShouldBe(1);
        delays.ShouldBe(0);
    }

    [Theory]
    [InlineData("", "Error: no container found\nUsage: podman exec [options]")]
    [InlineData("Usage: dotnet nuget [command]", "Cannot connect to Podman")]
    public void RuntimeFailure_IsNeverAcceptedAsHelp(string stdout, string stderr) =>
        Should.Throw<ContainerRuntimeException>(() => DotnetHelpProcess.SelectHelp(stdout, stderr, 125));

    [Fact]
    public async Task SuccessfulHelp_ReturnsWithoutRetry()
    {
        var help = await DotnetHelpProcess.RunAsync("podman",
            () => Task.FromResult(("Usage: dotnet nuget [command]", "", 0)),
            _ => throw new InvalidOperationException("Unexpected retry"));

        help.ShouldBe("Usage: dotnet nuget [command]");
    }
}