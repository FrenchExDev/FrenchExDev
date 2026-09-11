namespace FrenchExDev.Net.Diem.Content.Blocks.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class ListBlockConcept : MetaConcept
    {
        public override string Name => "ListBlock";
        public override Type AttributeType => typeof(ListBlockAttribute);
    }
}
