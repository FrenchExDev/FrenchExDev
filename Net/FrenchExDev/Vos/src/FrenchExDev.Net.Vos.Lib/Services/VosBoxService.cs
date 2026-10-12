using FrenchExDev.Net.Injectable.Attributes;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.Vos.Lib.Services;

[Injectable(Scope = Scope.Transient, As = new[] { typeof(IVosBoxService) })]
public sealed class VosBoxService(
    IVosBackend backend,
    IVosFileReader fileReader,
    IVosEventEmitter emitter,
    ILogger<VosBoxService> logger) : IVosBoxService
{
    public async Task<Res.Result<VosActionResult>> ListAsync(bool boxInfo = false, CancellationToken ct = default)
    {
        _ = logger;
        emitter.Emit(new BoxListing());
        var r = await backend.ImageListAsync(ct);
        emitter.Emit(new BoxListed(r.Output));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> AddAsync(string name, Abstractions.Options.VosBoxAddOptions? options = null, CancellationToken ct = default)
    {
        emitter.Emit(new BoxAdding(name));
        var r = await backend.ImageAddAsync(name, ct);
        emitter.Emit(new BoxAdded(name));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> RemoveAsync(string name, Abstractions.Options.VosBoxRemoveOptions? options = null, CancellationToken ct = default)
    {
        emitter.Emit(new BoxRemoving(name));
        var r = await backend.ImageRemoveAsync(name, ct);
        emitter.Emit(new BoxRemoved(name));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> UpdateAsync(string configPath, string instanceName, Abstractions.Options.VosBoxUpdateOptions? options = null, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyList<(string, VosActionResult)>>.Failure(loadResult.ValidationResult!);

        var orch = new VosOrchestrator(backend, loadResult.Value!);
        emitter.Emit(new BoxUpdating(instanceName));
        var results = await orch.ExecuteAsync(instanceName, false, (b, i, c) => b.ImageUpdateAsync(i, c), ct);
        emitter.Emit(new BoxUpdated(instanceName));
        return Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>.Success(results);
    }

    public async Task<Res.Result<VosActionResult>> PruneAsync(Abstractions.Options.VosBoxPruneOptions? options = null, CancellationToken ct = default)
    {
        emitter.Emit(new BoxPruning());
        var r = await backend.ImagePruneAsync(ct);
        emitter.Emit(new BoxPruned());
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> OutdatedAsync(string configPath, string instanceName, Abstractions.Options.VosBoxOutdatedOptions? options = null, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyList<(string, VosActionResult)>>.Failure(loadResult.ValidationResult!);

        var orch = new VosOrchestrator(backend, loadResult.Value!);
        emitter.Emit(new BoxOutdatedChecking(instanceName));
        var results = await orch.ExecuteAsync(instanceName, false, (b, i, c) => b.ImageOutdatedAsync(i, c), ct);
        emitter.Emit(new BoxOutdatedChecked(instanceName));
        return Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>.Success(results);
    }

    public async Task<Res.Result<VosActionResult>> RepackageAsync(string name, string provider, string version, CancellationToken ct = default)
    {
        emitter.Emit(new BoxRepackaging(name, provider, version));
        var r = await backend.ImageRepackageAsync(name, provider, version, ct);
        emitter.Emit(new BoxRepackaged(name));
        return Res.Result<VosActionResult>.Success(r);
    }
}
