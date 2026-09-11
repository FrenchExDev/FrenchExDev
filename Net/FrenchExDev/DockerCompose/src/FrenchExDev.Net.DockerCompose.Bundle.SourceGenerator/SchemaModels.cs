using System.Collections.Generic;

namespace FrenchExDev.Net.DockerCompose.Bundle.SourceGenerator;

internal sealed class SchemaModel
{
    public string Version { get; set; } = "";
    public Dictionary<string, DefinitionModel> Definitions { get; set; } = new();
    public List<PropertyModel> RootProperties { get; set; } = new();
}

internal sealed class DefinitionModel
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<PropertyModel> Properties { get; set; } = new();
    public bool IsNullableType { get; set; }
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

    // For inline objects: the generated class name and its properties
    public string? InlineClassName { get; set; }
    public List<PropertyModel>? InlineObjectProperties { get; set; }

    // For oneOf[string, object]: the object-form class name
    public string? OneOfObjectClassName { get; set; }
    public List<PropertyModel>? OneOfObjectProperties { get; set; }
}

internal enum PropertyType
{
    String,
    Integer,
    Number,
    Boolean,
    StringOrBoolean,
    StringOrInteger,
    Array,
    Object,
    InlineObject,
    Ref,
    ListOrDict,
    StringOrList,
    StringOrObject,
    Command,
    ServiceConfigOrSecret,
    ExtraHosts,
    Ulimits
}

// Unified schema after merging multiple versions
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
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }
}

internal sealed class UnifiedProperty
{
    public PropertyModel Property { get; set; } = null!;
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }
}
