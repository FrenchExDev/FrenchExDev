namespace FrenchExDev.Net.Entity.Dsl.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class NavigationPropertyConcept : MetaConcept
    {
        public override string Name => "NavigationProperty";
        public override Type AttributeType => typeof(NavigationPropertyAttribute);
    }
}
