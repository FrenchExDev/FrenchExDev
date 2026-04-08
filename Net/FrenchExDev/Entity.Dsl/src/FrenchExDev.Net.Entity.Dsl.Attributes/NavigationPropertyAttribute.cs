namespace FrenchExDev.Net.Entity.Dsl.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Entity.Dsl.Attributes.Concepts;

    /// <summary>
    /// Declares a navigation property relationship at class level.
    /// Used by the Ddd.Entity.Dsl bridge to map DDD relationships (Composition, Aggregation, Association)
    /// to Entity.Dsl without needing to add attributes to existing properties.
    /// </summary>
    [MetaConcept(typeof(NavigationPropertyConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class NavigationPropertyAttribute : Attribute
    {
        public NavigationPropertyAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }

        /// <summary>The name of the navigation property on the entity class.</summary>
        [MetaProperty("PropertyName", "string")]
        public string PropertyName { get; }

        /// <summary>The delete behavior for this relationship.</summary>
        [MetaProperty("OnDelete", "DeleteBehavior")]
        public DeleteBehavior OnDelete { get; set; } = DeleteBehavior.NoAction;
    }
}
