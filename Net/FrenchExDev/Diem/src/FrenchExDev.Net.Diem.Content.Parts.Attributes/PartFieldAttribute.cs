namespace FrenchExDev.Net.Diem.Content.Parts.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Content.Parts.Attributes.Concepts;

    [MetaConcept(typeof(PartFieldConcept))]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class PartFieldAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("DisplayName", "string")]
        public string DisplayName { get; set; } = "";

        [MetaProperty("Required", "bool")]
        public bool Required { get; set; }

        [MetaProperty("MaxLength", "int")]
        public int MaxLength { get; set; }

        [MetaProperty("HelpText", "string")]
        public string HelpText { get; set; } = "";

        public PartFieldAttribute(string name) { Name = name; }
    }
}
