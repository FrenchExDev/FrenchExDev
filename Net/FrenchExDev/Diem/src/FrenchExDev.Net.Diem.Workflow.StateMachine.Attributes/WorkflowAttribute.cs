namespace FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes.Concepts;

    [MetaConcept(typeof(WorkflowConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class WorkflowAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Description", "string")]
        public string? Description { get; set; }

        public WorkflowAttribute(string name) { Name = name; }
    }
}
