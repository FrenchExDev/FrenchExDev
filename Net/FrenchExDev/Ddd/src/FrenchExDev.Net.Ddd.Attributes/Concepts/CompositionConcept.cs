namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class CompositionConcept : MetaConcept
    {
        public override string Name => "Composition";
        public override Type AttributeType => typeof(CompositionAttribute);
    }
}
