namespace FrenchExDev.Net.Diem.Content.StreamFields.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class StreamFieldConcept : MetaConcept
    {
        public override string Name => "StreamField";
        public override Type AttributeType => typeof(StreamFieldAttribute);
    }
}
