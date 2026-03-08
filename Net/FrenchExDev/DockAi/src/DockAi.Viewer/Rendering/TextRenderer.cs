using System.Net;

namespace DockAi.Viewer.Rendering;

public sealed class TextRenderer : IDocumentRenderer
{
    public IReadOnlyList<string> SupportedExtensions => [".txt"];

    public RenderResult Render(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var escaped = WebUtility.HtmlEncode(content);
        return new RenderResult
        {
            Html = $"<pre class=\"doc-text\">{escaped}</pre>",
            RenderMode = "html"
        };
    }
}
