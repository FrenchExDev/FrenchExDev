namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class EntityIdConcept : MetaConcept
    {
        public override string Name => "EntityId";
        public override Type AttributeType => typeof(EntityIdAttribute);
    }
}
