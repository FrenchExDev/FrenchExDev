namespace FrenchExDev.Net.Diem.Pages.Widgets.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class PageWidgetConcept : MetaConcept
    {
        public override string Name => "PageWidget";
        public override Type AttributeType => typeof(PageWidgetAttribute);
    }
}
