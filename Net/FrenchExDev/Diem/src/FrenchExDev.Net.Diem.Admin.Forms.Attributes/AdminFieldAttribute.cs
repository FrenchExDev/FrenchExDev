namespace FrenchExDev.Net.Diem.Admin.Forms.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Admin.Forms.Attributes.Concepts;

    [MetaConcept(typeof(AdminFieldConcept))]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AdminFieldAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("DisplayType", "string")]
        public string? DisplayType { get; set; }

        [MetaProperty("DisplayName", "string")]
        public string? DisplayName { get; set; }

        [MetaProperty("ReadOnly", "bool")]
        public bool ReadOnly { get; set; }

        [MetaProperty("HideInList", "bool")]
        public bool HideInList { get; set; }

        [MetaProperty("HideInForm", "bool")]
        public bool HideInForm { get; set; }

        [MetaProperty("Order", "int")]
        public int Order { get; set; }

        public AdminFieldAttribute(string name) { Name = name; }
    }
}
