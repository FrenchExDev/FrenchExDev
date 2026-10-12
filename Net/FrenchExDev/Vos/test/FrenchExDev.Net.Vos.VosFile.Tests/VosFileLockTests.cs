using FrenchExDev.Net.Vos.VosFile;

namespace FrenchExDev.Net.Vos.VosFile.Tests;

public class VosFileLockTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"voslock-{Guid.NewGuid():N}.yaml");

    public VosFileLockTests() => File.WriteAllText(_tempFile, "test");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
        if (File.Exists(_tempFile + ".lock")) File.Delete(_tempFile + ".lock");
    }

    [Fact]
    public void Acquire_creates_lock_file()
    {
        using var fileLock = VosFileLock.Acquire(_tempFile);
        File.Exists(_tempFile + ".lock").ShouldBeTrue();
    }

    [Fact]
    public void Dispose_releases_lock()
    {
        var fileLock = VosFileLock.Acquire(_tempFile);
        fileLock.Dispose();
        // Should be able to acquire again after dispose
        using var second = VosFileLock.Acquire(_tempFile);
        second.ShouldNotBeNull();
    }

    [Fact]
    public void Acquire_times_out_when_lock_already_held()
    {
        using var first = VosFileLock.Acquire(_tempFile);

        Should.Throw<TimeoutException>(() =>
            VosFileLock.Acquire(_tempFile, TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void Double_dispose_does_not_throw()
    {
        var fileLock = VosFileLock.Acquire(_tempFile);
        fileLock.Dispose();
        Should.NotThrow(() => fileLock.Dispose());
    }

    [Fact]
    public async Task Acquire_retries_and_succeeds_when_lock_released()
    {
        var first = VosFileLock.Acquire(_tempFile);

        // Release the lock on a background thread after a short delay
        var releaseTask = Task.Run(async () =>
        {
            await Task.Delay(200);
            first.Dispose();
        });

        // This should retry (catch IOException when not timed out) then succeed
        using var second = VosFileLock.Acquire(_tempFile, TimeSpan.FromSeconds(5));
        second.ShouldNotBeNull();
        await releaseTask;
    }
}
