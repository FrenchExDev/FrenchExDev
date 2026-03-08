using HtmlAgilityPack;

namespace DockAi.Viewer.Rendering;

public sealed class HtmlRenderer : IDocumentRenderer
{
    public IReadOnlyList<string> SupportedExtensions => [".html", ".htm"];

    public RenderResult Render(string filePath)
    {
        var doc = new HtmlDocument();
        doc.Load(filePath);

        // Strip script and style elements for safety
        var toRemove = doc.DocumentNode.SelectNodes("//script|//style");
        if (toRemove is not null)
            foreach (var node in toRemove)
                node.Remove();

        // Extract body content if present, otherwise use full document
        var body = doc.DocumentNode.SelectSingleNode("//body");
        var html = body?.InnerHtml ?? doc.DocumentNode.InnerHtml;

        return new RenderResult
        {
            Html = $"<div class=\"doc-html\">{html}</div>",
            RenderMode = "html"
        };
    }
}
