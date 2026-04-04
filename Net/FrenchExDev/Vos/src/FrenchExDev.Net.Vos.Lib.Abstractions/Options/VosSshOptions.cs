using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosSshOptions
{
    public bool? Plain { get; init; }
    public bool? NoTty { get; init; }
}
