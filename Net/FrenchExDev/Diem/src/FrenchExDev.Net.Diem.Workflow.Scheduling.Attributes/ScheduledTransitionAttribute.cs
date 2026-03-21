namespace FrenchExDev.Net.Diem.Workflow.Scheduling.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Workflow.Scheduling.Attributes.Concepts;

    [MetaConcept(typeof(ScheduledTransitionConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ScheduledTransitionAttribute : Attribute
    {
        [MetaProperty("From", "string", Required = true)]
        public string From { get; set; }

        [MetaProperty("To", "string", Required = true)]
        public string To { get; set; }

        [MetaProperty("DateProperty", "string", Required = true)]
        public string DateProperty { get; set; }

        public ScheduledTransitionAttribute(string from, string to, string dateProperty)
        { From = from; To = to; DateProperty = dateProperty; }
    }
}
