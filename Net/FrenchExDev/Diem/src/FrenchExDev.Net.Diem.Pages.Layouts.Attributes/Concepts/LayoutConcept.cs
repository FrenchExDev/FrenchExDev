namespace FrenchExDev.Net.Diem.Pages.Layouts.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class LayoutConcept : MetaConcept
    {
        public override string Name => "Layout";
        public override Type AttributeType => typeof(LayoutAttribute);
    }
}
