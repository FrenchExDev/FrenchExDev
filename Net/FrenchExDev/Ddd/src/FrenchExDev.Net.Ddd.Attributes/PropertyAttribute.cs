namespace FrenchExDev.Net.Ddd.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Ddd.Attributes.Concepts;

    [MetaConcept(typeof(PropertyConcept))]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class PropertyAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Required", "bool")]
        public bool Required { get; set; }

        [MetaProperty("MaxLength", "int")]
        public int MaxLength { get; set; }

        public PropertyAttribute(string name) { Name = name; }
    }
}
