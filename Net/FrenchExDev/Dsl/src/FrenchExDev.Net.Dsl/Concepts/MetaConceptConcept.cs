namespace FrenchExDev.Net.Dsl.Concepts;

/// <summary>
/// The companion for MetaConcept itself. M3 fixed point.
/// </summary>
public sealed class MetaConceptConcept : MetaConcept
{
    public override string Name => "MetaConcept";
    public override Type AttributeType => typeof(MetaConceptAttribute);
}
