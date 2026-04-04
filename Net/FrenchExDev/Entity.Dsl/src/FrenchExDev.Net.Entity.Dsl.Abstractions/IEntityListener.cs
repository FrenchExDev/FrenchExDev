namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;

/// <summary>
/// Lifecycle listener for a specific entity type.
/// Register implementations in DI; the generated DbContext dispatches to them.
/// All methods have default implementations — implement only the hooks you need.
/// </summary>
public interface IEntityListener<T> where T : class
{
    // Persistence lifecycle (Doctrine: prePersist/postPersist/preUpdate/postUpdate/preRemove/postRemove)
    Task OnAddingAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    Task OnAddedAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    Task OnModifyingAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    Task OnModifiedAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    Task OnRemovingAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    Task OnRemovedAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;

    // Materialization lifecycle (Doctrine: postLoad)
    Task OnLoadedAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// Global listener — fires for ALL entity types (Doctrine EventSubscriber equivalent).
/// Registered in DI as IGlobalEntityListener. Receives untyped EntityEntry.
/// </summary>
public interface IGlobalEntityListener
{
    Task OnAddingAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnAddedAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnModifyingAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnModifiedAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnRemovingAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnRemovedAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    Task OnLoadedAsync(EntityEntry entry, CancellationToken ct = default) => Task.CompletedTask;
}
