using FrenchExDev.Net.BinaryWrapper;

namespace FrenchExDev.Net.Vagrant;

public sealed record VagrantUpResult(
    IReadOnlyList<string> MachinesReady,
    IReadOnlyList<string> Errors,
    bool Success);

/// <summary>
/// Collects <see cref="VagrantEvent"/> instances from a vagrant up run
/// and produces an aggregated <see cref="VagrantUpResult"/>.
/// </summary>
public sealed class VagrantUpCollector : IResultCollector<VagrantEvent, VagrantUpResult>
{
    private readonly List<string> _machinesReady = [];
    private readonly List<string> _errors = [];
    private bool _hasFailure;

    public void OnEvent(VagrantEvent @event)
    {
        switch (@event)
        {
            case VagrantActionCompleted { Success: true } completed:
                _machinesReady.Add(completed.MachineName);
                break;

            case VagrantActionCompleted { Success: false }:
                _hasFailure = true;
                break;

            case VagrantMachineError error:
                _errors.Add($"{error.MachineName}: {error.Message}");
                break;
        }
    }

    public VagrantUpResult Complete() =>
        new(_machinesReady.AsReadOnly(), _errors.AsReadOnly(), !_hasFailure && _errors.Count == 0);
}
