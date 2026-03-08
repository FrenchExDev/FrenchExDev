using DockAi.Api.Models;

namespace DockAi.Api.Dsl;

/// <summary>
/// Fluent C# API for building an OntologySchema programmatically.
/// Alternative to authoring .dsl files — both produce the same OntologySchema.
/// </summary>
public sealed class OntologyBuilder
{
    private readonly OntologySchema _schema = new();

    public OntologyBuilder Type(string id, Action<EntityTypeBuilder> configure)
    {
        var b = new EntityTypeBuilder(id);
        configure(b);
        _schema.EntityTypes[id] = b.Build();
        return this;
    }

    public OntologyBuilder Entity(string id, string typeId, Action<EntityBuilder> configure)
    {
        var b = new EntityBuilder(id, typeId);
        configure(b);
        _schema.Entities[id] = b.Build();
        return this;
    }

    public OntologyBuilder RelationType(string id, Action<RelationTypeBuilder> configure)
    {
        var b = new RelationTypeBuilder(id);
        configure(b);
        _schema.RelationTypes[id] = b.Build();
        return this;
    }

    public OntologyBuilder Relation(string fromId, string typeId, string toId,
        Action<RelationBuilder>? configure = null)
    {
        var b = new RelationBuilder(fromId, typeId, toId);
        configure?.Invoke(b);
        _schema.Relations.Add(b.Build());
        return this;
    }

    public OntologyBuilder Taxonomy(string id, string label, Action<TaxonomyBuilder> configure)
    {
        var b = new TaxonomyBuilder(id, label);
        configure(b);
        _schema.Taxonomies[id] = b.Build();
        return this;
    }

    public OntologyBuilder Rule(string id, Action<ClassificationRuleBuilder> configure)
    {
        var b = new ClassificationRuleBuilder(id);
        configure(b);
        _schema.Rules.Add(b.Build());
        return this;
    }

    public OntologySchema Build() => _schema;
}

// ── Individual builders ──

public sealed class EntityTypeBuilder
{
    private readonly string _id;
    private readonly Dictionary<string, PropertyDef> _properties = new();

    internal EntityTypeBuilder(string id) => _id = id;

    public EntityTypeBuilder Field(string name, PropertyKind kind, List<string>? enumValues = null)
    {
        _properties[name] = new PropertyDef { Name = name, Kind = kind, EnumValues = enumValues };
        return this;
    }

    internal EntityType Build() => new() { Id = _id, Properties = _properties };
}

public sealed class EntityBuilder
{
    private readonly string _id;
    private readonly string _typeId;
    private readonly Dictionary<string, object?> _properties = new();

    internal EntityBuilder(string id, string typeId) { _id = id; _typeId = typeId; }

    public EntityBuilder Set(string name, object? value)
    {
        _properties[name] = value;
        return this;
    }

    public EntityBuilder Aliases(params string[] aliases) => Set("aliases", aliases.ToList());

    internal Entity Build() => new() { Id = _id, TypeId = _typeId, Properties = _properties };
}

public sealed class RelationTypeBuilder
{
    private readonly string _id;
    private string _fromType = "any";
    private List<string> _toTypes = ["any"];
    private List<string> _propertyNames = [];
    private string? _inverse;
    private bool _symmetric;
    private bool _auto;

    internal RelationTypeBuilder(string id) => _id = id;

    public RelationTypeBuilder From(string type) { _fromType = type; return this; }
    public RelationTypeBuilder To(params string[] types) { _toTypes = types.ToList(); return this; }
    public RelationTypeBuilder Properties(params string[] names) { _propertyNames = names.ToList(); return this; }
    public RelationTypeBuilder Inverse(string inverse) { _inverse = inverse; return this; }
    public RelationTypeBuilder Symmetric(bool value = true) { _symmetric = value; return this; }
    public RelationTypeBuilder Auto(bool value = true) { _auto = value; return this; }

    internal RelationType Build() => new()
    {
        Id = _id, FromType = _fromType, ToTypes = _toTypes,
        PropertyNames = _propertyNames, Inverse = _inverse,
        Symmetric = _symmetric, Auto = _auto
    };
}

public sealed class RelationBuilder
{
    private readonly string _fromId;
    private readonly string _typeId;
    private readonly string _toId;
    private readonly Dictionary<string, string> _properties = new();

    internal RelationBuilder(string fromId, string typeId, string toId)
    {
        _fromId = fromId; _typeId = typeId; _toId = toId;
    }

    public RelationBuilder Set(string name, string value) { _properties[name] = value; return this; }

    internal Relation Build() => new()
    {
        FromId = _fromId, ToId = _toId, TypeId = _typeId, Properties = _properties
    };
}

public sealed class TaxonomyBuilder
{
    private readonly string _id;
    private readonly string _label;
    private bool _facet;
    private readonly List<TaxonomyNode> _roots = [];

    internal TaxonomyBuilder(string id, string label) { _id = id; _label = label; }

    public TaxonomyBuilder Facet(bool value = true) { _facet = value; return this; }

    public TaxonomyBuilder Node(string id, string label, Action<TaxonomyNodeBuilder>? configure = null)
    {
        var nb = new TaxonomyNodeBuilder(id, label);
        configure?.Invoke(nb);
        _roots.Add(nb.Build(parent: null));
        return this;
    }

    internal Taxonomy Build() => new() { Id = _id, Label = _label, Facet = _facet, Roots = _roots };
}

public sealed class TaxonomyNodeBuilder
{
    private readonly string _id;
    private readonly string _label;
    private string? _description;
    private string? _color;
    private string? _icon;
    private readonly List<TaxonomyNodeBuilder> _children = [];

    internal TaxonomyNodeBuilder(string id, string label) { _id = id; _label = label; }

    public TaxonomyNodeBuilder Desc(string desc) { _description = desc; return this; }
    public TaxonomyNodeBuilder Color(string color) { _color = color; return this; }
    public TaxonomyNodeBuilder Icon(string icon) { _icon = icon; return this; }

    public TaxonomyNodeBuilder Child(string id, string label, Action<TaxonomyNodeBuilder>? configure = null)
    {
        var nb = new TaxonomyNodeBuilder(id, label);
        configure?.Invoke(nb);
        _children.Add(nb);
        return this;
    }

    internal TaxonomyNode Build(TaxonomyNode? parent)
    {
        var node = new TaxonomyNode
        {
            Id = _id, Label = _label, Description = _description,
            Color = _color, Icon = _icon, Parent = parent
        };
        foreach (var child in _children)
            node.Children.Add(child.Build(parent: node));
        return node;
    }
}

public sealed class ClassificationRuleBuilder
{
    private readonly string _id;
    private string _pattern = "";
    private RuleAction _action = RuleAction.Assign;
    private string? _taxonomyId;
    private string? _nodeId;
    private string? _extractType;
    private float _confidence = 0.5f;

    internal ClassificationRuleBuilder(string id) => _id = id;

    public ClassificationRuleBuilder When(string pattern) { _pattern = pattern; return this; }

    public ClassificationRuleBuilder ThenAssign(string taxonomyId, string nodeId)
    {
        _action = RuleAction.Assign;
        _taxonomyId = taxonomyId;
        _nodeId = nodeId;
        return this;
    }

    public ClassificationRuleBuilder ThenExtract(string typeName)
    {
        _action = RuleAction.Extract;
        _extractType = typeName;
        return this;
    }

    public ClassificationRuleBuilder Confidence(float value) { _confidence = value; return this; }

    internal ClassificationRule Build() => new()
    {
        Id = _id, Pattern = _pattern, Action = _action,
        TaxonomyId = _taxonomyId, NodeId = _nodeId,
        ExtractType = _extractType, Confidence = _confidence
    };
}
