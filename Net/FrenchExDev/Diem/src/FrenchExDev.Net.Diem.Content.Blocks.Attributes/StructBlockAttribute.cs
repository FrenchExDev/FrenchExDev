namespace FrenchExDev.Net.Diem.Content.Blocks.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Content.Blocks.Attributes.Concepts;

    [MetaConcept(typeof(StructBlockConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class StructBlockAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Description", "string")]
        public string Description { get; set; } = "";

        [MetaProperty("Icon", "string")]
        public string Icon { get; set; } = "";

        public StructBlockAttribute(string name) { Name = name; }
    }
}
