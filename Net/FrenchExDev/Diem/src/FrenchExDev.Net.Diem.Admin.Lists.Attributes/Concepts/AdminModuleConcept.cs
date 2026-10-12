namespace FrenchExDev.Net.Diem.Admin.Lists.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class AdminModuleConcept : MetaConcept
    {
        public override string Name => "AdminModule";
        public override Type AttributeType => typeof(AdminModuleAttribute);
    }
}
