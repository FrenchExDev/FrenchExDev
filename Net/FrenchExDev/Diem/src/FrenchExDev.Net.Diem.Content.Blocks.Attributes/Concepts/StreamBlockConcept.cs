namespace FrenchExDev.Net.Diem.Content.Blocks.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class StreamBlockConcept : MetaConcept
    {
        public override string Name => "StreamBlock";
        public override Type AttributeType => typeof(StreamBlockAttribute);
    }
}
