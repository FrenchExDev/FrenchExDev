namespace FrenchExDev.Net.Diem.Workflow.Gates.Attributes.Concepts
{
    using System;
    using System.Collections.Generic;
    using FrenchExDev.Net.Dsl;

    public sealed class RequiresRoleConcept : MetaConcept
    {
        public override string Name => "RequiresRole";
        public override Type AttributeType => typeof(RequiresRoleAttribute);
        public override IReadOnlyList<Type> SuperTypes => new[] { typeof(GateConcept) };
    }
}
