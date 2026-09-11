namespace FrenchExDev.Net.Diem.Content.Parts.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    [Generator(LanguageNames.CSharp)]
    public sealed class ContentPartsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Will generate part implementations, EF owned types, admin field editors
        }
    }
}
