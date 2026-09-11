namespace FrenchExDev.Net.Saga;

/// <summary>Represents the lifecycle state of a saga instance.</summary>
public enum SagaState
{
    /// <summary>The saga has been created but not yet started.</summary>
    Pending,

    /// <summary>The saga is actively executing its steps.</summary>
    Running,

    /// <summary>All steps completed successfully.</summary>
    Completed,

    /// <summary>A step failed and compensation is in progress.</summary>
    Compensating,

    /// <summary>All executed steps have been compensated after a failure.</summary>
    Compensated,

    /// <summary>The saga failed and compensation also failed.</summary>
    Failed
}
