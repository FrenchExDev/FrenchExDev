namespace FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class StageConcept : MetaConcept
    {
        public override string Name => "Stage";
        public override Type AttributeType => typeof(StageAttribute);
    }
}
