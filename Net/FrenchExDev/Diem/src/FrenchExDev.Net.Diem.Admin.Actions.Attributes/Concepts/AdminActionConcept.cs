namespace FrenchExDev.Net.Diem.Admin.Actions.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class AdminActionConcept : MetaConcept
    {
        public override string Name => "AdminAction";
        public override Type AttributeType => typeof(AdminActionAttribute);
    }
}
