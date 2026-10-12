namespace FrenchExDev.Net.Vos.Lib.Tests.Fakes;

public sealed class FakeVosBackend : IVosBackend
{
    public string Name => "fake";
    public IReadOnlySet<string> SupportedActions => new HashSet<string>();
    public VosActionResult DefaultResult { get; set; } = new(true, "ok");

    public Task<VosActionResult> UpAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> HaltAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> DestroyAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ReloadAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ProvisionAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> StatusAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> SuspendAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ResumeAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> InitAsync(string box, string outputPath, string? boxVersion = null, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> SshAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> SshCommandAsync(ResolvedInstance instance, string command, CancellationToken ct = default) => Task.FromResult(new VosActionResult(true, $"executed: {command}"));
    public Task<VosActionResult> SshConfigAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> RdpAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> PowershellAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> WinrmAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> WinrmConfigAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> UploadAsync(ResolvedInstance instance, string source, string destination, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> SnapshotSaveAsync(ResolvedInstance instance, string name, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> SnapshotRestoreAsync(ResolvedInstance instance, string name, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> SnapshotDeleteAsync(ResolvedInstance instance, string name, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> SnapshotListAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> SnapshotPushAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> SnapshotPopAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ImageAddAsync(string name, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ImageRemoveAsync(string name, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ImageListAsync(CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ImageUpdateAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ImagePruneAsync(CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ImageOutdatedAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ImageRepackageAsync(string name, string provider, string version, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ImageCreateAsync(string boxName, string outputPath, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosImageBuildResult> ImageBuildAsync(string projectPath, IReadOnlyDictionary<string, string>? variables = null, bool force = false, CancellationToken ct = default)
        => Task.FromResult(new VosImageBuildResult(true, "built", null, []));
    public Task<VosActionResult> PortAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> GlobalStatusAsync(CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> PackageAsync(ResolvedInstance instance, CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> ValidateAsync(CancellationToken ct = default) => Task.FromResult(DefaultResult);
    public Task<VosActionResult> VersionAsync(CancellationToken ct = default) => Task.FromResult(DefaultResult);
}
