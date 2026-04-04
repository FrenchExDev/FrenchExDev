namespace FrenchExDev.Net.Entity.Dsl.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Entity.Dsl.Attributes.Concepts;

    /// <summary>
    /// Marks a property as the primary key (or part of a composite key).
    /// Standalone — no DDD dependency. The Ddd.Entity.Dsl bridge maps [EntityId] to this.
    /// </summary>
    [MetaConcept(typeof(PrimaryKeyConcept))]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class PrimaryKeyAttribute : Attribute
    {
        [MetaProperty("Order", "int")]
        public int Order { get; set; } = 0;

        [MetaProperty("ValueGenerated", "ValueGeneration")]
        public ValueGeneration ValueGenerated { get; set; } = ValueGeneration.OnAdd;
    }
}
