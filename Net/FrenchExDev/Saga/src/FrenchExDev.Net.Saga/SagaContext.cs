namespace FrenchExDev.Net.Saga;

/// <summary>Base class for saga context, carrying state across steps.</summary>
public abstract class SagaContext
{
    /// <summary>Unique identifier for this saga instance.</summary>
    public Guid SagaId { get; set; } = Guid.NewGuid();

    /// <summary>Current lifecycle state of the saga.</summary>
    public SagaState State { get; set; } = SagaState.Pending;

    /// <summary>Zero-based index of the step currently being executed.</summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>Error message from the last failed step, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>Timestamp when the saga execution started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Timestamp when the saga completed, was compensated, or failed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
