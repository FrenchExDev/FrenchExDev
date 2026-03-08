namespace DockAi.Api.Parsing;

public sealed class TextParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".txt"];

    public string Parse(string filePath) => File.ReadAllText(filePath);
}
