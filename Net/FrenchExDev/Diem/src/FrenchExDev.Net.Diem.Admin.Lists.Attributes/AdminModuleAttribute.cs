namespace FrenchExDev.Net.Diem.Admin.Lists.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Admin.Lists.Attributes.Concepts;

    [MetaConcept(typeof(AdminModuleConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class AdminModuleAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        public Type Aggregate { get; }

        [MetaProperty("Icon", "string")]
        public string? Icon { get; set; }

        [MetaProperty("Group", "string")]
        public string? Group { get; set; }

        [MetaProperty("PageSize", "int")]
        public int PageSize { get; set; } = 25;

        public AdminModuleAttribute(string name, Type aggregate) { Name = name; Aggregate = aggregate; }
    }
}
