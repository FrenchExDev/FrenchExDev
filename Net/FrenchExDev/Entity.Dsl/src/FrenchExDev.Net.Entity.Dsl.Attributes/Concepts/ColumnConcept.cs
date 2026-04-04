namespace FrenchExDev.Net.Entity.Dsl.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class ColumnConcept : MetaConcept
    {
        public override string Name => "Column";
        public override Type AttributeType => typeof(ColumnAttribute);
    }
}
