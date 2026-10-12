namespace FrenchExDev.Net.Entity.Dsl.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Entity.Dsl.Attributes.Concepts;

    [MetaConcept(typeof(ColumnConcept))]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ColumnAttribute : Attribute
    {
        [MetaProperty("Name", "string")]
        public string? Name { get; set; }

        [MetaProperty("TypeName", "string")]
        public string? TypeName { get; set; }

        [MetaProperty("Order", "int")]
        public int Order { get; set; } = -1;
    }
}
