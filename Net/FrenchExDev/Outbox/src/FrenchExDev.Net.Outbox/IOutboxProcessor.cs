namespace FrenchExDev.Net.Outbox;

/// <summary>
/// Processes pending outbox messages.
/// </summary>
public interface IOutboxProcessor
{
    /// <summary>
    /// Processes all pending outbox messages.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    Task ProcessPendingAsync(CancellationToken ct = default);
}
