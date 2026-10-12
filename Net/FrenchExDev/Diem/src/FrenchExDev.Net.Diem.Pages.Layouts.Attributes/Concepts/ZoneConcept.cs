namespace FrenchExDev.Net.Diem.Pages.Layouts.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class ZoneConcept : MetaConcept
    {
        public override string Name => "Zone";
        public override Type AttributeType => typeof(ZoneAttribute);
    }
}
