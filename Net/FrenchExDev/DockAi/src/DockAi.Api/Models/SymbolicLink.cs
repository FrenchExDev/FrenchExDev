namespace DockAi.Api.Models;

/// <summary>
/// A weighted, typed link between two documents or between a document and an entity.
/// </summary>
public sealed class SymbolicLink
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public required string LinkType { get; init; }
    public string? LinkName { get; init; }
    public float Strength { get; init; }
    public string? Reason { get; init; }
    public List<string> Via { get; init; } = [];
    public Dictionary<string, string> Properties { get; init; } = new();
}
