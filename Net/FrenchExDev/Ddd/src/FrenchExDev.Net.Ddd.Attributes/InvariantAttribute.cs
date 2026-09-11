namespace FrenchExDev.Net.Ddd.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Ddd.Attributes.Concepts;

    [MetaConcept(typeof(InvariantConcept))]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class InvariantAttribute : Attribute
    {
        [MetaProperty("Description", "string", Required = true)]
        public string Description { get; }

        public InvariantAttribute(string description) { Description = description; }
    }
}
