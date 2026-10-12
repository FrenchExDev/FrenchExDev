using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Lib.Abstractions;

public interface IVosVmService
{
    // VM lifecycle
    Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> UpAsync(string configPath, string? instanceName, Options.VosUpOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> HaltAsync(string configPath, string? instanceName, bool force = false, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> DestroyAsync(string configPath, string? instanceName, bool force = false, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> ReloadAsync(string configPath, string? instanceName, Options.VosProvisionOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> ProvisionAsync(string configPath, string? instanceName, string[]? provisionWith = null, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> StatusAsync(string configPath, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> SuspendAsync(string configPath, string? instanceName, CancellationToken ct = default);
    Task<Res.Result<IReadOnlyList<(string Name, VosActionResult Result)>>> ResumeAsync(string configPath, string? instanceName, Options.VosProvisionOptions? options = null, CancellationToken ct = default);

    // SSH / Remote
    Task<Res.Result<VosActionResult>> SshAsync(string configPath, string instanceName, Options.VosSshOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> SshCommandAsync(string configPath, string instanceName, string command, bool noTty = false, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> SshConfigAsync(string configPath, string instanceName, string? host = null, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> UploadAsync(string configPath, string instanceName, string source, string destination, Options.VosUploadOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> PortAsync(string configPath, string instanceName, string? guest = null, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> PackageAsync(string configPath, string instanceName, Options.VosPackageOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> RdpAsync(string configPath, string instanceName, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> PowershellAsync(string configPath, string instanceName, string? command = null, bool elevated = false, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> WinrmAsync(string configPath, string instanceName, string? command = null, Options.VosWinrmOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> WinrmConfigAsync(string configPath, string instanceName, string? host = null, CancellationToken ct = default);

    // Snapshots
    Task<Res.Result<VosActionResult>> SnapshotSaveAsync(string configPath, string instanceName, string snapshotName, bool force = false, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> SnapshotRestoreAsync(string configPath, string instanceName, string snapshotName, Options.VosSnapshotRestoreOptions? options = null, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> SnapshotDeleteAsync(string configPath, string instanceName, string snapshotName, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> SnapshotListAsync(string configPath, string instanceName, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> SnapshotPushAsync(string configPath, string instanceName, CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> SnapshotPopAsync(string configPath, string instanceName, Options.VosSnapshotPopOptions? options = null, CancellationToken ct = default);

    // Diagnostics
    Task<Res.Result<VosActionResult>> GlobalStatusAsync(CancellationToken ct = default);
    Task<Res.Result<VosActionResult>> VagrantValidateAsync(bool ignoreProvider = false, CancellationToken ct = default);

    // Health check
    Task<Res.Result<IReadOnlyList<VosHealthCheckResult>>> CheckAsync(string configPath, string? instanceName = null, CancellationToken ct = default);
}

public sealed record VosHealthCheckResult(string InstanceName, bool SshOk, bool HostnameOk, bool IpOk, bool? DockerOk);
