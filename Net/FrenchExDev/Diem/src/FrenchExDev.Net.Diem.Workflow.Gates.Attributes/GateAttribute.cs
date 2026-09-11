namespace FrenchExDev.Net.Diem.Workflow.Gates.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Workflow.Gates.Attributes.Concepts;

    [MetaConcept(typeof(GateConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class GateAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Transition", "string", Required = true)]
        public string Transition { get; set; } = "";

        [MetaProperty("GateType", "string")]
        public string GateType { get; set; } = "Custom";

        public GateAttribute(string name) { Name = name; }
    }
}
