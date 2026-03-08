using System.Text.RegularExpressions;
using DockAi.Api.Models;
using DockAi.Api.Ontology;

namespace DockAi.Api.Analysis;

/// <summary>
/// Detects known ontology entities in document content using alias matching.
/// </summary>
public sealed class EntityExtractor
{
    private readonly OntologyStore _ontology;

    // Compiled regex per entity: matches any alias as whole word (case-insensitive)
    private Dictionary<string, Regex> _patterns = new();

    public EntityExtractor(OntologyStore ontology)
    {
        _ontology = ontology;
    }

    /// <summary>Rebuild patterns from current ontology state. Call after ontology load.</summary>
    public void BuildPatterns()
    {
        _patterns.Clear();
        foreach (var entity in _ontology.Entities.Values)
        {
            var aliases = new List<string> { entity.Name };
            aliases.AddRange(entity.Aliases);

            // Sort longest first to avoid partial matches
            var escaped = aliases
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .OrderByDescending(a => a.Length)
                .Select(Regex.Escape);

            var pattern = $@"\b({string.Join('|', escaped)})\b";
            _patterns[entity.Id] = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }

    /// <summary>Extract entity references from the document content and populate doc.Entities.</summary>
    public void Extract(Document doc)
    {
        if (string.IsNullOrWhiteSpace(doc.Content)) return;

        foreach (var (entityId, regex) in _patterns)
        {
            if (regex.IsMatch(doc.Content))
                doc.Entities.Add(entityId);
        }
    }

    /// <summary>Count occurrences of a specific entity in content.</summary>
    public int Count(string content, string entityId)
    {
        return _patterns.TryGetValue(entityId, out var rx) ? rx.Matches(content).Count : 0;
    }
}
