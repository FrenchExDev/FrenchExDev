namespace FrenchExDev.Net.Dsl.Concepts;

public sealed class MetaReferenceConcept : MetaConcept
{
    public override string Name => "MetaReference";
    public override Type AttributeType => typeof(MetaReferenceAttribute);
}
