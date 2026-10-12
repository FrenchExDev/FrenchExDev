namespace FrenchExDev.Net.Vos.Config;

/// <summary>
/// Root configuration model for a Vos project.
/// Deserialized from <c>config-vos.yaml</c>.
/// </summary>
public sealed class VosConfig
{
    /// <summary>Backend to use (currently: "vagrant").</summary>
    public string Backend { get; set; } = "vagrant";

    /// <summary>Naming format for vagrant machine names (e.g. "{machine}-{index:D2}").</summary>
    public string? Format { get; set; }

    /// <summary>Machine type templates (reusable configurations).</summary>
    public Dictionary<string, VosMachineType> MachineTypes { get; set; } = new();

    /// <summary>Machine declarations (reference a machine type, define instances).</summary>
    public Dictionary<string, VosMachine> Machines { get; set; } = new();
}
