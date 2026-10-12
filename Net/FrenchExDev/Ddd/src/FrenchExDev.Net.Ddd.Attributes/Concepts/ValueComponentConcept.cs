namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class ValueComponentConcept : MetaConcept
    {
        public override string Name => "ValueComponent";
        public override Type AttributeType => typeof(ValueComponentAttribute);
    }
}
