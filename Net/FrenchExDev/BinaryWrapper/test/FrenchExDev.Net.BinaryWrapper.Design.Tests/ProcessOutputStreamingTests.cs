using System.Collections.Concurrent;
using System.Text;
using FrenchExDev.Net.BinaryWrapper.Design;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Tests;

public sealed class ProcessOutputStreamingTests
{
    private static string[] Script(string windows, string unix) =>
        OperatingSystem.IsWindows()
            ? ["powershell.exe", "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
                Convert.ToBase64String(Encoding.Unicode.GetBytes("$ProgressPreference = 'SilentlyContinue'; " + windows))]
            : ["/bin/sh", "-c", unix];

    [Fact]
    public async Task StreamsBeforeExit_AndPreservesCapturedOutput()
    {
        var gate = Path.Combine(Path.GetTempPath(), "bw-output-" + Guid.NewGuid().ToString("N"));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stdout = new ConcurrentQueue<string>();
        var stderr = new ConcurrentQueue<string>();
        var command = Script(
            "[Console]::Out.Write(\"first`r`n\"); [Console]::Out.Flush(); " +
            $"while (-not (Test-Path -LiteralPath '{gate.Replace("'", "''")}')) {{ Start-Sleep -Milliseconds 10 }}; " +
            "[Console]::Error.Write('error-tail'); [Console]::Out.Write('last')",
            $"printf 'first\\r\\n'; while [ ! -f '{gate.Replace("'", "'\\''")}' ]; do sleep 0.01; done; " +
            "printf error-tail >&2; printf last");
        var process = ProcessRunnerContainerRuntime.RunProcessAsync(command,
            line => { stdout.Enqueue(line); ready.TrySetResult(); }, stderr.Enqueue);
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            process.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            await File.WriteAllTextAsync(gate, "");
            try
            {
                var captured = await process.WaitAsync(TimeSpan.FromSeconds(15));
                captured.ShouldBe("first\r\nlast");
            }
            finally { File.Delete(gate); }
        }
        stdout.ToArray().ShouldBe(new[] { "first", "last" });
        stderr.ToArray().ShouldBe(new[] { "error-tail" });
    }

    [Fact]
    public async Task ObserverFailure_StillDrainsBothStreams()
    {
        var command = Script(
            "for ($i=0; $i -lt 1024; $i++) { [Console]::Out.WriteLine(('o' * 1024)); [Console]::Error.WriteLine(('e' * 1024)) }",
            "i=0; while [ $i -lt 4096 ]; do printf 'stdout payload abcdefghijklmnopqrstuvwxyz0123456789\\n'; " +
            "printf 'stderr payload abcdefghijklmnopqrstuvwxyz0123456789\\n' >&2; i=$((i+1)); done");
        var errors = 0;
        var process = ProcessRunnerContainerRuntime.RunProcessAsync(command,
            _ => throw new InvalidOperationException("observer failure"),
            _ => Interlocked.Increment(ref errors));
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => process.WaitAsync(TimeSpan.FromSeconds(20)));
        exception.Message.ShouldBe("observer failure");
        errors.ShouldBeGreaterThan(100);
    }

    [Fact]
    public async Task NonzeroExit_StreamsAndReportsStderr()
    {
        var stderr = new ConcurrentQueue<string>();
        var command = Script("[Console]::Error.Write('build-error'); exit 7", "printf build-error >&2; exit 7");
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            ProcessRunnerContainerRuntime.RunProcessAsync(command, null, stderr.Enqueue).WaitAsync(TimeSpan.FromSeconds(15)));
        exception.Message.ShouldContain("code 7");
        exception.Message.ShouldContain("build-error");
        stderr.ToArray().ShouldBe(new[] { "build-error" });
    }
}

