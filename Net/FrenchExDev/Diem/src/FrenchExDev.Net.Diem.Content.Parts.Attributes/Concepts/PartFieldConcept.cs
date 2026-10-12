namespace FrenchExDev.Net.Diem.Content.Parts.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class PartFieldConcept : MetaConcept
    {
        public override string Name => "PartField";
        public override Type AttributeType => typeof(PartFieldAttribute);
    }
}
