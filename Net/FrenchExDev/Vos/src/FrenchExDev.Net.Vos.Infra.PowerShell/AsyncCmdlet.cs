using System.Management.Automation;
using System.Threading.Channels;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

/// <summary>
/// Base class for async PowerShell cmdlets.
/// Bridges async operations to the synchronous PS pipeline using a channel-based message pump.
/// Derived classes implement <see cref="ProcessRecordAsync"/> instead of <see cref="PSCmdlet.ProcessRecord"/>.
/// </summary>
public abstract class AsyncCmdlet : PSCmdlet
{
    protected abstract Task ProcessRecordAsync(CancellationToken ct);

    protected override void ProcessRecord()
    {
        using var cts = new CancellationTokenSource();
        var channel = Channel.CreateUnbounded<Action>();

        var task = Task.Run(async () =>
        {
            try
            {
                await ProcessRecordAsync(cts.Token);
            }
            finally
            {
                channel.Writer.Complete();
            }
        });

        // Drain channel on the cmdlet thread — safe to call WriteObject/WriteVerbose/etc.
        foreach (var action in channel.Reader.ReadAllAsync().ToBlockingEnumerable())
            action();

        task.GetAwaiter().GetResult(); // rethrow exceptions
    }

    /// <summary>Thread-safe: queues WriteObject to the cmdlet thread.</summary>
    protected void QueueWriteObject(Channel<Action> channel, object obj)
        => channel.Writer.TryWrite(() => WriteObject(obj));

    /// <summary>Thread-safe: queues WriteVerbose to the cmdlet thread.</summary>
    protected void QueueWriteVerbose(Channel<Action> channel, string text)
        => channel.Writer.TryWrite(() => WriteVerbose(text));

    /// <summary>Thread-safe: queues WriteProgress to the cmdlet thread.</summary>
    protected void QueueWriteProgress(Channel<Action> channel, ProgressRecord record)
        => channel.Writer.TryWrite(() => WriteProgress(record));
}
