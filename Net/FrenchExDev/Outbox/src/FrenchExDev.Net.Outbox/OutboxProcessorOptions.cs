namespace FrenchExDev.Net.Outbox;

/// <summary>
/// Configuration options for the outbox processor.
/// </summary>
public class OutboxProcessorOptions
{
    /// <summary>Gets or sets the polling interval between processing cycles. Default is 5 seconds.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the maximum number of messages to process per batch. Default is 100.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Gets or sets the maximum number of retry attempts per message. Default is 3.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Gets or sets how long processed messages are retained before cleanup. Default is 7 days.</summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}
