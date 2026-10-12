namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Controls how the state machine handles concurrent access.
/// </summary>
public enum ConcurrencyMode
{
    /// <summary>No synchronization. Caller is responsible for single-threaded access.</summary>
    None,

    /// <summary>SemaphoreSlim-based async locking. Recommended for multi-threaded use.</summary>
    Semaphore
}
