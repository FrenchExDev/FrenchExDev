using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.Vagrant;

public sealed class VagrantBackend : IVosBackend
{
    public string Name => "vagrant";

    private static readonly HashSet<string> Supported = new()
    {
        "up", "halt", "destroy", "reload", "provision", "status",
        "ssh", "ssh-command", "suspend", "resume",
        "snapshot-save", "snapshot-restore"
    };

    public IReadOnlySet<string> SupportedActions => Supported;

    public Task<VosActionResult> UpAsync(ResolvedInstance instance, CancellationToken ct) => Ok($"vagrant up {instance.Name}");
    public Task<VosActionResult> HaltAsync(ResolvedInstance instance, bool force, CancellationToken ct) => Ok($"vagrant halt {instance.Name}");
    public Task<VosActionResult> DestroyAsync(ResolvedInstance instance, bool force, CancellationToken ct) => Ok($"vagrant destroy {instance.Name}");
    public Task<VosActionResult> ReloadAsync(ResolvedInstance instance, CancellationToken ct) => Ok($"vagrant reload {instance.Name}");
    public Task<VosActionResult> ProvisionAsync(ResolvedInstance instance, CancellationToken ct) => Ok($"vagrant provision {instance.Name}");
    public Task<VosActionResult> StatusAsync(ResolvedInstance instance, CancellationToken ct) => Ok($"vagrant status {instance.Name}");
    public Task<VosActionResult> SshAsync(ResolvedInstance instance, CancellationToken ct) => Ok($"vagrant ssh {instance.Name}");
    public Task<VosActionResult> SshCommandAsync(ResolvedInstance instance, string command, CancellationToken ct) => Ok($"vagrant ssh {instance.Name} -c '{command}'");
    public Task<VosActionResult> SuspendAsync(ResolvedInstance instance, CancellationToken ct) => Ok($"vagrant suspend {instance.Name}");
    public Task<VosActionResult> ResumeAsync(ResolvedInstance instance, CancellationToken ct) => Ok($"vagrant resume {instance.Name}");
    public Task<VosActionResult> SnapshotSaveAsync(ResolvedInstance instance, string name, CancellationToken ct) => Ok($"vagrant snapshot save {instance.Name} {name}");
    public Task<VosActionResult> SnapshotRestoreAsync(ResolvedInstance instance, string name, CancellationToken ct) => Ok($"vagrant snapshot restore {instance.Name} {name}");

    private static Task<VosActionResult> Ok(string output) => Task.FromResult(new VosActionResult(true, output));
}
