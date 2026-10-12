namespace FrenchExDev.Net.Diem.Admin.Lists.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class AdminFilterConcept : MetaConcept
    {
        public override string Name => "AdminFilter";
        public override Type AttributeType => typeof(AdminFilterAttribute);
    }
}
