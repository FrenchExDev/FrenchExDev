using FrenchExDev.Net.Injectable.Attributes;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.Vos.Lib.Services;

[Injectable(Scope = Scope.Transient, As = new[] { typeof(IVosNetworkService) })]
public sealed class VosNetworkService(
    IVosFileReader fileReader,
    IVosFileWriter fileWriter,
    IVosEventEmitter emitter,
    ILogger<VosNetworkService> logger) : IVosNetworkService
{
    public async Task<Res.Result<int>> GenerateAsync(string configPath, string subnet = "192.168.56.0/24", int startAt = 10, bool local = false, CancellationToken ct = default)
    {
        _ = logger;
        emitter.Emit(new NetworkGenerating(subnet, startAt));
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<int>.Failure(loadResult.ValidationResult!);

        var assigned = NetworkGenerator.Generate(loadResult.Value!, subnet, startAt);
        await (local ? fileWriter.WriteLocalAsync(loadResult.Value!, configPath, ct) : fileWriter.WriteAsync(loadResult.Value!, configPath, ct));

        var conflicts = NetworkGenerator.ValidateNoConflicts(loadResult.Value!);
        foreach (var c in conflicts)
            emitter.Emit(new NetworkConflictDetected(c));

        emitter.Emit(new NetworkGenerated(assigned));
        return Res.Result<int>.Success(assigned);
    }

    public async Task<Res.Result<IReadOnlyList<(string Name, string? Ip, string? Hostname)>>> ShowAsync(string configPath, CancellationToken ct = default)
    {
        emitter.Emit(new NetworkShowing());
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyList<(string, string?, string?)>>.Failure(loadResult.ValidationResult!);

        var assignments = NetworkGenerator.Show(loadResult.Value!);

        var conflicts = NetworkGenerator.ValidateNoConflicts(loadResult.Value!);
        foreach (var c in conflicts)
            emitter.Emit(new NetworkConflictDetected(c));

        emitter.Emit(new NetworkShown(assignments.Count));
        return Res.Result<IReadOnlyList<(string Name, string? Ip, string? Hostname)>>.Success(assignments);
    }
}
