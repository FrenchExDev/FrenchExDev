using System.Collections.Generic;
using FrenchExDev.Net.Builder.SourceGenerator.Lib;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.SourceGenerator;

/// <summary>
/// Converts schema properties to BuilderPropertyModels for builder emission.
/// </summary>
internal static class BuilderHelper
{
    public static BuilderEmitModel CreateBuilderModel(
        string ns, string className, List<UnifiedProperty> properties)
    {
        var builderProps = new List<BuilderPropertyModel>();
        foreach (var up in properties)
        {
            var prop = up.Property;
            var csharpType = NamingHelper.MapCSharpType(prop);
            var (isCollection, itemType) = DetectCollection(csharpType);
            var (isDict, dictKey, dictValue) = DetectDictionary(csharpType);
            var dictValueBuilder = isDict ? ResolveValueBuilderClassName(dictValue) : null;

            builderProps.Add(new BuilderPropertyModel(
                prop.CSharpName,
                csharpType,
                csharpType, // all types are already nullable
                isCollection,
                itemType,
                withMethodAttributes: BuildVersionAttributes(up),
                isDictionary: isDict,
                dictKeyTypeFull: dictKey,
                dictValueTypeFull: dictValue,
                dictValueBuilderClassName: dictValueBuilder,
                dictSingularName: dictValueBuilder is not null ? Singularize(prop.CSharpName) : null));
        }

        // Extensions property (present on every model class)
        builderProps.Add(CreateExtensionsProperty());

        return new BuilderEmitModel(
            ns,
            className,
            className + "Builder",
            builderProps);
    }

    public static BuilderEmitModel CreateRootBuilderModel(
        string ns, string className, List<UnifiedProperty> rootProperties)
    {
        var builderProps = new List<BuilderPropertyModel>();
        foreach (var up in rootProperties)
        {
            var prop = up.Property;
            var csharpType = NamingHelper.MapCSharpType(prop);
            var (isCollection, itemType) = DetectCollection(csharpType);
            var (isDict, dictKey, dictValue) = DetectDictionary(csharpType);
            var dictValueBuilder = isDict ? ResolveValueBuilderClassName(dictValue) : null;

            builderProps.Add(new BuilderPropertyModel(
                prop.CSharpName,
                csharpType,
                csharpType,
                isCollection,
                itemType,
                withMethodAttributes: BuildVersionAttributes(up),
                isDictionary: isDict,
                dictKeyTypeFull: dictKey,
                dictValueTypeFull: dictValue,
                dictValueBuilderClassName: dictValueBuilder,
                dictSingularName: dictValueBuilder is not null ? Singularize(prop.CSharpName) : null));
        }

        // Jobs dictionary
        const string jobsDictType = "global::System.Collections.Generic.Dictionary<string, GitLabCiJob>?";
        builderProps.Add(new BuilderPropertyModel(
            "Jobs",
            jobsDictType,
            jobsDictType,
            isDictionary: true,
            dictKeyTypeFull: "string",
            dictValueTypeFull: "GitLabCiJob",
            dictValueBuilderClassName: "GitLabCiJobBuilder",
            dictSingularName: "Job"));

        builderProps.Add(CreateExtensionsProperty());

        return new BuilderEmitModel(
            ns,
            className,
            className + "Builder",
            builderProps);
    }

    private static BuilderPropertyModel CreateExtensionsProperty()
    {
        const string extType = "global::System.Collections.Generic.Dictionary<string, object?>?";
        return new BuilderPropertyModel(
            "Extensions", extType, extType,
            isDictionary: true,
            dictKeyTypeFull: "string",
            dictValueTypeFull: "object?");
    }

    private static List<string>? BuildVersionAttributes(UnifiedProperty up)
    {
        if (up.SinceVersion is null && up.UntilVersion is null)
            return null;

        var attrs = new List<string>();
        if (up.SinceVersion is not null)
            attrs.Add($"[SinceVersion(\"{up.SinceVersion}\")]");
        if (up.UntilVersion is not null)
            attrs.Add($"[UntilVersion(\"{up.UntilVersion}\")]");
        return attrs;
    }

    private static (bool IsCollection, string? ItemType) DetectCollection(string csharpType)
    {
        const string listPrefix = "global::System.Collections.Generic.List<";

        if (csharpType.StartsWith(listPrefix))
        {
            var inner = csharpType.Substring(listPrefix.Length);
            var closingBracket = inner.LastIndexOf('>');
            if (closingBracket > 0)
            {
                var itemType = inner.Substring(0, closingBracket);
                return (true, itemType);
            }
        }

        return (false, null);
    }

    private static (bool IsDictionary, string? KeyType, string? ValueType) DetectDictionary(string csharpType)
    {
        const string dictPrefix = "global::System.Collections.Generic.Dictionary<";

        if (!csharpType.StartsWith(dictPrefix))
            return (false, null, null);

        var inner = csharpType.Substring(dictPrefix.Length);
        var closingBracket = inner.LastIndexOf('>');
        if (closingBracket <= 0)
            return (false, null, null);

        var keyValue = inner.Substring(0, closingBracket);
        var commaIdx = keyValue.IndexOf(", ");
        if (commaIdx <= 0)
            return (false, null, null);

        var keyType = keyValue.Substring(0, commaIdx);
        var valueType = keyValue.Substring(commaIdx + 2);
        return (true, keyType, valueType);
    }

    private static string? ResolveValueBuilderClassName(string? valueType)
    {
        if (valueType is null)
            return null;

        var clean = valueType.TrimEnd('?').Trim();

        if (clean == "object" || clean == "string" || clean.StartsWith("global::System"))
            return null;

        return clean + "Builder";
    }

    private static string? Singularize(string name)
    {
        if (name.EndsWith("s") && name.Length > 1)
            return name.Substring(0, name.Length - 1);
        return name;
    }
}
