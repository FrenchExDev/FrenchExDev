namespace FrenchExDev.Net.Entity.Dsl.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Entity.Dsl.Attributes.Concepts;

    /// <summary>
    /// Marks a property as the primary key (or part of a composite key).
    /// Can also be applied at class level with property names — used by the
    /// Ddd.Entity.Dsl bridge to map [EntityId] without modifying existing properties.
    /// </summary>
    [MetaConcept(typeof(PrimaryKeyConcept))]
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class PrimaryKeyAttribute : Attribute
    {
        /// <summary>
        /// Property-level usage: marks the decorated property as primary key.
        /// </summary>
        public PrimaryKeyAttribute() { }

        /// <summary>
        /// Class-level usage: specifies primary key property names (composite key support).
        /// </summary>
        public PrimaryKeyAttribute(params string[] propertyNames)
        {
            PropertyNames = propertyNames;
        }

        /// <summary>Property names when used at class level (null when used at property level).</summary>
        [MetaProperty("PropertyNames", "string[]")]
        public string[]? PropertyNames { get; }

        [MetaProperty("Order", "int")]
        public int Order { get; set; } = 0;

        [MetaProperty("ValueGenerated", "ValueGeneration")]
        public ValueGeneration ValueGenerated { get; set; } = ValueGeneration.OnAdd;
    }
}
