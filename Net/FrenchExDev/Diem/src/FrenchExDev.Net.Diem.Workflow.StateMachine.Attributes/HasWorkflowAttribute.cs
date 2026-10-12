namespace FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes.Concepts;

    [MetaConcept(typeof(HasWorkflowConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class HasWorkflowAttribute : Attribute
    {
        [MetaProperty("WorkflowName", "string", Required = true)]
        public string WorkflowName { get; set; }

        public HasWorkflowAttribute(string workflowName) { WorkflowName = workflowName; }
    }
}
