namespace FrenchExDev.Net.Diem.Pages.Routing.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class BoundEntityConcept : MetaConcept
    {
        public override string Name => "BoundEntity";
        public override Type AttributeType => typeof(BoundEntityAttribute);
    }
}
