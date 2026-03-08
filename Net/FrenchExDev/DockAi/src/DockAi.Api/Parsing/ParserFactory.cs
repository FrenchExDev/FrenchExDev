namespace DockAi.Api.Parsing;

/// <summary>
/// Resolves the correct <see cref="IDocumentParser"/> for a given file extension.
/// </summary>
public sealed class ParserFactory
{
    private readonly Dictionary<string, IDocumentParser> _parsers = new(StringComparer.OrdinalIgnoreCase);

    public ParserFactory()
    {
        Register(new TextParser());
        Register(new MarkdownParser());
        Register(new HtmlParser());
        Register(new DocxParser());
        Register(new XlsxParser());
        Register(new CsvParser());
        Register(new PdfParser());
    }

    private void Register(IDocumentParser parser)
    {
        foreach (var ext in parser.SupportedExtensions)
            _parsers[ext] = parser;
    }

    public IDocumentParser? GetParser(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return _parsers.GetValueOrDefault(ext);
    }

    public bool CanParse(string filePath) => GetParser(filePath) is not null;

    public IReadOnlyCollection<string> SupportedExtensions => _parsers.Keys;
}
