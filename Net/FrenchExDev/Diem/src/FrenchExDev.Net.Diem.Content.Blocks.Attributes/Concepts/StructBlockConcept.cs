namespace FrenchExDev.Net.Diem.Content.Blocks.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class StructBlockConcept : MetaConcept
    {
        public override string Name => "StructBlock";
        public override Type AttributeType => typeof(StructBlockAttribute);
    }
}
