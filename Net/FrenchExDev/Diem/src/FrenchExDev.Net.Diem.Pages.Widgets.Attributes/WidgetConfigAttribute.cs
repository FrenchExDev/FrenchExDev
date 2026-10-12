namespace FrenchExDev.Net.Diem.Pages.Widgets.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Pages.Widgets.Attributes.Concepts;

    [MetaConcept(typeof(WidgetConfigConcept))]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class WidgetConfigAttribute : Attribute
    {
        [MetaProperty("DisplayName", "string")]
        public string DisplayName { get; set; } = "";

        [MetaProperty("HelpText", "string")]
        public string HelpText { get; set; } = "";

        [MetaProperty("Required", "bool")]
        public bool Required { get; set; }

        [MetaProperty("DefaultValue", "string")]
        public string DefaultValue { get; set; } = "";
    }
}
