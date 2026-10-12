using FrenchExDev.Net.Builder.Attributes;

namespace FrenchExDev.Net.Vos.Lib.Abstractions.Options;

[Builder]
public sealed class VosUploadOptions
{
    public bool? Temporary { get; init; }
    public bool? Compress { get; init; }
    public string? CompressionType { get; init; }
}
