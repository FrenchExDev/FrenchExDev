namespace FrenchExDev.Net.Diem.Pages.Layouts.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Pages.Layouts.Attributes.Concepts;

    [MetaConcept(typeof(ZoneConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ZoneAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("MaxWidgets", "int")]
        public int MaxWidgets { get; set; } = 10;

        public ZoneAttribute(string name) { Name = name; }
    }
}
