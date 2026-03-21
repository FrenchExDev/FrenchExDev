namespace FrenchExDev.Net.Diem.Pages.Widgets.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Pages.Widgets.Attributes.Concepts;

    [MetaConcept(typeof(PageWidgetConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class PageWidgetAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Module", "string", Required = true)]
        public string Module { get; set; } = "";

        [MetaProperty("Description", "string")]
        public string Description { get; set; } = "";

        [MetaProperty("Icon", "string")]
        public string Icon { get; set; } = "";

        public PageWidgetAttribute(string name) { Name = name; }
    }
}
