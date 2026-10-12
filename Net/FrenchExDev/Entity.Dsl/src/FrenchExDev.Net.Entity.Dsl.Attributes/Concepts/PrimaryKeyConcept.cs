namespace FrenchExDev.Net.Entity.Dsl.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class PrimaryKeyConcept : MetaConcept
    {
        public override string Name => "PrimaryKey";
        public override Type AttributeType => typeof(PrimaryKeyAttribute);
    }
}
