namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Repository for association classes (join entities with payload).</summary>
public interface IAssociationRepository<TAssoc, TLeft, TRight> : IRepository<TAssoc>
    where TAssoc : class
    where TLeft : class
    where TRight : class
{
    Task<TAssoc?> FindByEndpointsAsync(object leftKey, object rightKey, CancellationToken ct = default);

    Task<IReadOnlyList<TAssoc>> FindByLeftAsync(object leftKey, CancellationToken ct = default);

    Task<IReadOnlyList<TAssoc>> FindByRightAsync(object rightKey, CancellationToken ct = default);

    Task<IReadOnlyList<TRight>> FindRightsByLeftAsync(object leftKey, CancellationToken ct = default);

    Task<IReadOnlyList<TLeft>> FindLeftsByRightAsync(object rightKey, CancellationToken ct = default);
}
