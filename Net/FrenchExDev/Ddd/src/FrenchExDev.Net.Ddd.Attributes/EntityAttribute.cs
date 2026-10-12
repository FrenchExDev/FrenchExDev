namespace FrenchExDev.Net.Ddd.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Ddd.Attributes.Concepts;

    [MetaConcept(typeof(EntityConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class EntityAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        public EntityAttribute(string name) { Name = name; }
    }
}
