using DockAi.Api.Models;

namespace DockAi.Api.Ontology;

/// <summary>
/// In-memory graph of entity types, entities, relation types, and relations.
/// Thread-safe for reads after initialization.
/// </summary>
public sealed class OntologyStore
{
    private readonly Dictionary<string, EntityType> _entityTypes = new();
    private readonly Dictionary<string, Entity> _entities = new();
    private readonly Dictionary<string, RelationType> _relationTypes = new();
    private readonly List<Relation> _relations = [];

    // Reverse index: alias -> entity id (lowercase)
    private readonly Dictionary<string, string> _aliasIndex = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, EntityType> EntityTypes => _entityTypes;
    public IReadOnlyDictionary<string, Entity> Entities => _entities;
    public IReadOnlyDictionary<string, RelationType> RelationTypes => _relationTypes;
    public IReadOnlyList<Relation> Relations => _relations;

    public void AddEntityType(EntityType type) => _entityTypes[type.Id] = type;

    public void AddEntity(Entity entity)
    {
        _entities[entity.Id] = entity;
        // Index name + aliases
        _aliasIndex[entity.Id] = entity.Id;
        _aliasIndex[entity.Name.ToLowerInvariant()] = entity.Id;
        foreach (var alias in entity.Aliases)
            _aliasIndex[alias.ToLowerInvariant()] = entity.Id;
    }

    public void AddRelationType(RelationType rt) => _relationTypes[rt.Id] = rt;

    public void AddRelation(Relation rel) => _relations.Add(rel);

    /// <summary>Resolve a name/alias to an entity id (case-insensitive).</summary>
    public string? ResolveEntity(string nameOrAlias) =>
        _aliasIndex.TryGetValue(nameOrAlias, out var id) ? id : null;

    /// <summary>Get all relations involving a given entity (as from or to).</summary>
    public IEnumerable<Relation> RelationsFor(string entityId) =>
        _relations.Where(r => r.FromId == entityId || r.ToId == entityId);

    /// <summary>Get all entities of a given type.</summary>
    public IEnumerable<Entity> EntitiesOfType(string typeId) =>
        _entities.Values.Where(e => e.TypeId == typeId);

    /// <summary>Get entity by id.</summary>
    public Entity? GetEntity(string id) => _entities.GetValueOrDefault(id);

    public void Clear()
    {
        _entityTypes.Clear();
        _entities.Clear();
        _relationTypes.Clear();
        _relations.Clear();
        _aliasIndex.Clear();
    }
}
