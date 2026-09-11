namespace FrenchExDev.Net.Packer.Bundle.Hcl2;

/// <summary>
/// An entry in the <c>locals { }</c> block: <c>name = expression</c>.
/// Immutable record.
/// </summary>
public sealed record PackerLocal
{
    public required string Name { get; init; }
    public required string Expression { get; init; }
    public bool Sensitive { get; init; }
}
