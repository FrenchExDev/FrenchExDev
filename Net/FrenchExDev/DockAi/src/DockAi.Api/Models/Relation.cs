namespace DockAi.Api.Models;

/// <summary>
/// A relation type (predicate) defined via @relation.type.
/// </summary>
public sealed class RelationType
{
    public required string Id { get; init; }
    public required string FromType { get; init; }
    public required List<string> ToTypes { get; init; }
    public List<string> PropertyNames { get; init; } = [];
    public string? Inverse { get; init; }
    public bool Symmetric { get; init; }
    public bool Auto { get; init; }
}

/// <summary>
/// A concrete relation instance defined via @relation.
/// </summary>
public sealed class Relation
{
    public required string FromId { get; init; }
    public required string ToId { get; init; }
    public required string TypeId { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}
