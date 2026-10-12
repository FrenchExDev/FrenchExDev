namespace FrenchExDev.Net.Diem.Pages.Routing.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Pages.Routing.Attributes.Concepts;

    [MetaConcept(typeof(BoundEntityConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class BoundEntityAttribute : Attribute
    {
        public Type EntityType { get; }

        [MetaProperty("UrlPattern", "string")]
        public string UrlPattern { get; set; } = "";

        public BoundEntityAttribute(Type entityType) { EntityType = entityType; }
    }
}
