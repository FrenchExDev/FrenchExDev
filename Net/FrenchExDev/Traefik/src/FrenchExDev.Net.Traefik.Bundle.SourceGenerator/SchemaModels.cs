using System;
using System.Collections.Generic;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator;

// All IR types implement IEquatable<T> with structural equality so the
// incremental generator pipeline can cache parsed models. They remain mutable
// classes (not records) because the parser builds them via property setters.

internal sealed class SchemaModel : IEquatable<SchemaModel>
{
    public string Version { get; set; } = "";
    public SchemaKind Kind { get; set; }
    public Dictionary<string, DefinitionModel> Definitions { get; set; } = new();
    public List<PropertyModel> RootProperties { get; set; } = new();

    public bool Equals(SchemaModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Version == other.Version
            && Kind == other.Kind
            && IrEquality.DictEqual(Definitions, other.Definitions)
            && IrEquality.ListEqual(RootProperties, other.RootProperties);
    }

    public override bool Equals(object? obj) => Equals(obj as SchemaModel);

    public override int GetHashCode() => IrEquality.Combine(
        IrEquality.HashOfString(Version),
        (int)Kind,
        IrEquality.DictHash(Definitions),
        IrEquality.ListHash(RootProperties));
}

internal enum SchemaKind
{
    Static,
    Dynamic
}

internal sealed class DefinitionModel : IEquatable<DefinitionModel>
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<PropertyModel> Properties { get; set; } = new();
    public bool IsOneOfDiscriminated { get; set; }
    public List<DiscriminatedBranch>? Branches { get; set; }

    public bool Equals(DefinitionModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && Description == other.Description
            && IsOneOfDiscriminated == other.IsOneOfDiscriminated
            && IrEquality.ListEqual(Properties, other.Properties)
            && IrEquality.ListEqual(Branches, other.Branches);
    }

    public override bool Equals(object? obj) => Equals(obj as DefinitionModel);

    public override int GetHashCode() => IrEquality.Combine(
        IrEquality.HashOfString(Name),
        IrEquality.HashOfString(Description),
        IsOneOfDiscriminated ? 1 : 0,
        IrEquality.ListHash(Properties),
        IrEquality.ListHash(Branches));
}

internal sealed class DiscriminatedBranch : IEquatable<DiscriminatedBranch>
{
    public string PropertyName { get; set; } = "";
    public string RefName { get; set; } = "";

    public bool Equals(DiscriminatedBranch? other)
    {
        if (other is null) return false;
        return PropertyName == other.PropertyName && RefName == other.RefName;
    }

    public override bool Equals(object? obj) => Equals(obj as DiscriminatedBranch);

    public override int GetHashCode() => IrEquality.Combine(
        IrEquality.HashOfString(PropertyName),
        IrEquality.HashOfString(RefName));
}

internal sealed class PropertyModel : IEquatable<PropertyModel>
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

    public bool Equals(PropertyModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return JsonName == other.JsonName
            && CSharpName == other.CSharpName
            && Description == other.Description
            && IsDeprecated == other.IsDeprecated
            && Type == other.Type
            && Ref == other.Ref
            && IsRequired == other.IsRequired
            && IsNullable == other.IsNullable
            && InlineClassName == other.InlineClassName
            && DictOfRefTarget == other.DictOfRefTarget
            && PatternPropsInlineClassName == other.PatternPropsInlineClassName
            && Equals(Items, other.Items)
            && IrEquality.ListEqual(EnumValues, other.EnumValues)
            && IrEquality.ListEqual(InlineObjectProperties, other.InlineObjectProperties)
            && IrEquality.ListEqual(PatternPropsInlineProperties, other.PatternPropsInlineProperties);
    }

    public override bool Equals(object? obj) => Equals(obj as PropertyModel);

    public override int GetHashCode() => IrEquality.Combine(
        IrEquality.HashOfString(JsonName),
        IrEquality.HashOfString(CSharpName),
        IrEquality.HashOfString(Description),
        IsDeprecated ? 1 : 0,
        (int)Type,
        IrEquality.HashOfString(Ref),
        IsRequired ? 1 : 0,
        IsNullable ? 1 : 0,
        IrEquality.HashOfString(InlineClassName),
        IrEquality.HashOfString(DictOfRefTarget),
        IrEquality.HashOfString(PatternPropsInlineClassName),
        Items?.GetHashCode() ?? 0,
        IrEquality.ListHash(EnumValues),
        IrEquality.ListHash(InlineObjectProperties),
        IrEquality.ListHash(PatternPropsInlineProperties));
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

// Unified schema (single-version pass-through for Traefik today; multi-version
// merging is supported but currently only one v3 schema is loaded).
internal sealed class UnifiedSchema : IEquatable<UnifiedSchema>
{
    public List<string> Versions { get; set; } = new();
    public Dictionary<string, UnifiedDefinition> Definitions { get; set; } = new();
    public List<UnifiedProperty> RootProperties { get; set; } = new();

    public bool Equals(UnifiedSchema? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return IrEquality.ListEqual(Versions, other.Versions)
            && IrEquality.DictEqual(Definitions, other.Definitions)
            && IrEquality.ListEqual(RootProperties, other.RootProperties);
    }

    public override bool Equals(object? obj) => Equals(obj as UnifiedSchema);

    public override int GetHashCode() => IrEquality.Combine(
        IrEquality.ListHash(Versions),
        IrEquality.DictHash(Definitions),
        IrEquality.ListHash(RootProperties));
}

internal sealed class UnifiedDefinition : IEquatable<UnifiedDefinition>
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<UnifiedProperty> Properties { get; set; } = new();
    public bool IsOneOfDiscriminated { get; set; }
    public List<DiscriminatedBranch>? Branches { get; set; }
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }

    public bool Equals(UnifiedDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && Description == other.Description
            && IsOneOfDiscriminated == other.IsOneOfDiscriminated
            && SinceVersion == other.SinceVersion
            && UntilVersion == other.UntilVersion
            && IrEquality.ListEqual(Properties, other.Properties)
            && IrEquality.ListEqual(Branches, other.Branches);
    }

    public override bool Equals(object? obj) => Equals(obj as UnifiedDefinition);

    public override int GetHashCode() => IrEquality.Combine(
        IrEquality.HashOfString(Name),
        IrEquality.HashOfString(Description),
        IsOneOfDiscriminated ? 1 : 0,
        IrEquality.HashOfString(SinceVersion),
        IrEquality.HashOfString(UntilVersion),
        IrEquality.ListHash(Properties),
        IrEquality.ListHash(Branches));
}

internal sealed class UnifiedProperty : IEquatable<UnifiedProperty>
{
    public PropertyModel Property { get; set; } = null!;
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }

    public bool Equals(UnifiedProperty? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return SinceVersion == other.SinceVersion
            && UntilVersion == other.UntilVersion
            && Equals(Property, other.Property);
    }

    public override bool Equals(object? obj) => Equals(obj as UnifiedProperty);

    public override int GetHashCode() => IrEquality.Combine(
        IrEquality.HashOfString(SinceVersion),
        IrEquality.HashOfString(UntilVersion),
        Property?.GetHashCode() ?? 0);
}
