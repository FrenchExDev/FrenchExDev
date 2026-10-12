namespace FrenchExDev.Net.Diem.Content.Blocks.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class BlockFieldConcept : MetaConcept
    {
        public override string Name => "BlockField";
        public override Type AttributeType => typeof(BlockFieldAttribute);
    }
}
