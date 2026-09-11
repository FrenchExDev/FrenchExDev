using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosBoxAddOptions
{
    public bool? Force { get; init; }
    public bool? Insecure { get; init; }
    public bool? Clean { get; init; }
    public bool? LocationTrusted { get; init; }
    public string? Cacert { get; init; }
    public string? Capath { get; init; }
    public string? Cert { get; init; }
    public string? Provider { get; init; }
    public string? BoxVersion { get; init; }
    public string? Checksum { get; init; }
    public string? ChecksumType { get; init; }
    public string? Name { get; init; }
    public string? Architecture { get; init; }
}
