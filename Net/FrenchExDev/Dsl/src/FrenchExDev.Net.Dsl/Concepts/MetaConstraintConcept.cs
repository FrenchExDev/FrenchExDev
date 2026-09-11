namespace FrenchExDev.Net.Dsl.Concepts;

public sealed class MetaConstraintConcept : MetaConcept
{
    public override string Name => "MetaConstraint";
    public override Type AttributeType => typeof(MetaConstraintAttribute);
}
