// Result<T,TError> will be wired in later
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos;

/// <summary>
/// Represents the result of a Vos action.
/// </summary>
public sealed record VosActionResult(bool Success, string Output, string? Error = null);

/// <summary>
/// Error type for Vos operations.
/// </summary>
public abstract record VosError
{
    public sealed record CommandFailed(string Message, int ExitCode) : VosError;
    public sealed record UnsupportedAction(string Action, string Backend) : VosError;
    public sealed record ConfigError(string Message) : VosError;
}

/// <summary>
/// Abstracts over VM backends (currently: Vagrant).
/// Each method maps to a Vos CLI command.
/// </summary>
public interface IVosBackend
{
    string Name { get; }

    Task<VosActionResult> UpAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> HaltAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default);
    Task<VosActionResult> DestroyAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default);
    Task<VosActionResult> ReloadAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> ProvisionAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> StatusAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> SshAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> SshCommandAsync(ResolvedInstance instance, string command, CancellationToken ct = default);
    Task<VosActionResult> SuspendAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> ResumeAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> SnapshotSaveAsync(ResolvedInstance instance, string name, CancellationToken ct = default);
    Task<VosActionResult> SnapshotRestoreAsync(ResolvedInstance instance, string name, CancellationToken ct = default);
    Task<VosActionResult> SnapshotDeleteAsync(ResolvedInstance instance, string name, CancellationToken ct = default);
    Task<VosActionResult> SnapshotListAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> SnapshotPushAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> SnapshotPopAsync(ResolvedInstance instance, CancellationToken ct = default);

    // Additional vagrant commands
    Task<VosActionResult> SshConfigAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> PortAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> GlobalStatusAsync(CancellationToken ct = default);
    Task<VosActionResult> PackageAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> ValidateAsync(CancellationToken ct = default);
    Task<VosActionResult> RdpAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> PowershellAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> WinrmAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> WinrmConfigAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> UploadAsync(ResolvedInstance instance, string source, string destination, CancellationToken ct = default);

    /// <summary>Actions this backend supports.</summary>
    IReadOnlySet<string> SupportedActions { get; }
}
