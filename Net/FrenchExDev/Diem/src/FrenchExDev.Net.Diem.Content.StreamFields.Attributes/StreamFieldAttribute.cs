namespace FrenchExDev.Net.Diem.Content.StreamFields.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Content.StreamFields.Attributes.Concepts;

    [MetaConcept(typeof(StreamFieldConcept))]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class StreamFieldAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        public Type[] AllowedBlockTypes { get; set; } = Array.Empty<Type>();

        public StreamFieldAttribute(string name) { Name = name; }
    }
}
