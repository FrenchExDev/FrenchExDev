namespace FrenchExDev.Net.Outbox;

/// <summary>
/// Represents a message stored in the outbox, pending publication.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Gets or sets the unique identifier of the message.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the fully-qualified type name of the event.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialized event payload.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp when the message was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the timestamp when the message was successfully processed.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Gets or sets the number of processing attempts.</summary>
    public int Attempts { get; set; }

    /// <summary>Gets or sets the last error encountered during processing.</summary>
    public string? LastError { get; set; }
}
