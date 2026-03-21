namespace FrenchExDev.Net.Diem.Workflow.Locales.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Workflow.Locales.Attributes.Concepts;

    [MetaConcept(typeof(ForEachLocaleConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ForEachLocaleAttribute : Attribute
    {
        [MetaProperty("Stage", "string", Required = true)]
        public string Stage { get; set; }

        public ForEachLocaleAttribute(string stage) { Stage = stage; }
    }
}
