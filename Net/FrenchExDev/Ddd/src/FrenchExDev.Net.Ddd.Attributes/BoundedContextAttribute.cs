namespace FrenchExDev.Net.Ddd.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Ddd.Attributes.Concepts;

    [MetaConcept(typeof(BoundedContextConcept))]
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class BoundedContextAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Description", "string")]
        public string? Description { get; set; }

        public BoundedContextAttribute(string name) { Name = name; }
    }
}
