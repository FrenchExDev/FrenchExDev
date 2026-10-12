namespace FrenchExDev.Net.Entity.Dsl.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class DbContextConcept : MetaConcept
    {
        public override string Name => "DbContext";
        public override Type AttributeType => typeof(DbContextAttribute);
    }
}
