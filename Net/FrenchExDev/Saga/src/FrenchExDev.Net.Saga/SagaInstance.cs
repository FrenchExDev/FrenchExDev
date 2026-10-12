namespace FrenchExDev.Net.Saga;

/// <summary>Persistence entity representing a stored saga instance.</summary>
public sealed class SagaInstance
{
    /// <summary>Unique identifier for the saga instance.</summary>
    public Guid Id { get; set; }

    /// <summary>The type name of the saga (used for deserialization routing).</summary>
    public string SagaType { get; set; } = string.Empty;

    /// <summary>Serialized saga context as JSON.</summary>
    public string ContextJson { get; set; } = string.Empty;

    /// <summary>Current lifecycle state of the saga.</summary>
    public SagaState State { get; set; }

    /// <summary>Zero-based index of the current step.</summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>Timestamp when the saga was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp when the saga completed, was compensated, or failed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Error message from the last failed step, if any.</summary>
    public string? LastError { get; set; }
}
