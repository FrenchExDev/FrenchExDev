namespace FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes.Concepts;

    [MetaConcept(typeof(StageConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class StageAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("IsInitial", "bool")]
        public bool IsInitial { get; set; }

        [MetaProperty("IsFinal", "bool")]
        public bool IsFinal { get; set; }

        [MetaProperty("Color", "string")]
        public string? Color { get; set; }

        [MetaProperty("Description", "string")]
        public string? Description { get; set; }

        public StageAttribute(string name) { Name = name; }
    }
}
