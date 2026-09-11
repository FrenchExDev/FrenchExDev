namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class EntityConcept : MetaConcept
    {
        public override string Name => "Entity";
        public override Type AttributeType => typeof(EntityAttribute);
    }
}
