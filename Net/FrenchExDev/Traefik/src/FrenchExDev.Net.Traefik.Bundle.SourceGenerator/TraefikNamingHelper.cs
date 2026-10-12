using System.Text;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator;

internal static class TraefikNamingHelper
{
    public static string ToPascalCase(string camelOrSnake)
    {
        var sb = new StringBuilder();
        var capitalizeNext = true;
        foreach (var c in camelOrSnake)
        {
            if (c == '_' || c == '-' || c == '.')
            {
                capitalizeNext = true;
                continue;
            }
            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }
        return sb.ToString();
    }

    public static string DefinitionToClassName(string definitionName)
    {
        var pascal = ToPascalCase(definitionName);

        // Strip common schema-organizational prefixes
        // Static schema: "static*", "types*", "acme*" keep as-is
        // Dynamic schema: names are already clean

        return "Traefik" + pascal;
    }

    public static string MapCSharpType(PropertyModel prop)
    {
        return prop.Type switch
        {
            PropertyType.String => "string?",
            PropertyType.Integer => "int?",
            PropertyType.Number => "double?",
            PropertyType.Boolean => "bool?",
            PropertyType.InlineObject when prop.InlineClassName is not null =>
                $"{prop.InlineClassName}?",
            PropertyType.Array when prop.Items is not null => MapArrayItemType(prop.Items),
            PropertyType.Array => "global::System.Collections.Generic.List<object>?",
            PropertyType.Object => "global::System.Collections.Generic.Dictionary<string, object?>?",
            PropertyType.Ref when prop.Ref is not null => $"{DefinitionToClassName(prop.Ref)}?",
            PropertyType.DictOfRef when prop.DictOfRefTarget is not null =>
                $"global::System.Collections.Generic.Dictionary<string, {DefinitionToClassName(prop.DictOfRefTarget)}>?",
            PropertyType.DictOfObject =>
                "global::System.Collections.Generic.Dictionary<string, object?>?",
            PropertyType.PatternPropsInline when prop.PatternPropsInlineClassName is not null =>
                $"global::System.Collections.Generic.Dictionary<string, {prop.PatternPropsInlineClassName}>?",
            _ => "object?"
        };
    }

    private static string MapArrayItemType(PropertyModel items)
    {
        if (items.Type == PropertyType.Ref && items.Ref is not null)
            return $"global::System.Collections.Generic.List<{DefinitionToClassName(items.Ref)}>?";
        if (items.Type == PropertyType.InlineObject && items.InlineClassName is not null)
            return $"global::System.Collections.Generic.List<{items.InlineClassName}>?";
        if (items.Type == PropertyType.String)
            return "global::System.Collections.Generic.List<string>?";
        if (items.Type == PropertyType.Integer)
            return "global::System.Collections.Generic.List<int>?";
        if (items.Type == PropertyType.Object)
            return "global::System.Collections.Generic.List<object>?";

        return "global::System.Collections.Generic.List<object>?";
    }
}
