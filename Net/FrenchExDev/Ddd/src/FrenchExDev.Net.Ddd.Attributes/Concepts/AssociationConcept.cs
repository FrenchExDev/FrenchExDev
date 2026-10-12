namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class AssociationConcept : MetaConcept
    {
        public override string Name => "Association";
        public override Type AttributeType => typeof(AssociationAttribute);
    }
}
