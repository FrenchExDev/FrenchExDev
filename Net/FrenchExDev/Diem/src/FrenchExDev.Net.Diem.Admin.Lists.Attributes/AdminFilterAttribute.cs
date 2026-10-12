namespace FrenchExDev.Net.Diem.Admin.Lists.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Admin.Lists.Attributes.Concepts;

    [MetaConcept(typeof(AdminFilterConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class AdminFilterAttribute : Attribute
    {
        [MetaProperty("FieldName", "string", Required = true)]
        public string FieldName { get; set; }

        [MetaProperty("FilterType", "string")]
        public string FilterType { get; set; } = "Text";

        [MetaProperty("DisplayName", "string")]
        public string? DisplayName { get; set; }

        public AdminFilterAttribute(string fieldName) { FieldName = fieldName; }
    }
}
