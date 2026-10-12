namespace FrenchExDev.Net.Diem.Workflow.Scheduling.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class ScheduledTransitionConcept : MetaConcept
    {
        public override string Name => "ScheduledTransition";
        public override Type AttributeType => typeof(ScheduledTransitionAttribute);
    }
}
