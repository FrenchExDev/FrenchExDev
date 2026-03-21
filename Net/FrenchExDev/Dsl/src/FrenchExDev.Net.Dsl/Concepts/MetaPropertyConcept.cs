namespace FrenchExDev.Net.Dsl.Concepts;

public sealed class MetaPropertyConcept : MetaConcept
{
    public override string Name => "MetaProperty";
    public override Type AttributeType => typeof(MetaPropertyAttribute);
}
