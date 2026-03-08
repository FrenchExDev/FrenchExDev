namespace DockAi.Viewer.Rendering;

public sealed class PdfRenderer : IDocumentRenderer
{
    public IReadOnlyList<string> SupportedExtensions => [".pdf"];

    public RenderResult Render(string filePath)
    {
        // PDF is rendered by the browser's native viewer via /raw/{id}
        // The frontend will use the renderMode to create an <embed> element
        return new RenderResult
        {
            Html = "<p class=\"doc-pdf-placeholder\">PDF document — use the embedded viewer below.</p>",
            RenderMode = "pdf-embed"
        };
    }
}
