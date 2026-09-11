using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator;

internal static class TraefikSchemaReader
{
    public static SchemaModel Parse(string json, string version, SchemaKind kind)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var model = new SchemaModel { Version = version, Kind = kind };

        if (kind == SchemaKind.Dynamic)
        {
            // Dynamic schema has nested root: http { routers, services, middlewares }, tcp { ... }, etc.
            if (root.TryGetProperty("properties", out var rootProps))
            {
                foreach (var section in rootProps.EnumerateObject())
                {
                    // Each section (http, tcp, udp, tls) is itself an object with sub-properties
                    var sectionPm = new PropertyModel
                    {
                        JsonName = section.Name,
                        CSharpName = TraefikNamingHelper.ToPascalCase(section.Name),
                        Type = PropertyType.InlineObject,
                        InlineClassName = "TraefikDynamic" + TraefikNamingHelper.ToPascalCase(section.Name),
                    };

                    if (section.Value.TryGetProperty("properties", out var sectionProps))
                    {
                        sectionPm.InlineObjectProperties = ParseDynamicSectionProperties(
                            sectionProps, sectionPm.InlineClassName!);
                    }

                    model.RootProperties.Add(sectionPm);
                }
            }
        }
        else
        {
            // Static schema: standard root properties
            if (root.TryGetProperty("properties", out var rootProps))
                model.RootProperties = ParseProperties(rootProps, "TraefikStaticConfig");
        }

        // Parse definitions ($defs or definitions)
        JsonElement defs = default;
        bool hasDefs = root.TryGetProperty("$defs", out defs) ||
                       root.TryGetProperty("definitions", out defs);

        if (hasDefs)
        {
            foreach (var def in defs.EnumerateObject())
            {
                var defModel = ParseDefinition(def.Name, def.Value, kind);
                if (defModel is not null)
                    model.Definitions[def.Name] = defModel;
            }
        }

        return model;
    }

    public static SchemaKind DetectKind(string filename)
    {
        if (filename.Contains("file-provider"))
            return SchemaKind.Dynamic;
        return SchemaKind.Static;
    }

    public static string ExtractVersion(string filename)
    {
        // traefik-v3-static.json -> "3"
        // traefik-v3-file-provider.json -> "3"
        var name = Path.GetFileNameWithoutExtension(filename);
        if (name.StartsWith("traefik-v"))
        {
            var rest = name.Substring("traefik-v".Length);
            var dash = rest.IndexOf('-');
            return dash > 0 ? rest.Substring(0, dash) : rest;
        }
        return name;
    }

    private static List<PropertyModel> ParseDynamicSectionProperties(
        JsonElement propsElement, string parentClassName)
    {
        var result = new List<PropertyModel>();
        foreach (var prop in propsElement.EnumerateObject())
        {
            var pm = new PropertyModel
            {
                JsonName = prop.Name,
                CSharpName = TraefikNamingHelper.ToPascalCase(prop.Name),
            };

            var el = prop.Value;

            // Check for additionalProperties with $ref -> Dictionary<string, T>
            if (HasAdditionalPropertiesRef(el, out var apRef))
            {
                pm.Type = PropertyType.DictOfRef;
                pm.DictOfRefTarget = ExtractRef(apRef!);
                result.Add(pm);
                continue;
            }

            // Check for patternProperties with inline object
            if (el.TryGetProperty("patternProperties", out var pp))
            {
                foreach (var ppEntry in pp.EnumerateObject())
                {
                    if (ppEntry.Value.TryGetProperty("properties", out var ppInlineProps))
                    {
                        var inlineName = parentClassName + pm.CSharpName + "Entry";
                        pm.Type = PropertyType.PatternPropsInline;
                        pm.PatternPropsInlineClassName = inlineName;
                        pm.PatternPropsInlineProperties = ParseProperties(ppInlineProps, inlineName);
                    }
                    else
                    {
                        pm.Type = PropertyType.DictOfObject;
                    }
                    break; // only first pattern
                }
                result.Add(pm);
                continue;
            }

            // Check for array type (e.g., tls.certificates)
            if (el.TryGetProperty("type", out var typeEl))
            {
                var typeName = typeEl.ValueKind == JsonValueKind.String ? typeEl.GetString()! : "object";
                if (typeName == "array" && el.TryGetProperty("items", out var items))
                {
                    pm.Type = PropertyType.Array;
                    if (items.TryGetProperty("$ref", out var itemRef))
                    {
                        pm.Items = new PropertyModel { Ref = ExtractRef(itemRef.GetString()!), Type = PropertyType.Ref };
                    }
                    else if (items.TryGetProperty("properties", out var itemProps))
                    {
                        var itemClassName = parentClassName + pm.CSharpName + "Item";
                        pm.Items = new PropertyModel
                        {
                            Type = PropertyType.InlineObject,
                            InlineClassName = itemClassName,
                            InlineObjectProperties = ParseProperties(itemProps, itemClassName)
                        };
                    }
                    else
                    {
                        pm.Items = new PropertyModel { Type = PropertyType.Object };
                    }
                }
                else
                {
                    pm.Type = MapSingleType(typeName);
                }
            }

            result.Add(pm);
        }
        return result;
    }

    private static DefinitionModel? ParseDefinition(string name, JsonElement element, SchemaKind kind)
    {
        var def = new DefinitionModel { Name = name };

        if (element.TryGetProperty("description", out var desc))
            def.Description = desc.GetString();

        // Check for discriminated oneOf (httpMiddleware, httpService, etc.)
        if (element.TryGetProperty("oneOf", out var oneOf))
        {
            if (IsDiscriminatedOneOf(oneOf))
            {
                def.IsOneOfDiscriminated = true;
                def.Branches = ParseDiscriminatedBranches(oneOf);
                return def;
            }
        }

        if (element.TryGetProperty("properties", out var props))
        {
            var parentName = TraefikNamingHelper.DefinitionToClassName(name);
            def.Properties = ParseProperties(props, parentName);
        }

        // Handle definitions with additionalProperties but no properties (e.g., pluginMiddleware)
        // These are opaque object types
        if (def.Properties.Count == 0 && !def.IsOneOfDiscriminated)
        {
            // Check if it's an empty type object (like CertificateResolverTailscaleStruct)
            // or an additionalProperties-only type (like pluginMiddleware)
            if (!element.TryGetProperty("properties", out _))
            {
                // No properties at all - it's an opaque type, skip it unless it has additionalProperties: {"type":"object"}
                if (element.TryGetProperty("additionalProperties", out var ap) &&
                    ap.ValueKind == JsonValueKind.Object)
                {
                    // pluginMiddleware: additionalProperties: {type: "object"}
                    // Emit as Dictionary<string, object?>
                    return def;
                }
            }
        }

        return def;
    }

    private static List<PropertyModel> ParseProperties(JsonElement propsElement, string parentClassName)
    {
        var result = new List<PropertyModel>();
        foreach (var prop in propsElement.EnumerateObject())
        {
            var pm = ParseProperty(prop.Name, prop.Value, parentClassName);
            if (pm is not null)
                result.Add(pm);
        }
        return result;
    }

    private static PropertyModel ParseProperty(string name, JsonElement element, string parentClassName)
    {
        var pm = new PropertyModel
        {
            JsonName = name,
            CSharpName = TraefikNamingHelper.ToPascalCase(name)
        };

        if (element.TryGetProperty("description", out var desc))
            pm.Description = desc.GetString();

        if (element.TryGetProperty("deprecated", out var dep) && dep.GetBoolean())
            pm.IsDeprecated = true;

        // $ref
        if (element.TryGetProperty("$ref", out var refProp))
        {
            pm.Ref = ExtractRef(refProp.GetString()!);
            pm.Type = PropertyType.Ref;
            return pm;
        }

        // additionalProperties with $ref (Dictionary<string, T>)
        if (HasAdditionalPropertiesRef(element, out var apRef))
        {
            pm.Type = PropertyType.DictOfRef;
            pm.DictOfRefTarget = ExtractRef(apRef!);
            return pm;
        }

        // type
        if (element.TryGetProperty("type", out var typeEl))
        {
            // Handle nullable array types: ["array", "null"] or ["string", "null"]
            if (typeEl.ValueKind == JsonValueKind.Array)
            {
                var types = typeEl.EnumerateArray().Select(t => t.GetString()!).ToList();
                var nonNull = types.Where(t => t != "null").ToList();
                pm.IsNullable = types.Contains("null");

                if (nonNull.Count == 1)
                {
                    var actualType = nonNull[0];
                    if (actualType == "array" && element.TryGetProperty("items", out var items))
                    {
                        pm.Type = PropertyType.Array;
                        ParseArrayItems(pm, items, parentClassName);
                        return pm;
                    }
                    pm.Type = MapSingleType(actualType);
                    return pm;
                }

                pm.Type = PropertyType.String;
                return pm;
            }

            var typeName = typeEl.GetString()!;

            // Inline object with properties -> generate a class
            if (typeName == "object" && element.TryGetProperty("properties", out var objProps))
            {
                var className = parentClassName + pm.CSharpName;
                pm.Type = PropertyType.InlineObject;
                pm.InlineClassName = className;
                pm.InlineObjectProperties = ParseProperties(objProps, className);
                return pm;
            }

            // Object with additionalProperties as $ref (but we already handled that above)
            // Object without properties -> Dictionary<string, object?>
            if (typeName == "object" && !element.TryGetProperty("properties", out _))
            {
                // Check patternProperties
                if (element.TryGetProperty("patternProperties", out var pp))
                {
                    foreach (var ppEntry in pp.EnumerateObject())
                    {
                        if (ppEntry.Value.TryGetProperty("properties", out var ppInlineProps))
                        {
                            var inlineName = parentClassName + pm.CSharpName + "Entry";
                            pm.Type = PropertyType.PatternPropsInline;
                            pm.PatternPropsInlineClassName = inlineName;
                            pm.PatternPropsInlineProperties = ParseProperties(ppInlineProps, inlineName);
                            return pm;
                        }
                    }
                }

                pm.Type = PropertyType.Object;
                return pm;
            }

            pm.Type = MapSingleType(typeName);

            if (typeName == "array" && element.TryGetProperty("items", out var arrayItems))
            {
                ParseArrayItems(pm, arrayItems, parentClassName);
            }
        }

        // enum
        if (element.TryGetProperty("enum", out var enumEl))
        {
            pm.EnumValues = new List<string>();
            foreach (var e in enumEl.EnumerateArray())
                if (e.ValueKind == JsonValueKind.String)
                    pm.EnumValues.Add(e.GetString()!);
        }

        return pm;
    }

    private static void ParseArrayItems(PropertyModel pm, JsonElement items, string parentClassName)
    {
        if (items.TryGetProperty("$ref", out var itemRef))
        {
            pm.Items = new PropertyModel
            {
                Ref = ExtractRef(itemRef.GetString()!),
                Type = PropertyType.Ref
            };
        }
        else if (items.TryGetProperty("type", out var itemType))
        {
            if (itemType.GetString() == "object" && items.TryGetProperty("properties", out var itemObjProps))
            {
                var itemClassName = parentClassName + pm.CSharpName + "Item";
                pm.Items = new PropertyModel
                {
                    Type = PropertyType.InlineObject,
                    InlineClassName = itemClassName,
                    InlineObjectProperties = ParseProperties(itemObjProps, itemClassName)
                };
            }
            else
            {
                pm.Items = new PropertyModel { Type = MapSingleType(itemType.GetString()!) };
            }
        }
    }

    private static bool HasAdditionalPropertiesRef(JsonElement element, out string? refValue)
    {
        refValue = null;
        if (element.TryGetProperty("type", out var t) &&
            t.ValueKind == JsonValueKind.String && t.GetString() == "object" &&
            !element.TryGetProperty("properties", out _) &&
            element.TryGetProperty("additionalProperties", out var ap) &&
            ap.ValueKind == JsonValueKind.Object &&
            ap.TryGetProperty("$ref", out var apRefEl))
        {
            refValue = apRefEl.GetString();
            return true;
        }
        return false;
    }

    private static bool IsDiscriminatedOneOf(JsonElement oneOf)
    {
        // Each branch must be an object with exactly 1 property that has a $ref
        foreach (var item in oneOf.EnumerateArray())
        {
            if (!item.TryGetProperty("properties", out var props))
                return false;
            var propCount = 0;
            var hasRef = false;
            foreach (var p in props.EnumerateObject())
            {
                propCount++;
                if (p.Value.TryGetProperty("$ref", out _))
                    hasRef = true;
            }
            if (propCount != 1 || !hasRef)
                return false;
        }
        return true;
    }

    private static List<DiscriminatedBranch> ParseDiscriminatedBranches(JsonElement oneOf)
    {
        var branches = new List<DiscriminatedBranch>();
        foreach (var item in oneOf.EnumerateArray())
        {
            if (item.TryGetProperty("properties", out var props))
            {
                foreach (var p in props.EnumerateObject())
                {
                    if (p.Value.TryGetProperty("$ref", out var refEl))
                    {
                        branches.Add(new DiscriminatedBranch
                        {
                            PropertyName = p.Name,
                            RefName = ExtractRef(refEl.GetString()!)
                        });
                    }
                }
            }
        }
        return branches;
    }

    private static string ExtractRef(string refPath)
    {
        const string defsPrefix = "#/$defs/";
        const string definitionsPrefix = "#/definitions/";
        if (refPath.StartsWith(defsPrefix))
            return refPath.Substring(defsPrefix.Length);
        if (refPath.StartsWith(definitionsPrefix))
            return refPath.Substring(definitionsPrefix.Length);
        return refPath;
    }

    private static PropertyType MapSingleType(string typeName)
    {
        return typeName switch
        {
            "string" => PropertyType.String,
            "integer" => PropertyType.Integer,
            "number" => PropertyType.Number,
            "boolean" => PropertyType.Boolean,
            "array" => PropertyType.Array,
            "object" => PropertyType.Object,
            _ => PropertyType.String
        };
    }
}
