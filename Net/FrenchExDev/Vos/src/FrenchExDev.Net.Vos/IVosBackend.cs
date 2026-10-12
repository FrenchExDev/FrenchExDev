using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos;

/// <summary>
/// Represents the result of a Vos action.
/// </summary>
public record VosActionResult(bool Success, string Output, string? Error = null);

/// <summary>
/// Result of an image build operation, with artifact details.
/// </summary>
public sealed record VosImageBuildResult(
    bool Success, string Output, string? Error,
    IReadOnlyList<VosArtifact> Artifacts)
    : VosActionResult(Success, Output, Error);

/// <summary>
/// An artifact produced by an image build (e.g., a .box file).
/// </summary>
public sealed record VosArtifact(string BuilderType, string Name, string? Path);

/// <summary>
/// Error type for Vos operations.
/// </summary>
public abstract record VosError
{
    public sealed record CommandFailed(string Message, int ExitCode) : VosError;
    public sealed record UnsupportedAction(string Action, string Backend) : VosError;
    public sealed record ConfigError(string Message) : VosError;
    public sealed record BuildFailed(string Message, IReadOnlyList<string> Errors) : VosError;
}

/// <summary>
/// Abstracts over VM backends (currently: Vagrant).
/// Each method maps to a Vos CLI command.
/// </summary>
public interface IVosBackend
{
    // ── Identity ─────────────────────────────────────────────────────
    string Name { get; }
    IReadOnlySet<string> SupportedActions { get; }

    // ── VM Lifecycle ─────────────────────────────────────────────────
    Task<VosActionResult> UpAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> HaltAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default);
    Task<VosActionResult> DestroyAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default);
    Task<VosActionResult> ReloadAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> ProvisionAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> StatusAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> SuspendAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> ResumeAsync(ResolvedInstance instance, CancellationToken ct = default);

    // ── Project Scaffolding ──────────────────────────────────────────

    /// <summary>
    /// Initializes a new VM project for the given box (e.g., creates a Vagrantfile).
    /// Maps to "vos init" / "vagrant init".
    /// </summary>
    Task<VosActionResult> InitAsync(string box, string outputPath, string? boxVersion = null, CancellationToken ct = default);

    // ── SSH / Remote Access ──────────────────────────────────────────
    Task<VosActionResult> SshAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> SshCommandAsync(ResolvedInstance instance, string command, CancellationToken ct = default);
    Task<VosActionResult> SshConfigAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> RdpAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> PowershellAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> WinrmAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> WinrmConfigAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> UploadAsync(ResolvedInstance instance, string source, string destination, CancellationToken ct = default);

    // ── Snapshots ────────────────────────────────────────────────────
    Task<VosActionResult> SnapshotSaveAsync(ResolvedInstance instance, string name, CancellationToken ct = default);
    Task<VosActionResult> SnapshotRestoreAsync(ResolvedInstance instance, string name, CancellationToken ct = default);
    Task<VosActionResult> SnapshotDeleteAsync(ResolvedInstance instance, string name, CancellationToken ct = default);
    Task<VosActionResult> SnapshotListAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> SnapshotPushAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> SnapshotPopAsync(ResolvedInstance instance, CancellationToken ct = default);

    // ── Image Management ─────────────────────────────────────────────
    Task<VosActionResult> ImageAddAsync(string name, CancellationToken ct = default);
    Task<VosActionResult> ImageRemoveAsync(string name, CancellationToken ct = default);
    Task<VosActionResult> ImageListAsync(CancellationToken ct = default);
    Task<VosActionResult> ImageUpdateAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> ImagePruneAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if the box used by the instance has a newer version available.
    /// Maps to "vos box outdated" / "vagrant box outdated".
    /// </summary>
    Task<VosActionResult> ImageOutdatedAsync(ResolvedInstance instance, CancellationToken ct = default);

    /// <summary>
    /// Repackages an installed box to a .box file.
    /// Maps to "vos box repackage" / "vagrant box repackage".
    /// </summary>
    Task<VosActionResult> ImageRepackageAsync(string name, string provider, string version, CancellationToken ct = default);

    // ── Image Build (Packer integration) ─────────────────────────────

    /// <summary>
    /// Scaffolds a new Packer project for building a box image.
    /// Maps to "vos box init".
    /// </summary>
    Task<VosActionResult> ImageCreateAsync(string boxName, string outputPath, CancellationToken ct = default);

    /// <summary>
    /// Builds an image from a Packer project. Orchestrates packer init + packer build.
    /// Maps to "vos box build". Returns <see cref="VosImageBuildResult"/> with artifact paths.
    /// </summary>
    Task<VosImageBuildResult> ImageBuildAsync(string projectPath, IReadOnlyDictionary<string, string>? variables = null, bool force = false, CancellationToken ct = default);

    // ── Networking ───────────────────────────────────────────────────
    Task<VosActionResult> PortAsync(ResolvedInstance instance, CancellationToken ct = default);

    // ── Global / Diagnostics ─────────────────────────────────────────
    Task<VosActionResult> GlobalStatusAsync(CancellationToken ct = default);
    Task<VosActionResult> PackageAsync(ResolvedInstance instance, CancellationToken ct = default);
    Task<VosActionResult> ValidateAsync(CancellationToken ct = default);
    Task<VosActionResult> VersionAsync(CancellationToken ct = default);
}
