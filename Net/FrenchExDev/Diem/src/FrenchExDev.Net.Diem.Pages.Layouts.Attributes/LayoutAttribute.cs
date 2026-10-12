namespace FrenchExDev.Net.Diem.Pages.Layouts.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Pages.Layouts.Attributes.Concepts;

    [MetaConcept(typeof(LayoutConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class LayoutAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Description", "string")]
        public string Description { get; set; } = "";

        public LayoutAttribute(string name) { Name = name; }
    }
}
