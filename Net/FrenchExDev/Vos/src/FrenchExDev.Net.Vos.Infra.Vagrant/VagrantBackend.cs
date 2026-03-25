using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.Vagrant;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.Vagrant;

/// <summary>
/// <see cref="IVosBackend"/> implementation that executes real Vagrant CLI commands
/// via the <see cref="VagrantClient"/> BinaryWrapper.
/// </summary>
public sealed class VagrantBackend : IVosBackend
{
    private readonly BinaryBinding _binding;
    private readonly CommandExecutor _executor;
    private readonly VagrantClient _client;

    public VagrantBackend(string executablePath = "vagrant")
    {
        _binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("vagrant"),
            ExecutablePath = executablePath
        };
        _executor = new CommandExecutor(new DictionaryBinaryResolver(_binding));
        _client = FrenchExDev.Net.Vagrant.Vagrant.Create(_binding);
    }

    public string Name => "vagrant";

    private static readonly HashSet<string> Supported = new()
    {
        "up", "halt", "destroy", "reload", "provision", "status",
        "ssh", "ssh-command", "ssh-config", "suspend", "resume",
        "snapshot-save", "snapshot-restore", "snapshot-delete", "snapshot-list", "snapshot-push", "snapshot-pop",
        "port", "global-status", "package", "validate", "rdp",
        "powershell", "winrm", "winrm-config", "upload"
    };

    public IReadOnlySet<string> SupportedActions => Supported;

    public async Task<VosActionResult> UpAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.UpAsync(b => b.WithProvider(instance.ProviderType));
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> HaltAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default)
    {
        var command = await _client.HaltAsync(b => { if (force) b.WithForce(true); });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> DestroyAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default)
    {
        var command = await _client.DestroyAsync(b => { if (force) b.WithForce(true); });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> ReloadAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.ReloadAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> ProvisionAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.ProvisionAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> StatusAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.StatusAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> SshAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.SshAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> SshCommandAsync(ResolvedInstance instance, string command, CancellationToken ct = default)
    {
        var cmd = await _client.SshAsync(b => b.WithCommand(command));
        return await Execute(cmd, ct);
    }

    public async Task<VosActionResult> SuspendAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.SuspendAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> ResumeAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.ResumeAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> SnapshotSaveAsync(ResolvedInstance instance, string name, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.SaveAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> SnapshotRestoreAsync(ResolvedInstance instance, string name, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.RestoreAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> SnapshotDeleteAsync(ResolvedInstance instance, string name, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.DeleteAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> SnapshotListAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.ListAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> SnapshotPushAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.PushAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> SnapshotPopAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.PopAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> SshConfigAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.SshConfigAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> PortAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.PortAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> GlobalStatusAsync(CancellationToken ct = default)
    {
        var command = await _client.GlobalStatusAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> PackageAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.PackageAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> ValidateAsync(CancellationToken ct = default)
    {
        var command = await _client.ValidateAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> RdpAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.RdpAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> PowershellAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.PowershellAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> WinrmAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.WinrmAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> WinrmConfigAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.WinrmConfigAsync(b => { });
        return await Execute(command, ct);
    }

    public async Task<VosActionResult> UploadAsync(ResolvedInstance instance, string source, string destination, CancellationToken ct = default)
    {
        var command = await _client.UploadAsync(b => { });
        return await Execute(command, ct);
    }

    private async Task<VosActionResult> Execute(ICliCommand command, CancellationToken ct)
    {
        var result = await _executor.ExecuteAsync(_binding.Identifier, command, ct);
        return result.Match(
            onSuccess: output => new VosActionResult(
                output.ExitCode == 0,
                output.StandardOutput,
                output.ExitCode != 0 ? output.StandardError : null),
            onFailure: error => new VosActionResult(false, "", error.ToString()));
    }
}
