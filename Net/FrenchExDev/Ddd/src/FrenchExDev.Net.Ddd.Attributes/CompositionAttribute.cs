namespace FrenchExDev.Net.Ddd.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Ddd.Attributes.Concepts;

    [MetaConcept(typeof(CompositionConcept))]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class CompositionAttribute : Attribute { }
}
