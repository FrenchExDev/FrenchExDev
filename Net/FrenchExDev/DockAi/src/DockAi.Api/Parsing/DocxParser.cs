using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DockAi.Api.Parsing;

public sealed class DocxParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".docx"];

    public string Parse(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var para in body.Descendants<Paragraph>())
        {
            var text = para.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine(text);
        }
        return sb.ToString();
    }
}
