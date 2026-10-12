namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Generic EF Core repository base implementing <see cref="IRepository{T}"/>.
/// Provides all standard CRUD and query operations.
/// Developer creates a project-level intermediate (e.g., <c>MyProjectRepository&lt;T&gt;</c>)
/// inheriting this class for cross-cutting concerns. The SG-generated per-entity repositories
/// then inherit the developer's intermediate.
/// </summary>
public abstract class RepositoryBase<T> : IRepository<T> where T : class
{
    private readonly DbContext _context;
    private readonly DbSet<T> _dbSet;

    protected RepositoryBase(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    protected DbContext Context => _context;
    protected DbSet<T> DbSet => _dbSet;

    // ── IReadOnlyRepository<T> ─────────────────────────────────────

    public virtual ValueTask<T?> FindByIdAsync(params object[] keyValues)
        => _dbSet.FindAsync(keyValues);

    public virtual Task<T?> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
        => _dbSet.FirstOrDefaultAsync(predicate, ct);

    public virtual async Task<IReadOnlyList<T>> FindAllAsync(CancellationToken ct = default)
        => await _dbSet.ToListAsync(ct);

    public virtual async Task<IReadOnlyList<T>> FindWhereAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
        => await _dbSet.Where(predicate).ToListAsync(ct);

    public virtual async Task<IReadOnlyList<T>> FindBySpecAsync(
        ISpecification<T> spec,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsQueryable();
        query = query.Where(spec.Criteria);
        foreach (var include in spec.Includes)
            query = query.Include(include);
        if (spec.OrderBy != null) query = query.OrderBy(spec.OrderBy);
        if (spec.OrderByDescending != null) query = query.OrderByDescending(spec.OrderByDescending);
        if (spec.Skip.HasValue) query = query.Skip(spec.Skip.Value);
        if (spec.Take.HasValue) query = query.Take(spec.Take.Value);
        return await query.ToListAsync(ct);
    }

    public virtual Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
        => _dbSet.AnyAsync(predicate, ct);

    public virtual Task<int> CountAsync(CancellationToken ct = default)
        => _dbSet.CountAsync(ct);

    public IQueryable<T> Query => _dbSet.AsQueryable();

    // ── IRepository<T> (commands) ──────────────────────────────────

    public virtual void Add(T entity) => _dbSet.Add(entity);
    public virtual void AddRange(IEnumerable<T> entities) => _dbSet.AddRange(entities);
    public virtual void Update(T entity) => _dbSet.Update(entity);
    public virtual void Remove(T entity) => _dbSet.Remove(entity);
    public virtual void RemoveRange(IEnumerable<T> entities) => _dbSet.RemoveRange(entities);
    public virtual void Attach(T entity) => _dbSet.Attach(entity);
}
