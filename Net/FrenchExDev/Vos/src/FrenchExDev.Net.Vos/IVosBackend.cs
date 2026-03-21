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
/// Abstracts over VM backends (Vagrant, Podman machine).
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

    /// <summary>Actions this backend supports. Unsupported actions return <see cref="VosError.UnsupportedAction"/>.</summary>
    IReadOnlySet<string> SupportedActions { get; }
}
