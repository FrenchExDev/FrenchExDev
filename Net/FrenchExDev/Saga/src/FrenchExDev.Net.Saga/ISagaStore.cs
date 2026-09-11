namespace FrenchExDev.Net.Saga;

/// <summary>Persistence interface for saga instances.</summary>
public interface ISagaStore
{
    /// <summary>Saves a new saga instance.</summary>
    Task SaveAsync(SagaInstance instance, CancellationToken ct = default);

    /// <summary>Finds a saga instance by its identifier.</summary>
    Task<SagaInstance?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Finds all saga instances in the <see cref="SagaState.Pending"/> state.</summary>
    Task<IReadOnlyList<SagaInstance>> FindPendingAsync(CancellationToken ct = default);

    /// <summary>Updates an existing saga instance.</summary>
    Task UpdateAsync(SagaInstance instance, CancellationToken ct = default);
}
