namespace FrenchExDev.Net.Diem.Content.Blocks.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Content.Blocks.Attributes.Concepts;

    [MetaConcept(typeof(ListBlockConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ListBlockAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Description", "string")]
        public string Description { get; set; } = "";

        public Type ItemType { get; set; } = typeof(object);

        public ListBlockAttribute(string name) { Name = name; }
    }
}
