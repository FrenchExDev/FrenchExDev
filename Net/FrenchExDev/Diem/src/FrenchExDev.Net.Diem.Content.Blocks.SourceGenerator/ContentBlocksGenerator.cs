namespace FrenchExDev.Net.Diem.Content.Blocks.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    [Generator(LanguageNames.CSharp)]
    public sealed class ContentBlocksGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Will generate block serialization, validation, admin editors
        }
    }
}
