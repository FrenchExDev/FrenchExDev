using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace DockAi.Api.Parsing;

public sealed class CsvParser : IDocumentParser
{
    public IReadOnlyList<string> SupportedExtensions => [".csv"];

    public string Parse(string filePath)
    {
        var sb = new StringBuilder();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();
        if (csv.HeaderRecord is not null)
            sb.AppendLine(string.Join(" | ", csv.HeaderRecord));

        while (csv.Read())
        {
            var fields = new List<string>();
            for (var i = 0; csv.TryGetField<string>(i, out var field); i++)
            {
                if (!string.IsNullOrWhiteSpace(field))
                    fields.Add(field);
            }
            if (fields.Count > 0)
                sb.AppendLine(string.Join(" | ", fields));
        }
        return sb.ToString();
    }
}
