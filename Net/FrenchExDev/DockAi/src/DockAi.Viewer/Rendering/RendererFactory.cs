namespace DockAi.Viewer.Rendering;

public sealed class RendererFactory
{
    private readonly Dictionary<string, IDocumentRenderer> _renderers = new(StringComparer.OrdinalIgnoreCase);

    public RendererFactory()
    {
        Register(new TextRenderer());
        Register(new MarkdownRenderer());
        Register(new HtmlRenderer());
        Register(new DocxRenderer());
        Register(new XlsxRenderer());
        Register(new CsvRenderer());
        Register(new PdfRenderer());
    }

    private void Register(IDocumentRenderer renderer)
    {
        foreach (var ext in renderer.SupportedExtensions)
            _renderers[ext] = renderer;
    }

    public IDocumentRenderer? GetRenderer(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return _renderers.GetValueOrDefault(ext);
    }

    public bool CanRender(string filePath) => GetRenderer(filePath) is not null;
}
