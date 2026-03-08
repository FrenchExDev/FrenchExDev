namespace DockAi.Api.Models;

/// <summary>
/// A rule defined via @rule in the DSL for auto-classifying documents.
/// </summary>
public sealed class ClassificationRule
{
    public required string Id { get; init; }

    /// <summary>Regex pattern to match against document content.</summary>
    public required string Pattern { get; init; }

    /// <summary>Action: "assign" (taxonomy) or "extract" (entity type).</summary>
    public required RuleAction Action { get; init; }

    /// <summary>For assign: "track:voie_a" or "theme:fraude_tech".</summary>
    public string? TaxonomyId { get; init; }
    public string? NodeId { get; init; }

    /// <summary>For extract: the entity type to extract.</summary>
    public string? ExtractType { get; init; }

    /// <summary>Confidence threshold (0.0 - 1.0).</summary>
    public float Confidence { get; init; } = 0.5f;
}

public enum RuleAction
{
    Assign,
    Extract
}
