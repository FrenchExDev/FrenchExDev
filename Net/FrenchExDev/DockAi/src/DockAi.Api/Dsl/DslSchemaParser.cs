using DockAi.Api.Models;

namespace DockAi.Api.Dsl;

/// <summary>
/// Parses DSL schema files (ontology.dsl) into in-memory model objects.
/// </summary>
public sealed class DslSchemaParser
{
    private List<DslToken> _tokens = [];
    private int _pos;

    // Parsed results
    public Dictionary<string, EntityType> EntityTypes { get; } = new();
    public Dictionary<string, Entity> Entities { get; } = new();
    public Dictionary<string, RelationType> RelationTypes { get; } = new();
    public List<Relation> Relations { get; } = [];
    public Dictionary<string, Taxonomy> Taxonomies { get; } = new();
    public List<ClassificationRule> Rules { get; } = [];

    public void Parse(string source)
    {
        _tokens = new DslLexer(source).Tokenize();
        _pos = 0;

        while (!IsAtEnd())
        {
            SkipNewlines();
            if (IsAtEnd()) break;

            var token = Current();
            switch (token.Type)
            {
                case DslTokenType.KwType:
                    ParseEntityType();
                    break;
                case DslTokenType.KwEntity:
                    ParseEntity();
                    break;
                case DslTokenType.KwRelationType:
                    ParseRelationType();
                    break;
                case DslTokenType.KwRelation:
                    ParseRelation();
                    break;
                case DslTokenType.KwTaxonomy:
                    ParseTaxonomy();
                    break;
                case DslTokenType.KwRule:
                    ParseRule();
                    break;
                case DslTokenType.Comment:
                    Advance();
                    break;
                default:
                    Advance(); // skip unknown
                    break;
            }
        }
    }

    /// <summary>Export parsed results as an OntologySchema.</summary>
    public OntologySchema ToSchema() => new()
    {
        EntityTypes = new(EntityTypes),
        Entities = new(Entities),
        RelationTypes = new(RelationTypes),
        Relations = [.. Relations],
        Taxonomies = new(Taxonomies),
        Rules = [.. Rules]
    };

    // ── @type person { name: string, role: enum [...] } ──

    private void ParseEntityType()
    {
        Advance(); // @type
        var id = ExpectIdentifier();
        ExpectToken(DslTokenType.LeftBrace);

        var props = new Dictionary<string, PropertyDef>();
        while (!Check(DslTokenType.RightBrace) && !IsAtEnd())
        {
            SkipNewlines();
            if (Check(DslTokenType.RightBrace)) break;
            if (Check(DslTokenType.Comment)) { Advance(); continue; }

            var propName = ExpectIdentifier();
            ExpectToken(DslTokenType.Colon);
            var propDef = ParsePropertyDef(propName);
            props[propName] = propDef;
        }
        ExpectToken(DslTokenType.RightBrace);

        EntityTypes[id] = new EntityType { Id = id, Properties = props };
    }

    private PropertyDef ParsePropertyDef(string name)
    {
        var token = Current();
        if (token.Type == DslTokenType.Identifier && token.Value == "enum")
        {
            Advance(); // enum
            var values = ParseBracketedList();
            return new PropertyDef { Name = name, Kind = PropertyKind.Enum, EnumValues = values };
        }

        var typeStr = ExpectIdentifier();
        var isArray = false;
        if (Check(DslTokenType.LeftBracket))
        {
            Advance();
            ExpectToken(DslTokenType.RightBracket);
            isArray = true;
        }

        var kind = typeStr.ToLowerInvariant() switch
        {
            "string" when isArray => PropertyKind.StringArray,
            "string" => PropertyKind.String,
            "number" or "int" or "float" => PropertyKind.Number,
            "date" => PropertyKind.Date,
            "bool" or "boolean" => PropertyKind.Bool,
            _ => PropertyKind.String
        };

        return new PropertyDef { Name = name, Kind = kind };
    }

    // ── @entity erard : person { ... } ──

    private void ParseEntity()
    {
        Advance(); // @entity
        var id = ExpectIdentifier();
        ExpectToken(DslTokenType.Colon);
        var typeId = ExpectIdentifier();
        ExpectToken(DslTokenType.LeftBrace);

        var props = new Dictionary<string, object?>();
        while (!Check(DslTokenType.RightBrace) && !IsAtEnd())
        {
            SkipNewlines();
            if (Check(DslTokenType.RightBrace)) break;
            if (Check(DslTokenType.Comment)) { Advance(); continue; }

            var propName = ExpectIdentifier();
            ExpectToken(DslTokenType.Colon);
            var value = ParseValue();
            props[propName] = value;
        }
        ExpectToken(DslTokenType.RightBrace);

        Entities[id] = new Entity { Id = id, TypeId = typeId, Properties = props };
    }

    // ── @relation.type employs { from: organization, to: person, ... } ──

    private void ParseRelationType()
    {
        Advance(); // @relation.type
        var id = ExpectIdentifier();
        ExpectToken(DslTokenType.LeftBrace);

        string? from = null;
        var to = new List<string>();
        var propNames = new List<string>();
        string? inverse = null;
        bool symmetric = false;
        bool auto = false;

        while (!Check(DslTokenType.RightBrace) && !IsAtEnd())
        {
            SkipNewlines();
            if (Check(DslTokenType.RightBrace)) break;
            if (Check(DslTokenType.Comment)) { Advance(); continue; }

            var key = ExpectIdentifier();
            ExpectToken(DslTokenType.Colon);

            switch (key)
            {
                case "from":
                    from = ExpectIdentifier();
                    break;
                case "to":
                    if (Check(DslTokenType.LeftBracket))
                        to = ParseBracketedList();
                    else
                        to.Add(ExpectIdentifier());
                    break;
                case "properties":
                    propNames = ParseBracketedList();
                    break;
                case "inverse":
                    inverse = ExpectIdentifier();
                    break;
                case "symmetric":
                    symmetric = ExpectBool();
                    break;
                case "auto":
                    auto = ExpectBool();
                    break;
                default:
                    ParseValue(); // skip unknown
                    break;
            }
        }
        ExpectToken(DslTokenType.RightBrace);

        RelationTypes[id] = new RelationType
        {
            Id = id,
            FromType = from ?? "any",
            ToTypes = to.Count > 0 ? to : ["any"],
            PropertyNames = propNames,
            Inverse = inverse,
            Symmetric = symmetric,
            Auto = auto
        };
    }

    // ── @relation erard -[employed_by]-> qwant { ... } ──

    private void ParseRelation()
    {
        Advance(); // @relation
        var fromId = ExpectIdentifier();
        ExpectToken(DslTokenType.DashBracket);
        var typeId = ExpectIdentifier();
        ExpectToken(DslTokenType.BracketDash);
        var toId = ExpectIdentifier();

        var props = new Dictionary<string, string>();
        if (Check(DslTokenType.LeftBrace))
        {
            Advance();
            while (!Check(DslTokenType.RightBrace) && !IsAtEnd())
            {
                SkipNewlines();
                if (Check(DslTokenType.RightBrace)) break;
                if (Check(DslTokenType.Comment)) { Advance(); continue; }

                var key = ExpectIdentifier();
                ExpectToken(DslTokenType.Colon);
                var val = ExpectStringOrIdentifier();
                props[key] = val;
            }
            ExpectToken(DslTokenType.RightBrace);
        }

        Relations.Add(new Relation { FromId = fromId, ToId = toId, TypeId = typeId, Properties = props });
    }

    // ── @taxonomy track { label: "...", facet: true, voie_a "label" { ... } } ──

    private void ParseTaxonomy()
    {
        Advance(); // @taxonomy
        var id = ExpectIdentifier();
        ExpectToken(DslTokenType.LeftBrace);

        string? label = null;
        bool facet = false;
        var roots = new List<TaxonomyNode>();

        while (!Check(DslTokenType.RightBrace) && !IsAtEnd())
        {
            SkipNewlines();
            if (Check(DslTokenType.RightBrace)) break;
            if (Check(DslTokenType.Comment)) { Advance(); continue; }

            // Peek: is this a property (key: value) or a node (id "label" { ... }) ?
            if (Check(DslTokenType.Identifier) && PeekNext().Type == DslTokenType.Colon)
            {
                var key = ExpectIdentifier();
                ExpectToken(DslTokenType.Colon);
                switch (key)
                {
                    case "label":
                        label = ExpectStringOrIdentifier();
                        break;
                    case "facet":
                        facet = ExpectBool();
                        break;
                    default:
                        ParseValue();
                        break;
                }
            }
            else if (Check(DslTokenType.Identifier))
            {
                roots.Add(ParseTaxonomyNode(null));
            }
            else
            {
                Advance();
            }
        }
        ExpectToken(DslTokenType.RightBrace);

        Taxonomies[id] = new Taxonomy { Id = id, Label = label, Facet = facet, Roots = roots };
    }

    private TaxonomyNode ParseTaxonomyNode(TaxonomyNode? parent)
    {
        var nodeId = ExpectIdentifier();
        var nodeLabel = ExpectStringOrIdentifier();

        string? desc = null;
        string? color = null;
        string? icon = null;
        var children = new List<TaxonomyNode>();

        if (Check(DslTokenType.LeftBrace))
        {
            Advance();
            while (!Check(DslTokenType.RightBrace) && !IsAtEnd())
            {
                SkipNewlines();
                if (Check(DslTokenType.RightBrace)) break;
                if (Check(DslTokenType.Comment)) { Advance(); continue; }

                // Property or child node?
                if (Check(DslTokenType.Identifier) && PeekNext().Type == DslTokenType.Colon)
                {
                    var key = ExpectIdentifier();
                    ExpectToken(DslTokenType.Colon);
                    switch (key)
                    {
                        case "desc": desc = ExpectStringOrIdentifier(); break;
                        case "color": color = ExpectStringOrIdentifier(); break;
                        case "icon": icon = ExpectStringOrIdentifier(); break;
                        default: ParseValue(); break;
                    }
                }
                else if (Check(DslTokenType.Identifier))
                {
                    // Child taxonomy node
                    var node = new TaxonomyNode
                    {
                        Id = nodeId,
                        Label = nodeLabel,
                        Description = desc,
                        Color = color,
                        Icon = icon,
                        Parent = parent,
                        Children = children
                    };
                    // Actually we need to parse child first, then set parent
                    children.Add(ParseTaxonomyNode(null)); // parent set below
                }
                else
                {
                    Advance();
                }
            }
            ExpectToken(DslTokenType.RightBrace);
        }

        var result = new TaxonomyNode
        {
            Id = nodeId,
            Label = nodeLabel,
            Description = desc,
            Color = color,
            Icon = icon,
            Parent = parent,
            Children = children
        };

        // Fix parent references
        foreach (var child in children)
            child.Parent = result;

        return result;
    }

    // ── @rule auto_track { when content matches "pattern", then assign track: voie_a, confidence: 0.7 } ──

    private void ParseRule()
    {
        Advance(); // @rule
        var id = ExpectIdentifier();
        ExpectToken(DslTokenType.LeftBrace);

        string? pattern = null;
        RuleAction action = RuleAction.Assign;
        string? taxonomyId = null;
        string? nodeId = null;
        string? extractType = null;
        float confidence = 0.5f;

        while (!Check(DslTokenType.RightBrace) && !IsAtEnd())
        {
            SkipNewlines();
            if (Check(DslTokenType.RightBrace)) break;
            if (Check(DslTokenType.Comment)) { Advance(); continue; }

            var key = ExpectIdentifier();

            if (key == "when")
            {
                // when content matches "pattern"
                ExpectIdentifier(); // content
                ExpectIdentifier(); // matches
                pattern = ExpectString();
            }
            else if (key == "then")
            {
                var verb = ExpectIdentifier(); // assign or extract
                if (verb == "assign")
                {
                    action = RuleAction.Assign;
                    taxonomyId = ExpectIdentifier();
                    ExpectToken(DslTokenType.Colon);
                    nodeId = ExpectIdentifier();
                }
                else if (verb == "extract")
                {
                    action = RuleAction.Extract;
                    extractType = ExpectIdentifier();
                }
            }
            else if (key == "confidence")
            {
                ExpectToken(DslTokenType.Colon);
                confidence = float.Parse(Current().Value, System.Globalization.CultureInfo.InvariantCulture);
                Advance();
            }
            else
            {
                // skip
                if (Check(DslTokenType.Colon)) { Advance(); ParseValue(); }
            }
        }
        ExpectToken(DslTokenType.RightBrace);

        if (pattern is not null)
        {
            Rules.Add(new ClassificationRule
            {
                Id = id,
                Pattern = pattern,
                Action = action,
                TaxonomyId = taxonomyId,
                NodeId = nodeId,
                ExtractType = extractType,
                Confidence = confidence
            });
        }
    }

    // ── Helpers ──

    private object? ParseValue()
    {
        var token = Current();
        switch (token.Type)
        {
            case DslTokenType.QuotedString:
                Advance();
                return token.Value;
            case DslTokenType.Number:
                Advance();
                return double.TryParse(token.Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : token.Value;
            case DslTokenType.Bool:
                Advance();
                return token.Value == "true";
            case DslTokenType.Identifier:
                Advance();
                return token.Value;
            case DslTokenType.LeftBracket:
                return ParseBracketedListValues();
            default:
                Advance();
                return null;
        }
    }

    private List<string> ParseBracketedList()
    {
        ExpectToken(DslTokenType.LeftBracket);
        var items = new List<string>();
        while (!Check(DslTokenType.RightBracket) && !IsAtEnd())
        {
            SkipNewlines();
            if (Check(DslTokenType.RightBracket)) break;
            items.Add(ExpectStringOrIdentifier());
            if (Check(DslTokenType.Comma)) Advance();
        }
        ExpectToken(DslTokenType.RightBracket);
        return items;
    }

    private List<string> ParseBracketedListValues()
    {
        ExpectToken(DslTokenType.LeftBracket);
        var items = new List<string>();
        while (!Check(DslTokenType.RightBracket) && !IsAtEnd())
        {
            SkipNewlines();
            if (Check(DslTokenType.RightBracket)) break;
            items.Add(ExpectStringOrIdentifier());
            if (Check(DslTokenType.Comma)) Advance();
        }
        ExpectToken(DslTokenType.RightBracket);
        return items;
    }

    private string ExpectIdentifier()
    {
        SkipNewlines();
        var token = Current();
        if (token.Type is DslTokenType.Identifier or DslTokenType.QuotedString)
        {
            Advance();
            return token.Value;
        }
        throw new DslParseException($"Expected identifier at line {token.Line}:{token.Column}, got {token.Type} '{token.Value}'");
    }

    private string ExpectString()
    {
        SkipNewlines();
        var token = Current();
        if (token.Type == DslTokenType.QuotedString)
        {
            Advance();
            return token.Value;
        }
        throw new DslParseException($"Expected quoted string at line {token.Line}:{token.Column}, got {token.Type}");
    }

    private string ExpectStringOrIdentifier()
    {
        SkipNewlines();
        var token = Current();
        if (token.Type is DslTokenType.QuotedString or DslTokenType.Identifier or DslTokenType.Number)
        {
            Advance();
            return token.Value;
        }
        throw new DslParseException($"Expected string or identifier at line {token.Line}:{token.Column}, got {token.Type}");
    }

    private bool ExpectBool()
    {
        SkipNewlines();
        var token = Current();
        if (token.Type == DslTokenType.Bool)
        {
            Advance();
            return token.Value == "true";
        }
        if (token.Type == DslTokenType.Identifier && token.Value is "true" or "false")
        {
            Advance();
            return token.Value == "true";
        }
        throw new DslParseException($"Expected bool at line {token.Line}:{token.Column}, got {token.Type}");
    }

    private void ExpectToken(DslTokenType type)
    {
        SkipNewlines();
        var token = Current();
        if (token.Type != type)
            throw new DslParseException($"Expected {type} at line {token.Line}:{token.Column}, got {token.Type} '{token.Value}'");
        Advance();
    }

    private DslToken Current() => _pos < _tokens.Count ? _tokens[_pos] : new DslToken(DslTokenType.Eof, "", 0, 0);
    private DslToken PeekNext() => _pos + 1 < _tokens.Count ? _tokens[_pos + 1] : new DslToken(DslTokenType.Eof, "", 0, 0);
    private bool Check(DslTokenType type) => Current().Type == type;
    private bool IsAtEnd() => _pos >= _tokens.Count || Current().Type == DslTokenType.Eof;
    private void Advance() { if (_pos < _tokens.Count) _pos++; }

    private void SkipNewlines()
    {
        while (_pos < _tokens.Count && _tokens[_pos].Type is DslTokenType.Newline or DslTokenType.Comment)
            _pos++;
    }
}

public class DslParseException : Exception
{
    public DslParseException(string message) : base(message) { }
}
