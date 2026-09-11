namespace FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class HasWorkflowConcept : MetaConcept
    {
        public override string Name => "HasWorkflow";
        public override Type AttributeType => typeof(HasWorkflowAttribute);
    }
}
