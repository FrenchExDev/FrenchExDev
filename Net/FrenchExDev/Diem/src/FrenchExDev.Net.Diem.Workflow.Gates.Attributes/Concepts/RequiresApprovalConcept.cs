namespace FrenchExDev.Net.Diem.Workflow.Gates.Attributes.Concepts
{
    using System;
    using System.Collections.Generic;
    using FrenchExDev.Net.Dsl;

    public sealed class RequiresApprovalConcept : MetaConcept
    {
        public override string Name => "RequiresApproval";
        public override Type AttributeType => typeof(RequiresApprovalAttribute);
        public override IReadOnlyList<Type> SuperTypes => new[] { typeof(GateConcept) };
    }
}
