using FrenchExDev.Net.Injectable.Attributes;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.Vos.Lib.Services;

[Injectable(Scope = Scope.Transient, As = new[] { typeof(IVosVmService) })]
public sealed class VosVmService(
    IVosBackend backend,
    IVosFileReader fileReader,
    IVosEventEmitter emitter,
    ILogger<VosVmService> logger) : IVosVmService
{
    private async Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> ExecuteOnTargetsAsync(
        string configPath, string? instanceName,
        Func<IVosBackend, ResolvedInstance, CancellationToken, Task<VosActionResult>> action,
        CancellationToken ct)
    {
        _ = logger;
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure)
            return Res.Result<IReadOnlyList<(string, VosActionResult)>>.Failure(loadResult.ValidationResult!);

        var orch = new VosOrchestrator(backend, loadResult.Value!);
        var all = instanceName is null;
        var results = await orch.ExecuteAsync(instanceName, all, action, ct);
        return Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>.Success(results);
    }

    public async Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> UpAsync(string configPath, string? instanceName, Abstractions.Options.VosUpOptions? options = null, CancellationToken ct = default)
    {
        return await ExecuteOnTargetsAsync(configPath, instanceName, async (b, i, c) =>
        {
            emitter.Emit(new VmStarting(i.Name));
            var r = await b.UpAsync(i, c);
            emitter.Emit(r.Success ? new VmStarted(i.Name) : new VmOperationFailed("up", i.Name, r.Error ?? r.Output));
            return r;
        }, ct);
    }

    public async Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> HaltAsync(string configPath, string? instanceName, bool force = false, CancellationToken ct = default)
    {
        return await ExecuteOnTargetsAsync(configPath, instanceName, async (b, i, c) =>
        {
            emitter.Emit(new VmHalting(i.Name, force));
            var r = await b.HaltAsync(i, force, c);
            emitter.Emit(r.Success ? new VmHalted(i.Name) : new VmOperationFailed("halt", i.Name, r.Error ?? r.Output));
            return r;
        }, ct);
    }

    public async Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> DestroyAsync(string configPath, string? instanceName, bool force = false, CancellationToken ct = default)
    {
        return await ExecuteOnTargetsAsync(configPath, instanceName, async (b, i, c) =>
        {
            emitter.Emit(new VmDestroying(i.Name, force));
            var r = await b.DestroyAsync(i, force, c);
            emitter.Emit(r.Success ? new VmDestroyed(i.Name) : new VmOperationFailed("destroy", i.Name, r.Error ?? r.Output));
            return r;
        }, ct);
    }

    public Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> ReloadAsync(string configPath, string? instanceName, Abstractions.Options.VosProvisionOptions? options = null, CancellationToken ct = default)
        => ExecuteOnTargetsAsync(configPath, instanceName, async (b, i, c) => { emitter.Emit(new VmReloading(i.Name)); var r = await b.ReloadAsync(i, c); emitter.Emit(new VmReloaded(i.Name)); return r; }, ct);

    public Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> ProvisionAsync(string configPath, string? instanceName, string[]? provisionWith = null, CancellationToken ct = default)
        => ExecuteOnTargetsAsync(configPath, instanceName, async (b, i, c) => { emitter.Emit(new VmProvisioning(i.Name)); var r = await b.ProvisionAsync(i, c); emitter.Emit(new VmProvisioned(i.Name)); return r; }, ct);

    public Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> StatusAsync(string configPath, CancellationToken ct = default)
        => ExecuteOnTargetsAsync(configPath, null, async (b, i, c) => { emitter.Emit(new VmStatusQuerying(i.Name)); var r = await b.StatusAsync(i, c); emitter.Emit(new VmStatusQueried(i.Name, r.Output)); return r; }, ct);

    public Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> SuspendAsync(string configPath, string? instanceName, CancellationToken ct = default)
        => ExecuteOnTargetsAsync(configPath, instanceName, async (b, i, c) => { emitter.Emit(new VmSuspending(i.Name)); var r = await b.SuspendAsync(i, c); emitter.Emit(new VmSuspended(i.Name)); return r; }, ct);

    public Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> ResumeAsync(string configPath, string? instanceName, Abstractions.Options.VosProvisionOptions? options = null, CancellationToken ct = default)
        => ExecuteOnTargetsAsync(configPath, instanceName, async (b, i, c) => { emitter.Emit(new VmResuming(i.Name)); var r = await b.ResumeAsync(i, c); emitter.Emit(new VmResumed(i.Name)); return r; }, ct);

    // SSH
    public async Task<Res.Result<VosActionResult>> SshAsync(string configPath, string instanceName, Abstractions.Options.VosSshOptions? options = null, CancellationToken ct = default)
    {
        var targets = await ResolveInstance(configPath, instanceName, ct);
        if (targets is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        emitter.Emit(new SshConnecting(instanceName));
        var r = await backend.SshAsync(targets, ct);
        emitter.Emit(new SshConnected(instanceName));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> SshCommandAsync(string configPath, string instanceName, string command, bool noTty = false, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        emitter.Emit(new SshCommandExecuting(instanceName, command));
        var r = await backend.SshCommandAsync(target, command, ct);
        emitter.Emit(new SshCommandExecuted(instanceName, r.Output));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> SshConfigAsync(string configPath, string instanceName, string? host = null, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        emitter.Emit(new SshConfigQuerying(instanceName));
        var r = await backend.SshConfigAsync(target, ct);
        emitter.Emit(new SshConfigQueried(instanceName));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> UploadAsync(string configPath, string instanceName, string source, string destination, Abstractions.Options.VosUploadOptions? options = null, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        emitter.Emit(new FileUploading(instanceName, source, destination));
        var r = await backend.UploadAsync(target, source, destination, ct);
        emitter.Emit(new FileUploaded(instanceName, source, destination));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> PortAsync(string configPath, string instanceName, string? guest = null, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        emitter.Emit(new PortQuerying(instanceName));
        var r = await backend.PortAsync(target, ct);
        emitter.Emit(new PortQueried(instanceName));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> PackageAsync(string configPath, string instanceName, Abstractions.Options.VosPackageOptions? options = null, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        emitter.Emit(new PackageCreating(instanceName));
        var r = await backend.PackageAsync(target, ct);
        emitter.Emit(new PackageCreated(instanceName));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> RdpAsync(string configPath, string instanceName, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        return Res.Result<VosActionResult>.Success(await backend.RdpAsync(target, ct));
    }

    public async Task<Res.Result<VosActionResult>> PowershellAsync(string configPath, string instanceName, string? command = null, bool elevated = false, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        return Res.Result<VosActionResult>.Success(await backend.PowershellAsync(target, ct));
    }

    public async Task<Res.Result<VosActionResult>> WinrmAsync(string configPath, string instanceName, string? command = null, Abstractions.Options.VosWinrmOptions? options = null, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        return Res.Result<VosActionResult>.Success(await backend.WinrmAsync(target, ct));
    }

    public async Task<Res.Result<VosActionResult>> WinrmConfigAsync(string configPath, string instanceName, string? host = null, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        return Res.Result<VosActionResult>.Success(await backend.WinrmConfigAsync(target, ct));
    }

    // Snapshots
    public async Task<Res.Result<VosActionResult>> SnapshotSaveAsync(string configPath, string instanceName, string snapshotName, bool force = false, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        emitter.Emit(new SnapshotSaving(instanceName, snapshotName));
        var r = await backend.SnapshotSaveAsync(target, snapshotName, ct);
        emitter.Emit(new SnapshotSaved(instanceName, snapshotName));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> SnapshotRestoreAsync(string configPath, string instanceName, string snapshotName, Abstractions.Options.VosSnapshotRestoreOptions? options = null, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        emitter.Emit(new SnapshotRestoring(instanceName, snapshotName));
        var r = await backend.SnapshotRestoreAsync(target, snapshotName, ct);
        emitter.Emit(new SnapshotRestored(instanceName, snapshotName));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> SnapshotDeleteAsync(string configPath, string instanceName, string snapshotName, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        emitter.Emit(new SnapshotDeleting(instanceName, snapshotName));
        var r = await backend.SnapshotDeleteAsync(target, snapshotName, ct);
        emitter.Emit(new SnapshotDeleted(instanceName, snapshotName));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> SnapshotListAsync(string configPath, string instanceName, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        emitter.Emit(new SnapshotListing(instanceName));
        var r = await backend.SnapshotListAsync(target, ct);
        emitter.Emit(new SnapshotListed(instanceName));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> SnapshotPushAsync(string configPath, string instanceName, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        return Res.Result<VosActionResult>.Success(await backend.SnapshotPushAsync(target, ct));
    }

    public async Task<Res.Result<VosActionResult>> SnapshotPopAsync(string configPath, string instanceName, Abstractions.Options.VosSnapshotPopOptions? options = null, CancellationToken ct = default)
    {
        var target = await ResolveInstance(configPath, instanceName, ct);
        if (target is null) return Res.Result<VosActionResult>.Failure(new System.ComponentModel.DataAnnotations.ValidationResult("Instance not found"));
        return Res.Result<VosActionResult>.Success(await backend.SnapshotPopAsync(target, ct));
    }

    // Diagnostics
    public async Task<Res.Result<VosActionResult>> GlobalStatusAsync(CancellationToken ct = default)
    {
        emitter.Emit(new GlobalStatusQuerying());
        var r = await backend.GlobalStatusAsync(ct);
        emitter.Emit(new GlobalStatusQueried(r.Output));
        return Res.Result<VosActionResult>.Success(r);
    }

    public async Task<Res.Result<VosActionResult>> VagrantValidateAsync(bool ignoreProvider = false, CancellationToken ct = default)
    {
        emitter.Emit(new VagrantValidating());
        var r = await backend.ValidateAsync(ct);
        emitter.Emit(new VagrantValidated(r.Output));
        return Res.Result<VosActionResult>.Success(r);
    }

    // Health check
    public async Task<Res.Result<IReadOnlyList<VosHealthCheckResult>>> CheckAsync(string configPath, string? instanceName = null, CancellationToken ct = default)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return Res.Result<IReadOnlyList<VosHealthCheckResult>>.Failure(loadResult.ValidationResult!);

        var orch = new VosOrchestrator(backend, loadResult.Value!);
        var targets = orch.ResolveTargets(instanceName, instanceName is null);
        var results = new List<VosHealthCheckResult>();

        foreach (var target in targets)
        {
            emitter.Emit(new HealthCheckStarting(target.Name));
            var sshResult = await backend.SshCommandAsync(target, "echo ok", ct);
            var sshOk = sshResult.Success && sshResult.Output.Contains("ok");
            results.Add(new VosHealthCheckResult(target.Name, sshOk, true, true, null));
            emitter.Emit(new HealthCheckCompleted(target.Name, sshOk));
        }

        return Res.Result<IReadOnlyList<VosHealthCheckResult>>.Success(results);
    }

    private async Task<ResolvedInstance?> ResolveInstance(string configPath, string instanceName, CancellationToken ct)
    {
        var loadResult = await fileReader.ReadAsync(configPath, ct);
        if (loadResult.IsFailure) return null;
        var orch = new VosOrchestrator(backend, loadResult.Value!);
        var targets = orch.ResolveTargets(instanceName, false);
        return targets.Count > 0 ? targets[0] : null;
    }
}
