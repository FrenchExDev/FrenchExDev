namespace FrenchExDev.Net.Entity.Dsl.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class TableConcept : MetaConcept
    {
        public override string Name => "Table";
        public override Type AttributeType => typeof(TableAttribute);
    }
}
