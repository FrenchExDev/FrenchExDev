using System.Text.RegularExpressions;
using DockAi.Api.Models;

namespace DockAi.Api.Analysis;

/// <summary>
/// Applies classification rules (@rule) to documents.
/// </summary>
public sealed class RuleEngine
{
    private readonly List<CompiledRule> _rules = [];

    public void LoadRules(IEnumerable<ClassificationRule> rules)
    {
        _rules.Clear();
        foreach (var rule in rules)
        {
            try
            {
                var regex = new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                _rules.Add(new CompiledRule(rule, regex));
            }
            catch (RegexParseException)
            {
                // Skip invalid patterns
            }
        }
    }

    /// <summary>Apply all matching rules to a document.</summary>
    public void Apply(Document doc)
    {
        if (string.IsNullOrWhiteSpace(doc.Content)) return;

        foreach (var compiled in _rules)
        {
            if (!compiled.Regex.IsMatch(doc.Content)) continue;

            var rule = compiled.Rule;
            if (rule.Action == RuleAction.Assign && rule.TaxonomyId is not null && rule.NodeId is not null)
            {
                if (!doc.TaxonomyAssignments.TryGetValue(rule.TaxonomyId, out var nodes))
                {
                    nodes = [];
                    doc.TaxonomyAssignments[rule.TaxonomyId] = nodes;
                }
                nodes.Add(rule.NodeId);
            }
        }
    }

    private sealed record CompiledRule(ClassificationRule Rule, Regex Regex);
}
