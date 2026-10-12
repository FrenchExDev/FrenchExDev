namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class BoundedContextConcept : MetaConcept
    {
        public override string Name => "BoundedContext";
        public override Type AttributeType => typeof(BoundedContextAttribute);
    }
}
