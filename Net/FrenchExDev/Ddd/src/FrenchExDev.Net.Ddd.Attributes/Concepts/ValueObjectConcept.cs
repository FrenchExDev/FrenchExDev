namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class ValueObjectConcept : MetaConcept
    {
        public override string Name => "ValueObject";
        public override Type AttributeType => typeof(ValueObjectAttribute);
    }
}
