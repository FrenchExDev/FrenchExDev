using System.Text;
using ClosedXML.Excel;

namespace DockAi.Api.Parsing;

public sealed class XlsxParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".xlsx", ".xls"];

    public string Parse(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var sb = new StringBuilder();

        foreach (var sheet in workbook.Worksheets)
        {
            sb.AppendLine($"[Sheet: {sheet.Name}]");
            var range = sheet.RangeUsed();
            if (range is null) continue;

            foreach (var row in range.Rows())
            {
                var cells = new List<string>();
                foreach (var cell in row.Cells())
                {
                    var val = cell.GetFormattedString();
                    if (!string.IsNullOrWhiteSpace(val))
                        cells.Add(val);
                }
                if (cells.Count > 0)
                    sb.AppendLine(string.Join(" | ", cells));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
