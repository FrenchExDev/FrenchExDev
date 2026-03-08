using System.Text;
using UglyToad.PdfPig;

namespace DockAi.Api.Parsing;

public sealed class PdfParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".pdf"];

    public string Parse(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine(text);
        }
        return sb.ToString();
    }
}
