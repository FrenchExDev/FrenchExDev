using System.Globalization;
using System.Net;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace DockAi.Viewer.Rendering;

public sealed class CsvRenderer : IDocumentRenderer
{
    public IReadOnlyList<string> SupportedExtensions => [".csv"];

    public RenderResult Render(string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        var sb = new StringBuilder();
        sb.Append("<div class=\"doc-csv\"><table class=\"csv-table\">");

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();
        if (csv.HeaderRecord is not null)
        {
            sb.Append("<thead><tr>");
            foreach (var h in csv.HeaderRecord)
                sb.Append($"<th>{WebUtility.HtmlEncode(h)}</th>");
            sb.Append("</tr></thead>");
        }

        sb.Append("<tbody>");
        while (csv.Read())
        {
            sb.Append("<tr>");
            for (var i = 0; csv.TryGetField<string>(i, out var field); i++)
                sb.Append($"<td>{WebUtility.HtmlEncode(field ?? "")}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></div>");

        return new RenderResult { Html = sb.ToString(), RenderMode = "html" };
    }
}
