using System.Collections.Generic;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator;

internal sealed class SchemaModel
{
    public string Version { get; set; } = "";
    public SchemaKind Kind { get; set; }
    public Dictionary<string, DefinitionModel> Definitions { get; set; } = new();
    public List<PropertyModel> RootProperties { get; set; } = new();
}

internal enum SchemaKind
{
    Static,
    Dynamic
}

internal sealed class DefinitionModel
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<PropertyModel> Properties { get; set; } = new();
    public bool IsOneOfDiscriminated { get; set; }
    public List<DiscriminatedBranch>? Branches { get; set; }
}

internal sealed class DiscriminatedBranch
{
    public string PropertyName { get; set; } = "";
    public string RefName { get; set; } = "";
}

internal sealed class PropertyModel
{
    public string JsonName { get; set; } = "";
    public string CSharpName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsDeprecated { get; set; }
    public PropertyType Type { get; set; } = PropertyType.String;
    public string? Ref { get; set; }
    public PropertyModel? Items { get; set; }
    public List<string>? EnumValues { get; set; }
    public bool IsRequired { get; set; }
    public bool IsNullable { get; set; }

    // For inline objects: the generated class name and its properties
    public string? InlineClassName { get; set; }
    public List<PropertyModel>? InlineObjectProperties { get; set; }

    // For DictOfRef: the referenced definition name for the dictionary value type
    public string? DictOfRefTarget { get; set; }

    // For patternProperties with inline objects
    public string? PatternPropsInlineClassName { get; set; }
    public List<PropertyModel>? PatternPropsInlineProperties { get; set; }
}

internal enum PropertyType
{
    String,
    Integer,
    Number,
    Boolean,
    Array,
    Object,
    InlineObject,
    Ref,
    DictOfRef,
    DictOfObject,
    PatternPropsInline,
}

// Unified schema (single-version pass-through for Traefik)
internal sealed class UnifiedSchema
{
    public List<string> Versions { get; set; } = new();
    public Dictionary<string, UnifiedDefinition> Definitions { get; set; } = new();
    public List<UnifiedProperty> RootProperties { get; set; } = new();
}

internal sealed class UnifiedDefinition
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<UnifiedProperty> Properties { get; set; } = new();
    public bool IsOneOfDiscriminated { get; set; }
    public List<DiscriminatedBranch>? Branches { get; set; }
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }
}

internal sealed class UnifiedProperty
{
    public PropertyModel Property { get; set; } = null!;
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }
}
