namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Read-only repository — queries only. Use for CQRS read side.</summary>
public interface IReadOnlyRepository<T> where T : class
{
    ValueTask<T?> FindByIdAsync(params object[] keyValues);

    Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    Task<IReadOnlyList<T>> FindAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<T>> FindWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    Task<IReadOnlyList<T>> FindBySpecAsync(ISpecification<T> spec, CancellationToken ct = default);

    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

    IQueryable<T> Query { get; }
}
