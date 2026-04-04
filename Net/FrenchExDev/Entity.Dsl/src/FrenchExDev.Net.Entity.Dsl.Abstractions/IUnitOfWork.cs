namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    int SaveChanges();

    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);

    void DetachAll();

    bool HasChanges { get; }
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);

    Task RollbackAsync(CancellationToken ct = default);
}

/// <summary>
/// Typed UnitOfWork that exposes all repositories for a given DbContext.
/// Generated per [DbContext].
/// </summary>
public interface IUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
{
    TContext Context { get; }
}
