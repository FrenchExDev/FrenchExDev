using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Abstractions;

/// <summary>
/// Load config once, batch mutations, commit (save) once.
/// Holds a file lock for the duration.
/// </summary>
public interface IVosUnitOfWork : IAsyncDisposable
{
    VosConfig Config { get; }
    VosConfigManager Manager { get; }
    Task<Res.Result> CommitAsync(CancellationToken ct = default);
    void Rollback();
}

public interface IVosUnitOfWorkFactory
{
    Task<IVosUnitOfWork> CreateAsync(string configPath, bool local = false, CancellationToken ct = default);
}
