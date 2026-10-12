namespace FrenchExDev.Net.Diem.Pages.Widgets.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class WidgetConfigConcept : MetaConcept
    {
        public override string Name => "WidgetConfig";
        public override Type AttributeType => typeof(WidgetConfigAttribute);
    }
}
