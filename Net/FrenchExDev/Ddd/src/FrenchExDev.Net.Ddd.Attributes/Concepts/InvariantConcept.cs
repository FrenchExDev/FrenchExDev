namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class InvariantConcept : MetaConcept
    {
        public override string Name => "Invariant";
        public override Type AttributeType => typeof(InvariantAttribute);
    }
}
