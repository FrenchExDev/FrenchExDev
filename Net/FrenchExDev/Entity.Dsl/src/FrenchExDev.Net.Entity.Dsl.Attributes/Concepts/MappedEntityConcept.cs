namespace FrenchExDev.Net.Entity.Dsl.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class MappedEntityConcept : MetaConcept
    {
        public override string Name => "MappedEntity";
        public override Type AttributeType => typeof(MappedEntityAttribute);
    }
}
