namespace FrenchExDev.Net.Ddd.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Ddd.Attributes.Concepts;

    [MetaConcept(typeof(ValueComponentConcept))]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ValueComponentAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Type", "string", Required = true)]
        public string Type { get; set; }

        [MetaProperty("Required", "bool")]
        public bool Required { get; set; }

        public ValueComponentAttribute(string name, string type) { Name = name; Type = type; }
    }
}
