namespace DockAi.Api.Models;

/// <summary>
/// An entity type defined in the ontology DSL via @type.
/// </summary>
public sealed class EntityType
{
    public required string Id { get; init; }
    public Dictionary<string, PropertyDef> Properties { get; init; } = new();
}

/// <summary>
/// Definition of a single property inside an entity type.
/// </summary>
public sealed class PropertyDef
{
    public required string Name { get; init; }
    public required PropertyKind Kind { get; init; }

    /// <summary>Allowed values when Kind is Enum.</summary>
    public List<string>? EnumValues { get; init; }
}

public enum PropertyKind
{
    String,
    Number,
    Date,
    Bool,
    StringArray,
    Enum
}

/// <summary>
/// A concrete entity instance defined via @entity.
/// </summary>
public sealed class Entity
{
    public required string Id { get; init; }
    public required string TypeId { get; init; }
    public Dictionary<string, object?> Properties { get; init; } = new();

    public string Name => Properties.TryGetValue("name", out var n) ? n?.ToString() ?? Id : Id;
    public IReadOnlyList<string> Aliases =>
        Properties.TryGetValue("aliases", out var a) && a is List<string> list ? list : [];
}
