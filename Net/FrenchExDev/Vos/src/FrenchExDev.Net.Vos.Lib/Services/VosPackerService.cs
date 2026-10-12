using FrenchExDev.Net.Injectable.Attributes;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.Vos.Lib.Services;

[Injectable(Scope = Scope.Transient, As = new[] { typeof(IVosPackerService) })]
public sealed class VosPackerService(
    IVosBackend backend,
    IVosEventEmitter emitter,
    ILogger<VosPackerService> logger) : IVosPackerService
{
    public async Task<Res.Result<VosActionResult>> InitAsync(string boxName, string outputPath = ".", CancellationToken ct = default)
    {
        _ = logger;
        emitter.Emit(new BoxInitializing(boxName, outputPath));
        var r = await backend.ImageCreateAsync(boxName, outputPath, ct);
        emitter.Emit(new BoxInitialized(boxName, outputPath));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosImageBuildResult>> BuildAsync(string projectPath, Abstractions.Options.VosPackerBuildOptions? options = null, CancellationToken ct = default)
    {
        var force = options?.Force ?? false;
        emitter.Emit(new BoxBuilding(projectPath, force));

        Dictionary<string, string>? vars = options?.Vars;
        var r = await backend.ImageBuildAsync(projectPath, vars, force, ct);

        if (r.Success)
        {
            foreach (var a in r.Artifacts)
                emitter.Emit(new BoxArtifactProduced(a.BuilderType, a.Name, a.Path));
            emitter.Emit(new BoxBuilt(projectPath, r.Artifacts.Count));
        }
        else
        {
            emitter.Emit(new BoxBuildFailed(projectPath, r.Error ?? r.Output));
        }

        return Res.Result<VosImageBuildResult>.Success(r);
    }
}
