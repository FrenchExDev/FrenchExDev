using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos;

/// <summary>
/// Extension point for contributing machine type defaults (e.g. Alpine VirtualBox settings).
/// Contributors modify a <see cref="VosMachineType"/> in-place.
/// </summary>
public interface IMachineTypeContributor
{
    string MachineTypeName { get; }
    void Contribute(VosMachineType machineType);
}
