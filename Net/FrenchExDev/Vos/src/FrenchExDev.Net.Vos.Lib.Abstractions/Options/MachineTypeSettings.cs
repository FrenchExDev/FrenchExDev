using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class MachineTypeSettings
{
    public int? Memory { get; init; }
    public int? Cpus { get; init; }
    public int? VideoMemory { get; init; }
    public bool? NestedVirt { get; init; }
    public bool? SataSsd { get; init; }
    public bool? Gui { get; init; }
    public string? NicPromisc { get; init; }
    public bool? NoLinkedClones { get; init; }
}
