using System.Collections.Concurrent;

namespace FrenchExDev.Net.Outbox.Testing;

/// <summary>
/// In-memory implementation of <see cref="IOutbox"/> for testing purposes.
/// </summary>
public sealed class InMemoryOutbox : IOutbox
{
    private readonly ConcurrentBag<OutboxMessage> _messages = new();

    /// <summary>
    /// Gets all stored outbox messages for test assertions.
    /// </summary>
    public IReadOnlyCollection<OutboxMessage> Messages => _messages.ToArray();

    /// <inheritdoc />
    public Task StoreAsync(OutboxMessage message, CancellationToken ct = default)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(message);
#else
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }
#endif
        _messages.Add(message);
        return Task.CompletedTask;
    }
}
