namespace FrenchExDev.Net.Diem.Pages.Layouts.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class AreaConcept : MetaConcept
    {
        public override string Name => "Area";
        public override Type AttributeType => typeof(AreaAttribute);
    }
}
