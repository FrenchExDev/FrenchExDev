using System.Text.RegularExpressions;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace DockAi.Api.Dsl;

/// <summary>
/// Parses a DSL query string into a Lucene <see cref="Query"/> plus metadata
/// (requested facets, link filters, etc.).
/// </summary>
public sealed class DslQueryParser
{
    public Query? LuceneQuery { get; private set; }
    public List<string> RequestedFacets { get; } = [];
    public int FacetTop { get; private set; } = 10;
    public string? LinkedDocId { get; private set; }
    public string? LinkType { get; private set; }
    public string? LinkVia { get; private set; }
    public float? MinLinkStrength { get; private set; }
    public string? PathFrom { get; private set; }
    public string? PathTo { get; private set; }
    public string? NeighborsOf { get; private set; }
    public int NeighborDepth { get; private set; } = 1;

    private static readonly Regex FieldFilterRx = new(@"^(\w[\w.]*):(>|<|>=|<=)?(.+)$", RegexOptions.Compiled);
    private static readonly Regex RangeRx = new(@"^(.+)\.\.(.+)$", RegexOptions.Compiled);
    private static readonly Regex FuzzyRx = new(@"^(\w+)~(\d+)?$", RegexOptions.Compiled);
    private static readonly Regex WildcardRx = new(@"^(\w+)\*$", RegexOptions.Compiled);
    private static readonly Regex PathRx = new(@"^(\w+)->(\w+)$", RegexOptions.Compiled);

    public void Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            LuceneQuery = new MatchAllDocsQuery();
            return;
        }

        var tokens = Tokenize(input);
        var query = new BooleanQuery();
        var hasClause = false;

        for (var i = 0; i < tokens.Count; i++)
        {
            var tok = tokens[i];

            // Boolean operators
            if (tok.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                tok.Equals("OR", StringComparison.OrdinalIgnoreCase) ||
                tok.Equals("NOT", StringComparison.OrdinalIgnoreCase))
                continue;

            var occur = Occur.MUST;
            if (i > 0 && tokens[i - 1].Equals("OR", StringComparison.OrdinalIgnoreCase))
                occur = Occur.SHOULD;
            if (i > 0 && tokens[i - 1].Equals("NOT", StringComparison.OrdinalIgnoreCase))
                occur = Occur.MUST_NOT;

            // Facet request: facet:entity,track
            if (tok.StartsWith("facet:", StringComparison.OrdinalIgnoreCase))
            {
                var facetValue = tok[6..];
                RequestedFacets.AddRange(facetValue.Split(',', StringSplitOptions.RemoveEmptyEntries));
                continue;
            }

            // Facet top: top:10
            if (tok.StartsWith("top:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(tok[4..], out var t)) FacetTop = t;
                continue;
            }

            // Depth for neighbors
            if (tok.StartsWith("depth:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(tok[6..], out var d)) NeighborDepth = d;
                continue;
            }

            // Field filter
            var fm = FieldFilterRx.Match(tok);
            if (fm.Success)
            {
                var field = fm.Groups[1].Value;
                var op = fm.Groups[2].Value;
                var value = fm.Groups[3].Value;

                var q = BuildFieldQuery(field, op, value);
                if (q is not null)
                {
                    query.Add(q, occur);
                    hasClause = true;
                }
                continue;
            }

            // Path: erard->leandri
            var pm = PathRx.Match(tok);
            if (pm.Success)
            {
                PathFrom = pm.Groups[1].Value;
                PathTo = pm.Groups[2].Value;
                continue;
            }

            // Phrase: "exact phrase"
            if (tok.StartsWith('"') && tok.EndsWith('"') && tok.Length > 2)
            {
                var phrase = tok[1..^1];
                var pq = new PhraseQuery();
                foreach (var word in phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    pq.Add(new Term("content", word.ToLowerInvariant()));
                query.Add(pq, occur);
                hasClause = true;
                continue;
            }

            // Fuzzy: word~2
            var fzm = FuzzyRx.Match(tok);
            if (fzm.Success)
            {
                var term = fzm.Groups[1].Value.ToLowerInvariant();
                var dist = fzm.Groups[2].Success ? int.Parse(fzm.Groups[2].Value) : 2;
                query.Add(new FuzzyQuery(new Term("content", term), Math.Min(dist, 2)), occur);
                hasClause = true;
                continue;
            }

            // Wildcard: word*
            var wm = WildcardRx.Match(tok);
            if (wm.Success)
            {
                query.Add(new PrefixQuery(new Term("content", wm.Groups[1].Value.ToLowerInvariant())), occur);
                hasClause = true;
                continue;
            }

            // Plain term
            query.Add(new TermQuery(new Term("content", tok.ToLowerInvariant())), occur);
            hasClause = true;
        }

        LuceneQuery = hasClause ? query : new MatchAllDocsQuery();
    }

    private Query? BuildFieldQuery(string field, string op, string value)
    {
        // Handle special link/neighbor fields as metadata (not Lucene)
        switch (field.ToLowerInvariant())
        {
            case "linked":
                LinkedDocId = value;
                return null;
            case "link.type":
                LinkType = value;
                return null;
            case "link.via":
                LinkVia = value;
                return null;
            case "link.strength":
                if (float.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var s))
                    MinLinkStrength = s;
                return null;
            case "neighbors":
                NeighborsOf = value;
                return null;
            case "path":
                var parts = value.Split("->", 2);
                if (parts.Length == 2) { PathFrom = parts[0]; PathTo = parts[1]; }
                return null;
        }

        // Map DSL field names to Lucene field names
        var luceneField = field.ToLowerInvariant() switch
        {
            "entity" => "entities",
            "entity.type" => "entity_types",
            "entity.role" => "entity_roles",
            "track" => "tracks",
            "theme" => "themes",
            "category" => "categories",
            "type" => "file_type",
            "tag" => "tags",
            "date" => "date",
            "size" => "file_size",
            _ => field.ToLowerInvariant()
        };

        // Date range: date:2025-01..2025-06
        var rm = RangeRx.Match(value);
        if (rm.Success)
        {
            return TermRangeQuery.NewStringRange(luceneField,
                rm.Groups[1].Value, rm.Groups[2].Value, true, true);
        }

        // Numeric comparison: date:>2025-01-01 or size:>100kb
        if (!string.IsNullOrEmpty(op))
        {
            if (luceneField == "date")
            {
                return op switch
                {
                    ">" => TermRangeQuery.NewStringRange(luceneField, value, null, false, true),
                    ">=" => TermRangeQuery.NewStringRange(luceneField, value, null, true, true),
                    "<" => TermRangeQuery.NewStringRange(luceneField, null, value, true, false),
                    "<=" => TermRangeQuery.NewStringRange(luceneField, null, value, true, true),
                    _ => new TermQuery(new Term(luceneField, value.ToLowerInvariant()))
                };
            }

            if (luceneField == "file_size" && TryParseSize(value, out var bytes))
            {
                return op switch
                {
                    ">" => NumericRangeQuery.NewInt64Range(luceneField, bytes, long.MaxValue, false, true),
                    ">=" => NumericRangeQuery.NewInt64Range(luceneField, bytes, long.MaxValue, true, true),
                    "<" => NumericRangeQuery.NewInt64Range(luceneField, 0, bytes, true, false),
                    "<=" => NumericRangeQuery.NewInt64Range(luceneField, 0, bytes, true, true),
                    _ => null
                };
            }
        }

        // Simple term match
        return new TermQuery(new Term(luceneField, value.ToLowerInvariant()));
    }

    private static bool TryParseSize(string value, out long bytes)
    {
        bytes = 0;
        value = value.Trim().ToLowerInvariant();
        long multiplier = 1;
        if (value.EndsWith("kb")) { multiplier = 1024; value = value[..^2]; }
        else if (value.EndsWith("mb")) { multiplier = 1024 * 1024; value = value[..^2]; }
        else if (value.EndsWith("gb")) { multiplier = 1024L * 1024 * 1024; value = value[..^2]; }

        if (long.TryParse(value, out var num))
        {
            bytes = num * multiplier;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Simple tokenizer for search queries: handles quotes, parentheses, operators.
    /// </summary>
    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < input.Length)
        {
            if (char.IsWhiteSpace(input[i])) { i++; continue; }

            // Quoted string
            if (input[i] == '"')
            {
                var end = input.IndexOf('"', i + 1);
                if (end == -1) end = input.Length;
                tokens.Add(input[i..(end + 1)]);
                i = end + 1;
                continue;
            }

            // Parentheses (ignored for now, reserved for future grouping)
            if (input[i] is '(' or ')') { i++; continue; }

            // Token (until whitespace)
            var start = i;
            while (i < input.Length && !char.IsWhiteSpace(input[i]) && input[i] is not '(' and not ')')
                i++;
            tokens.Add(input[start..i]);
        }
        return tokens;
    }
}
