using System.Collections.Concurrent;

namespace FrenchExDev.Net.Saga.Testing;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ISagaStore"/> for testing purposes.
/// </summary>
public sealed class InMemorySagaStore : ISagaStore
{
    private readonly ConcurrentDictionary<Guid, SagaInstance> _store = new();

    /// <inheritdoc />
    public Task SaveAsync(SagaInstance instance, CancellationToken ct = default)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        _store[instance.Id] = instance;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SagaInstance?> FindAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var instance);
        return Task.FromResult<SagaInstance?>(instance);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SagaInstance>> FindPendingAsync(CancellationToken ct = default)
    {
        var pending = _store.Values
            .Where(i => i.State == SagaState.Pending)
            .ToList();
        return Task.FromResult<IReadOnlyList<SagaInstance>>(pending);
    }

    /// <inheritdoc />
    public Task UpdateAsync(SagaInstance instance, CancellationToken ct = default)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        _store[instance.Id] = instance;
        return Task.CompletedTask;
    }
}
