namespace FrenchExDev.Net.Outbox;

/// <summary>
/// Stores outbox messages for later processing.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Stores a message in the outbox.
    /// </summary>
    /// <param name="message">The message to store.</param>
    /// <param name="ct">A cancellation token.</param>
    Task StoreAsync(OutboxMessage message, CancellationToken ct = default);
}
