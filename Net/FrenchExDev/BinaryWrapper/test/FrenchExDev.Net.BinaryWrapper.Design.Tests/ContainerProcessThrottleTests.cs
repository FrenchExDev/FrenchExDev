using FrenchExDev.Net.BinaryWrapper.Design;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Tests;

public sealed class ContainerProcessThrottleTests
{
    [Theory]
    [InlineData("podman", true)]
    [InlineData("podman.exe", true)]
    [InlineData("C:\\Program Files\\Podman\\PODMAN.EXE", true)]
    [InlineData("docker", false)]
    [InlineData("powershell.exe", false)]
    public async Task ConcurrentCalls_ShareTheWindowsPodmanLimit(string executable, bool isPodman)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var tasks = Enumerable.Range(0, 16).Select(i => ContainerProcessThrottle.RunAsync(executable, async () =>
        {
            Interlocked.Increment(ref active);
            try { await release.Task; return i; }
            finally { Interlocked.Decrement(ref active); }
        })).ToArray();
        var initiallyActive = Volatile.Read(ref active);
        release.TrySetResult();

        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
        initiallyActive.ShouldBe(OperatingSystem.IsWindows() && isPodman ? 4 : 16);
        results.ShouldBe(Enumerable.Range(0, 16).ToArray());
        active.ShouldBe(0);
    }

    [Fact]
    public async Task FailedCalls_ReleaseTheirSlots()
    {
        for (var i = 0; i < 12; i++)
            await Should.ThrowAsync<InvalidOperationException>(() =>
                ContainerProcessThrottle.RunAsync<string>("podman",
                    () => throw new InvalidOperationException("Failed process"))
                    .WaitAsync(TimeSpan.FromSeconds(10)));

        var result = await ContainerProcessThrottle.RunAsync("podman", () => Task.FromResult("recovered"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        result.ShouldBe("recovered");
    }
}