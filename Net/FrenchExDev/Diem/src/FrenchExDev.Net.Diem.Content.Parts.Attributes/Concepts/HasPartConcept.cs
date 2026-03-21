namespace FrenchExDev.Net.Diem.Content.Parts.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class HasPartConcept : MetaConcept
    {
        public override string Name => "HasPart";
        public override Type AttributeType => typeof(HasPartAttribute);
    }
}
