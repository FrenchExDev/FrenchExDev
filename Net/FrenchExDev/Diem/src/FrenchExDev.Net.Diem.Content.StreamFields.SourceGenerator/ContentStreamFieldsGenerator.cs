namespace FrenchExDev.Net.Diem.Content.StreamFields.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    [Generator(LanguageNames.CSharp)]
    public sealed class ContentStreamFieldsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Will generate JSON converters for stream field polymorphic deserialization
        }
    }
}
