namespace FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class WorkflowConcept : MetaConcept
    {
        public override string Name => "Workflow";
        public override Type AttributeType => typeof(WorkflowAttribute);
    }
}
