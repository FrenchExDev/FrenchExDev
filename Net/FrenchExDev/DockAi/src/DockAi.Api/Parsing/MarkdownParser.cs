using Markdig;

namespace DockAi.Api.Parsing;

public sealed class MarkdownParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".md"];

    public string Parse(string filePath)
    {
        var md = File.ReadAllText(filePath);
        // Convert markdown to plain text by stripping HTML from rendered output
        var html = Markdown.ToHtml(md);
        return StripHtml(html);
    }

    private static string StripHtml(string html)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode.InnerText;
    }
}
