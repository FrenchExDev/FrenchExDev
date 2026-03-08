using System.Net;
using System.Text;
using ClosedXML.Excel;

namespace DockAi.Viewer.Rendering;

public sealed class XlsxRenderer : IDocumentRenderer
{
    public IReadOnlyList<string> SupportedExtensions => [".xlsx", ".xls"];

    public RenderResult Render(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheetNames = workbook.Worksheets.Select(ws => ws.Name).ToList();

        var sb = new StringBuilder();
        sb.Append("<div class=\"doc-xlsx\">");

        // Sheet tabs
        if (sheetNames.Count > 1)
        {
            sb.Append("<div class=\"xlsx-tabs\">");
            for (var i = 0; i < sheetNames.Count; i++)
            {
                var active = i == 0 ? " active" : "";
                sb.Append($"<button class=\"xlsx-tab{active}\" data-sheet=\"{i}\">{WebUtility.HtmlEncode(sheetNames[i])}</button>");
            }
            sb.Append("</div>");
        }

        // Sheet contents
        for (var i = 0; i < workbook.Worksheets.Count; i++)
        {
            var sheet = workbook.Worksheets.ElementAt(i);
            var hidden = i > 0 ? " style=\"display:none\"" : "";
            sb.Append($"<div class=\"xlsx-sheet\" data-sheet=\"{i}\"{hidden}>");
            sb.Append(RenderSheet(sheet));
            sb.Append("</div>");
        }

        sb.Append("</div>");

        return new RenderResult
        {
            Html = sb.ToString(),
            RenderMode = "html",
            Metadata = new() { ["sheets"] = sheetNames }
        };
    }

    private static string RenderSheet(IXLWorksheet sheet)
    {
        var range = sheet.RangeUsed();
        if (range is null) return "<p class=\"xlsx-empty\">Empty sheet</p>";

        var sb = new StringBuilder();
        sb.Append("<table class=\"xlsx-table\">");

        var isFirst = true;
        foreach (var row in range.Rows())
        {
            sb.Append("<tr>");
            var tag = isFirst ? "th" : "td";
            foreach (var cell in row.Cells())
            {
                var val = cell.GetFormattedString();
                var align = cell.Style.Alignment.Horizontal switch
                {
                    XLAlignmentHorizontalValues.Right => " class=\"text-right\"",
                    XLAlignmentHorizontalValues.Center => " class=\"text-center\"",
                    _ => ""
                };
                sb.Append($"<{tag}{align}>{WebUtility.HtmlEncode(val)}</{tag}>");
            }
            sb.Append("</tr>");
            isFirst = false;
        }

        sb.Append("</table>");
        return sb.ToString();
    }
}
