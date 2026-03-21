using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.Podman;

public sealed class PodmanMachineBackend : IVosBackend
{
    public string Name => "podman";

    private static readonly HashSet<string> Supported = new()
    {
        "up", "halt", "destroy", "reload", "status", "ssh", "ssh-command"
    };

    public IReadOnlySet<string> SupportedActions => Supported;

    public Task<VosActionResult> UpAsync(ResolvedInstance instance, CancellationToken ct) => Ok($"podman machine start {instance.Name}");
    public Task<VosActionResult> HaltAsync(ResolvedInstance instance, bool force, CancellationToken ct) => Ok($"podman machine stop {instance.Name}");
    public Task<VosActionResult> DestroyAsync(ResolvedInstance instance, bool force, CancellationToken ct) => Ok($"podman machine rm {instance.Name}");
    public Task<VosActionResult> ReloadAsync(ResolvedInstance instance, CancellationToken ct) => Ok($"podman machine stop && start {instance.Name}");
    public Task<VosActionResult> ProvisionAsync(ResolvedInstance instance, CancellationToken ct) => Unsupported("provision");
    public Task<VosActionResult> StatusAsync(ResolvedInstance instance, CancellationToken ct) => Ok("podman machine list");
    public Task<VosActionResult> SshAsync(ResolvedInstance instance, CancellationToken ct) => Ok($"podman machine ssh {instance.Name}");
    public Task<VosActionResult> SshCommandAsync(ResolvedInstance instance, string command, CancellationToken ct) => Ok($"podman machine ssh {instance.Name} {command}");
    public Task<VosActionResult> SuspendAsync(ResolvedInstance instance, CancellationToken ct) => Unsupported("suspend");
    public Task<VosActionResult> ResumeAsync(ResolvedInstance instance, CancellationToken ct) => Unsupported("resume");
    public Task<VosActionResult> SnapshotSaveAsync(ResolvedInstance instance, string name, CancellationToken ct) => Unsupported("snapshot-save");
    public Task<VosActionResult> SnapshotRestoreAsync(ResolvedInstance instance, string name, CancellationToken ct) => Unsupported("snapshot-restore");

    private static Task<VosActionResult> Ok(string output) => Task.FromResult(new VosActionResult(true, output));
    private Task<VosActionResult> Unsupported(string action) => Task.FromResult(new VosActionResult(false, "", $"Action '{action}' is not supported by the {Name} backend"));
}
