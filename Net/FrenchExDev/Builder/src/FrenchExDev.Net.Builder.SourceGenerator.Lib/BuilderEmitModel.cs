using System.Collections.Generic;

namespace FrenchExDev.Net.Builder.SourceGenerator.Lib;

/// <summary>
/// Describes a builder class to emit. No Roslyn dependency — works with plain strings.
/// </summary>
public sealed class BuilderEmitModel
{
    public BuilderEmitModel(
        string ns,
        string targetClassName,
        string builderClassName,
        IReadOnlyList<BuilderPropertyModel> properties,
        string? exceptionFullName = null,
        string instantiation = "init",
        string? preamble = null)
    {
        Namespace = ns;
        TargetClassName = targetClassName;
        BuilderClassName = builderClassName;
        Properties = properties;
        ExceptionFullName = exceptionFullName;
        Instantiation = instantiation;
        Preamble = preamble;
    }

    public string Namespace { get; }
    public string TargetClassName { get; }
    public string BuilderClassName { get; }
    public IReadOnlyList<BuilderPropertyModel> Properties { get; }
    public string? ExceptionFullName { get; }
    public string Instantiation { get; }

    /// <summary>
    /// Raw C# source inserted after the class opening brace, before input properties.
    /// Used for custom fields, constructors, etc.
    /// </summary>
    public string? Preamble { get; }
}

/// <summary>
/// Describes a single property for builder emission.
/// </summary>
public sealed class BuilderPropertyModel
{
    public BuilderPropertyModel(
        string name,
        string typeFull,
        string nullableTypeFull,
        bool isCollection = false,
        string? itemTypeFull = null,
        IReadOnlyList<string>? withMethodAttributes = null,
        string? withMethodBodyPrefix = null,
        string? instantiationExpression = null,
        bool isDictionary = false,
        string? dictKeyTypeFull = null,
        string? dictValueTypeFull = null,
        string? dictValueBuilderClassName = null,
        string? dictSingularName = null)
    {
        Name = name;
        TypeFull = typeFull;
        NullableTypeFull = nullableTypeFull;
        IsCollection = isCollection;
        ItemTypeFull = itemTypeFull;
        WithMethodAttributes = withMethodAttributes;
        WithMethodBodyPrefix = withMethodBodyPrefix;
        InstantiationExpression = instantiationExpression;
        IsDictionary = isDictionary;
        DictKeyTypeFull = dictKeyTypeFull;
        DictValueTypeFull = dictValueTypeFull;
        DictValueBuilderClassName = dictValueBuilderClassName;
        DictSingularName = dictSingularName;
    }

    public string Name { get; }
    public string TypeFull { get; }
    public string NullableTypeFull { get; }
    public bool IsCollection { get; }
    public string? ItemTypeFull { get; }

    /// <summary>
    /// Attribute lines emitted before the With*() method (e.g., "[SinceVersion(\"1.0\")]").
    /// </summary>
    public IReadOnlyList<string>? WithMethodAttributes { get; }

    /// <summary>
    /// Raw C# lines inserted at the start of the With*() method body, before the assignment.
    /// </summary>
    public string? WithMethodBodyPrefix { get; }

    /// <summary>
    /// Expression used instead of the property name in CreateInstance() (e.g., "PropName?.AsReadOnly()").
    /// When null, defaults to the property name.
    /// </summary>
    public string? InstantiationExpression { get; }

    /// <summary>True when the property is a Dictionary&lt;TKey, TValue&gt; type.</summary>
    public bool IsDictionary { get; }

    /// <summary>Fully-qualified key type (e.g., "string").</summary>
    public string? DictKeyTypeFull { get; }

    /// <summary>Fully-qualified value type, non-nullable (e.g., "ComposeService").</summary>
    public string? DictValueTypeFull { get; }

    /// <summary>Builder class name for the value type (e.g., "ComposeServiceBuilder"). Null if no builder exists.</summary>
    public string? DictValueBuilderClassName { get; }

    /// <summary>Singular name for single-entry convenience method (e.g., "Service" for Services). Null to skip.</summary>
    public string? DictSingularName { get; }
}
