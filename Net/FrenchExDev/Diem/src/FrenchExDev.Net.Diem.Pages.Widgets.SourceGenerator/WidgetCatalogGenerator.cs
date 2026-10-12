namespace FrenchExDev.Net.Diem.Pages.Widgets.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    [Generator(LanguageNames.CSharp)]
    public sealed class WidgetCatalogGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Will generate WidgetCatalog.g.cs from [PageWidget] classes
        }
    }
}
