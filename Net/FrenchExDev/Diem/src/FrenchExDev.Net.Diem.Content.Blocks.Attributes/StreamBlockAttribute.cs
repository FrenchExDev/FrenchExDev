namespace FrenchExDev.Net.Diem.Content.Blocks.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Content.Blocks.Attributes.Concepts;

    [MetaConcept(typeof(StreamBlockConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class StreamBlockAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Description", "string")]
        public string Description { get; set; } = "";

        public Type[] AllowedBlockTypes { get; set; } = Array.Empty<Type>();

        public StreamBlockAttribute(string name) { Name = name; }
    }
}
