using System.Management.Automation;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.Infra.Vagrant;
using FrenchExDev.Net.Vos.Infra.Podman;

namespace FrenchExDev.Net.Vos.Infra.PowerShell;

/// <summary>
/// Base class for Vos PowerShell cmdlets.
/// Resolves the backend and instance from parameters, then delegates to the subclass.
/// </summary>
public abstract class VosCmdletBase : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public string MachineName { get; set; } = "";

    [Parameter]
    [ValidateSet("vagrant", "podman")]
    public string Backend { get; set; } = "vagrant";

    protected abstract Task<VosActionResult> ExecuteActionAsync(
        IVosBackend backend, ResolvedInstance instance, CancellationToken ct);

    protected override void ProcessRecord()
    {
        var backend = ResolveBackend();
        var instance = new ResolvedInstance
        {
            Name = MachineName,
            Hostname = MachineName,
            Memory = 1024,
            Cpus = 2,
            VideoMemory = 64,
            ProviderType = Backend == "vagrant" ? "virtualbox" : "qemu"
        };

        var task = ExecuteActionAsync(backend, instance, CancellationToken.None);
        var result = task.GetAwaiter().GetResult();

        WriteObject(result);

        if (!result.Success && result.Error is not null)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException(result.Error),
                "VosActionFailed",
                ErrorCategory.OperationStopped,
                MachineName));
        }
    }

    private IVosBackend ResolveBackend() => Backend switch
    {
        "vagrant" => new VagrantBackend(),
        "podman" => new PodmanMachineBackend(),
        _ => throw new PSArgumentException($"Unknown backend: {Backend}", nameof(Backend))
    };
}
