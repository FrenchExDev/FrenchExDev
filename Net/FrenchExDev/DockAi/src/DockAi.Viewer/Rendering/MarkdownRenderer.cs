using Markdig;

namespace DockAi.Viewer.Rendering;

public sealed class MarkdownRenderer : IDocumentRenderer
{
    public IReadOnlyList<string> SupportedExtensions => [".md"];

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public RenderResult Render(string filePath)
    {
        var md = File.ReadAllText(filePath);
        var html = Markdown.ToHtml(md, Pipeline);
        return new RenderResult
        {
            Html = $"<div class=\"doc-markdown\">{html}</div>",
            RenderMode = "html"
        };
    }
}
