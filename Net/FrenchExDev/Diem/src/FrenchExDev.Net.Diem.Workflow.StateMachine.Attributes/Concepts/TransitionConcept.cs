namespace FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class TransitionConcept : MetaConcept
    {
        public override string Name => "Transition";
        public override Type AttributeType => typeof(TransitionAttribute);
    }
}
