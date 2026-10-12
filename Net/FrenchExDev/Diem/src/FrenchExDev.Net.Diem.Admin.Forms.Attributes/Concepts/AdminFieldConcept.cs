namespace FrenchExDev.Net.Diem.Admin.Forms.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class AdminFieldConcept : MetaConcept
    {
        public override string Name => "AdminField";
        public override Type AttributeType => typeof(AdminFieldAttribute);
    }
}
