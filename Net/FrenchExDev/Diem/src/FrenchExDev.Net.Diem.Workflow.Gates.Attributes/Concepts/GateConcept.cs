namespace FrenchExDev.Net.Diem.Workflow.Gates.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class GateConcept : MetaConcept
    {
        public override string Name => "Gate";
        public override Type AttributeType => typeof(GateAttribute);
    }
}
