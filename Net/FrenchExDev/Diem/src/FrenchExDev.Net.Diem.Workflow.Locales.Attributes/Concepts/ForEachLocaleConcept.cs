namespace FrenchExDev.Net.Diem.Workflow.Locales.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class ForEachLocaleConcept : MetaConcept
    {
        public override string Name => "ForEachLocale";
        public override Type AttributeType => typeof(ForEachLocaleAttribute);
    }
}
