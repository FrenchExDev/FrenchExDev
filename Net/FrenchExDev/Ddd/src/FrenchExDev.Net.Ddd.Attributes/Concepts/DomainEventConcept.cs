namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class DomainEventConcept : MetaConcept
    {
        public override string Name => "DomainEvent";
        public override Type AttributeType => typeof(DomainEventAttribute);
    }
}
