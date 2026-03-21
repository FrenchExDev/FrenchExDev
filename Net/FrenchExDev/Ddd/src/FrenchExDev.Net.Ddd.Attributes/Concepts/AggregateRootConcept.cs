namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using System.Collections.Generic;
    using FrenchExDev.Net.Dsl;

    public sealed class AggregateRootConcept : MetaConcept
    {
        public override string Name => "AggregateRoot";
        public override Type AttributeType => typeof(AggregateRootAttribute);
        public override IReadOnlyList<Type> SuperTypes => new[] { typeof(EntityConcept) };

        public override bool CanContain(MetaConcept child)
        {
            return child is EntityConcept || child is ValueObjectConcept;
        }
    }
}
