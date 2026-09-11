namespace FrenchExDev.Net.Diem.Pages.Layouts.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Pages.Layouts.Attributes.Concepts;

    [MetaConcept(typeof(AreaConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class AreaAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        public AreaAttribute(string name) { Name = name; }
    }
}
