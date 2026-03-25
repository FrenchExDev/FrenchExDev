using System.Collections.Generic;
using FrenchExDev.Net.Builder.SourceGenerator.Lib;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator;

internal static class TraefikBuilderHelper
{
    public static BuilderEmitModel CreateBuilderModel(
        string ns, string className, List<UnifiedProperty> properties)
    {
        var builderProps = new List<BuilderPropertyModel>();
        foreach (var up in properties)
        {
            var prop = up.Property;
            var csharpType = TraefikNamingHelper.MapCSharpType(prop);
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

        return new BuilderEmitModel(
            ns,
            className,
            className + "Builder",
            builderProps);
    }

    public static BuilderEmitModel CreateDiscriminatedBuilderModel(
        string ns, string className, List<DiscriminatedBranch> branches)
    {
        var builderProps = new List<BuilderPropertyModel>();
        foreach (var branch in branches)
        {
            var propName = TraefikNamingHelper.ToPascalCase(branch.PropertyName);
            var refClassName = TraefikNamingHelper.DefinitionToClassName(branch.RefName);
            var csharpType = $"{refClassName}?";

            builderProps.Add(new BuilderPropertyModel(
                propName,
                csharpType,
                csharpType));
        }

        return new BuilderEmitModel(
            ns,
            className,
            className + "Builder",
            builderProps);
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
