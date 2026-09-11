namespace FrenchExDev.Net.Diem.Content.Parts.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class ContentPartConcept : MetaConcept
    {
        public override string Name => "ContentPart";
        public override Type AttributeType => typeof(ContentPartAttribute);
    }
}
