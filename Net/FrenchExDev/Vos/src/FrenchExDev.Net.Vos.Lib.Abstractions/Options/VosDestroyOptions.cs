using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosDestroyOptions
{
    public bool? Force { get; init; }
    public bool? Graceful { get; init; }
    public bool? Parallel { get; init; }
}
