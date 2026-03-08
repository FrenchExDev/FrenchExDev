using HtmlAgilityPack;

namespace DockAi.Api.Parsing;

public sealed class HtmlParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".html", ".htm"];

    public string Parse(string filePath)
    {
        var doc = new HtmlDocument();
        doc.Load(filePath);

        // Remove script and style elements
        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style") ?? Enumerable.Empty<HtmlNode>())
            node.Remove();

        return doc.DocumentNode.InnerText.Trim();
    }
}
