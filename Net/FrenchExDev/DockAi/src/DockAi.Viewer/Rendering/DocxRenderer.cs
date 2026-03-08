using System.Net;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DockAi.Viewer.Rendering;

/// <summary>
/// Converts DOCX to structured HTML preserving headings, bold/italic, lists, tables, hyperlinks.
/// </summary>
public sealed class DocxRenderer : IDocumentRenderer
{
    public IReadOnlyList<string> SupportedExtensions => [".docx"];

    public RenderResult Render(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return new RenderResult { Html = "<p>Empty document</p>", RenderMode = "html" };

        var numberingPart = doc.MainDocumentPart?.NumberingDefinitionsPart;
        var sb = new StringBuilder();
        sb.Append("<div class=\"doc-docx\">");

        string? currentListTag = null;
        int currentListLevel = -1;

        foreach (var element in body.ChildElements)
        {
            if (element is Paragraph para)
            {
                var paraProps = para.ParagraphProperties;
                var numProps = paraProps?.NumberingProperties;

                // Close list if we exit a list context
                if (numProps is null && currentListTag is not null)
                {
                    sb.Append(CloseList(ref currentListTag, ref currentListLevel));
                }

                if (numProps is not null)
                {
                    // List item
                    var level = numProps.NumberingLevelReference?.Val?.Value ?? 0;
                    var numId = numProps.NumberingId?.Val?.Value ?? 0;
                    var listTag = IsOrderedList(numberingPart, numId, level) ? "ol" : "ul";

                    if (currentListTag is null)
                    {
                        sb.Append($"<{listTag}>");
                        currentListTag = listTag;
                        currentListLevel = level;
                    }
                    else if (currentListTag != listTag)
                    {
                        sb.Append(CloseList(ref currentListTag, ref currentListLevel));
                        sb.Append($"<{listTag}>");
                        currentListTag = listTag;
                        currentListLevel = level;
                    }

                    sb.Append("<li>");
                    sb.Append(RenderRuns(para, doc.MainDocumentPart));
                    sb.Append("</li>");
                }
                else
                {
                    // Regular paragraph or heading
                    var headingLevel = GetHeadingLevel(paraProps);
                    if (headingLevel > 0)
                    {
                        var tag = $"h{Math.Min(headingLevel, 6)}";
                        sb.Append($"<{tag}>{RenderRuns(para, doc.MainDocumentPart)}</{tag}>");
                    }
                    else
                    {
                        var text = RenderRuns(para, doc.MainDocumentPart);
                        if (!string.IsNullOrWhiteSpace(text))
                            sb.Append($"<p>{text}</p>");
                    }
                }
            }
            else if (element is Table table)
            {
                if (currentListTag is not null)
                    sb.Append(CloseList(ref currentListTag, ref currentListLevel));

                sb.Append(RenderTable(table, doc.MainDocumentPart));
            }
        }

        if (currentListTag is not null)
            sb.Append(CloseList(ref currentListTag, ref currentListLevel));

        sb.Append("</div>");
        return new RenderResult { Html = sb.ToString(), RenderMode = "html" };
    }

    private static string RenderRuns(Paragraph para, MainDocumentPart? mainPart)
    {
        var sb = new StringBuilder();
        foreach (var child in para.ChildElements)
        {
            if (child is Run run)
            {
                sb.Append(RenderRun(run));
            }
            else if (child is Hyperlink hyperlink)
            {
                var url = "";
                if (hyperlink.Id is not null && mainPart is not null)
                {
                    var rel = mainPart.HyperlinkRelationships
                        .FirstOrDefault(r => r.Id == hyperlink.Id);
                    url = rel?.Uri?.ToString() ?? "#";
                }
                sb.Append($"<a href=\"{WebUtility.HtmlEncode(url)}\" target=\"_blank\" rel=\"noopener\">");
                foreach (var r in hyperlink.Elements<Run>())
                    sb.Append(RenderRun(r));
                sb.Append("</a>");
            }
        }
        return sb.ToString();
    }

    private static string RenderRun(Run run)
    {
        var text = run.InnerText;
        if (string.IsNullOrEmpty(text)) return "";

        var encoded = WebUtility.HtmlEncode(text);
        var props = run.RunProperties;
        if (props is null) return encoded;

        if (props.Bold is not null && (props.Bold.Val is null || props.Bold.Val.Value))
            encoded = $"<strong>{encoded}</strong>";
        if (props.Italic is not null && (props.Italic.Val is null || props.Italic.Val.Value))
            encoded = $"<em>{encoded}</em>";
        if (props.Underline is not null && props.Underline.Val is not null &&
            props.Underline.Val.Value != UnderlineValues.None)
            encoded = $"<u>{encoded}</u>";
        if (props.Strike is not null && (props.Strike.Val is null || props.Strike.Val.Value))
            encoded = $"<s>{encoded}</s>";
        if (props.VerticalTextAlignment?.Val?.Value == VerticalPositionValues.Superscript)
            encoded = $"<sup>{encoded}</sup>";
        if (props.VerticalTextAlignment?.Val?.Value == VerticalPositionValues.Subscript)
            encoded = $"<sub>{encoded}</sub>";

        return encoded;
    }

    private static string RenderTable(Table table, MainDocumentPart? mainPart)
    {
        var sb = new StringBuilder();
        sb.Append("<table class=\"docx-table\">");

        var isFirst = true;
        foreach (var row in table.Elements<TableRow>())
        {
            var tag = isFirst ? "th" : "td";
            sb.Append("<tr>");
            foreach (var cell in row.Elements<TableCell>())
            {
                sb.Append($"<{tag}>");
                foreach (var para in cell.Elements<Paragraph>())
                    sb.Append(RenderRuns(para, mainPart));
                sb.Append($"</{tag}>");
            }
            sb.Append("</tr>");
            isFirst = false;
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static int GetHeadingLevel(ParagraphProperties? props)
    {
        if (props is null) return 0;

        var styleId = props.ParagraphStyleId?.Val?.Value;
        if (styleId is null) return 0;

        // Common heading style patterns
        var lower = styleId.ToLowerInvariant();
        if (lower.StartsWith("heading") || lower.StartsWith("titre"))
        {
            var numPart = new string(lower.Where(char.IsDigit).ToArray());
            if (int.TryParse(numPart, out var level) && level is >= 1 and <= 9)
                return level;
        }

        // Outline level
        var outlineLevel = props.OutlineLevel?.Val?.Value;
        if (outlineLevel is not null && outlineLevel >= 0 && outlineLevel <= 8)
            return outlineLevel.Value + 1;

        return 0;
    }

    private static bool IsOrderedList(NumberingDefinitionsPart? numberingPart, int numId, int level)
    {
        if (numberingPart is null) return false;

        try
        {
            var numbering = numberingPart.Numbering;
            var numInstance = numbering?.Elements<NumberingInstance>()
                .FirstOrDefault(n => n.NumberID?.Value == numId);
            var abstractNumId = numInstance?.AbstractNumId?.Val?.Value;
            if (abstractNumId is null) return false;

            var abstractNum = numbering?.Elements<AbstractNum>()
                .FirstOrDefault(a => a.AbstractNumberId?.Value == abstractNumId);
            var lvl = abstractNum?.Elements<Level>()
                .FirstOrDefault(l => l.LevelIndex?.Value == level);

            var fmt = lvl?.NumberingFormat?.Val?.Value;
            return fmt is not null && fmt != NumberFormatValues.Bullet;
        }
        catch
        {
            return false;
        }
    }

    private static string CloseList(ref string? listTag, ref int listLevel)
    {
        if (listTag is null) return "";
        var result = $"</{listTag}>";
        listTag = null;
        listLevel = -1;
        return result;
    }
}
