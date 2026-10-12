namespace FrenchExDev.Net.Entity.Dsl.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Entity.Dsl.Attributes.Concepts;

    /// <summary>
    /// Marks a class as an entity mapped to a database table.
    /// This is Entity.Dsl's own entry point — the SG discovers classes with this attribute.
    /// DDD users get this attribute auto-generated via the Ddd.Entity.Dsl bridge.
    /// </summary>
    [MetaConcept(typeof(MappedEntityConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class MappedEntityAttribute : Attribute { }
}
