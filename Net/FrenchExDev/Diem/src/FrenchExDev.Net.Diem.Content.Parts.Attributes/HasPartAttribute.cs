namespace FrenchExDev.Net.Diem.Content.Parts.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Content.Parts.Attributes.Concepts;

    [MetaConcept(typeof(HasPartConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class HasPartAttribute : Attribute
    {
        public Type PartType { get; }
        public HasPartAttribute(Type partType) { PartType = partType; }
    }
}
