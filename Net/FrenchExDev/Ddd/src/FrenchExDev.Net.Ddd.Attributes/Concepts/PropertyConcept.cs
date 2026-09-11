namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class PropertyConcept : MetaConcept
    {
        public override string Name => "Property";
        public override Type AttributeType => typeof(PropertyAttribute);
    }
}
