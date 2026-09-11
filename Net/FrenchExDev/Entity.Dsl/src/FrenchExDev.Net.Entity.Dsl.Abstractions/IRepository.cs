namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

using System.Collections.Generic;

/// <summary>Full repository — extends read-only with commands.</summary>
public interface IRepository<T> : IReadOnlyRepository<T> where T : class
{
    void Add(T entity);

    void AddRange(IEnumerable<T> entities);

    /// <summary>
    /// Marks a disconnected entity as modified. For tracked entities, EF Core
    /// change tracking handles updates automatically — no need to call this.
    /// </summary>
    void Update(T entity);

    void Remove(T entity);

    void RemoveRange(IEnumerable<T> entities);

    /// <summary>Attaches a disconnected entity without marking it as modified.</summary>
    void Attach(T entity);
}
