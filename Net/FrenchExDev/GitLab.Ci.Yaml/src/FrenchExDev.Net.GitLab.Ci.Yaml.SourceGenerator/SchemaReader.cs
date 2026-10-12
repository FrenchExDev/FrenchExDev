using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.SourceGenerator;

internal static class SchemaReader
{
    public static SchemaModel Parse(string json, string version)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var model = new SchemaModel { Version = version };

        if (root.TryGetProperty("properties", out var rootProps))
            model.RootProperties = ParseProperties(rootProps, "GitLabCi");

        // Support both "definitions" (draft-07) and "$defs" (2020-12)
        JsonElement defs = default;
        var hasDefs = root.TryGetProperty("$defs", out defs) ||
                      root.TryGetProperty("definitions", out defs);

        if (hasDefs)
        {
            // First pass: parse all definitions
            foreach (var def in defs.EnumerateObject())
            {
                var defModel = ParseDefinition(def.Name, def.Value);
                if (defModel is not null)
                    model.Definitions[def.Name] = defModel;
            }

            // Second pass: resolve allOf references (e.g., "job" -> allOf[{$ref: "job_template"}])
            foreach (var def in defs.EnumerateObject())
            {
                if (def.Value.TryGetProperty("allOf", out var allOf))
                {
                    ResolveAllOf(def.Name, allOf, model.Definitions);
                }
            }
        }

        return model;
    }

    private static void ResolveAllOf(string defName, JsonElement allOf, Dictionary<string, DefinitionModel> definitions)
    {
        if (!definitions.TryGetValue(defName, out var target))
            return;

        // If the definition has no properties of its own, merge from allOf refs
        if (target.Properties.Count > 0)
            return;

        foreach (var item in allOf.EnumerateArray())
        {
            if (item.TryGetProperty("$ref", out var refProp))
            {
                var refName = ExtractRef(refProp.GetString()!);
                if (definitions.TryGetValue(refName, out var source))
                {
                    // Copy properties from the referenced definition
                    foreach (var prop in source.Properties)
                    {
                        if (!target.Properties.Exists(p => p.JsonName == prop.JsonName))
                            target.Properties.Add(prop);
                    }
                    target.Description ??= source.Description;
                }
            }
        }
    }

    public static string ExtractVersion(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        var prefix = "gitlab-ci-v";
        if (name.StartsWith(prefix))
            return name.Substring(prefix.Length);
        return name;
    }

    private static DefinitionModel ParseDefinition(string name, JsonElement element)
    {
        var def = new DefinitionModel { Name = name };

        if (element.TryGetProperty("description", out var desc))
            def.Description = desc.GetString();
        else if (element.TryGetProperty("markdownDescription", out var mdDesc))
            def.Description = mdDesc.GetString();

        if (element.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in typeEl.EnumerateArray())
                if (t.GetString() == "null")
                    def.IsNullableType = true;
        }

        if (element.TryGetProperty("properties", out var props))
        {
            var parentName = NamingHelper.DefinitionToClassName(name);
            def.Properties = ParseProperties(props, parentName);
        }

        return def;
    }

    private static readonly HashSet<string> SkippedProperties = new()
    {
        "$schema", "!reference",
    };

    private static List<PropertyModel> ParseProperties(JsonElement propsElement, string parentClassName)
    {
        var result = new List<PropertyModel>();
        foreach (var prop in propsElement.EnumerateObject())
        {
            if (SkippedProperties.Contains(prop.Name))
                continue;
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
            CSharpName = NamingHelper.ToPascalCase(name)
        };

        if (element.TryGetProperty("description", out var desc))
            pm.Description = desc.GetString();
        else if (element.TryGetProperty("markdownDescription", out var mdDesc))
            pm.Description = mdDesc.GetString();

        if (element.TryGetProperty("deprecated", out var dep) && dep.GetBoolean())
            pm.IsDeprecated = true;

        // $ref — support both #/definitions/ and #/$defs/
        if (element.TryGetProperty("$ref", out var refProp))
        {
            var refName = ExtractRef(refProp.GetString()!);
            pm.Ref = refName;
            pm.Type = MapRefToType(refName);
            return pm;
        }

        // oneOf
        if (element.TryGetProperty("oneOf", out var oneOf))
        {
            ParseOneOf(pm, oneOf, parentClassName);
            return pm;
        }

        // anyOf (treated same as oneOf for code gen purposes)
        if (element.TryGetProperty("anyOf", out var anyOf))
        {
            ParseOneOf(pm, anyOf, parentClassName);
            return pm;
        }

        // type
        if (element.TryGetProperty("type", out var typeEl))
        {
            if (typeEl.ValueKind == JsonValueKind.Array)
            {
                var types = typeEl.EnumerateArray().Select(t => t.GetString()!).ToList();
                pm.Type = MapMultiType(types);
            }
            else
            {
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

                pm.Type = MapSingleType(typeName);

                if (typeName == "array" && element.TryGetProperty("items", out var items))
                {
                    ParseArrayItems(pm, items, parentClassName);
                }
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
            pm.Items = new PropertyModel { Ref = ExtractRef(itemRef.GetString()!), Type = PropertyType.Ref };
        }
        else if (items.TryGetProperty("type", out var itemType))
        {
            if (itemType.ValueKind == JsonValueKind.Array)
            {
                var itemTypes = itemType.EnumerateArray().Select(t => t.GetString()!).ToList();
                pm.Items = new PropertyModel { Type = MapMultiType(itemTypes) };
            }
            else if (itemType.GetString() == "object" && items.TryGetProperty("properties", out var itemObjProps))
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
        else if (items.TryGetProperty("oneOf", out var itemOneOf))
        {
            pm.Items = new PropertyModel { Type = PropertyType.String };
            ParseOneOf(pm.Items, itemOneOf, parentClassName + pm.CSharpName);
        }
        else if (items.TryGetProperty("anyOf", out var itemAnyOf))
        {
            pm.Items = new PropertyModel { Type = PropertyType.String };
            ParseOneOf(pm.Items, itemAnyOf, parentClassName + pm.CSharpName);
        }
    }

    private static void ParseOneOf(PropertyModel pm, JsonElement oneOf, string parentClassName)
    {
        var items = oneOf.EnumerateArray().ToList();

        var hasString = false;
        var hasArray = false;
        var hasObject = false;
        var hasInteger = false;
        var hasBoolean = false;
        var hasNull = false;
        var hasRef = false;
        string? refName = null;
        JsonElement? objectElement = null;

        foreach (var item in items)
        {
            if (item.TryGetProperty("$ref", out var r))
            {
                hasRef = true;
                refName = ExtractRef(r.GetString()!);
            }
            if (item.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
            {
                var t = typeProp.GetString();
                if (t == "string") hasString = true;
                else if (t == "array") hasArray = true;
                else if (t == "object") { hasObject = true; objectElement = item; }
                else if (t == "integer") hasInteger = true;
                else if (t == "boolean") hasBoolean = true;
                else if (t == "null") hasNull = true;
            }
        }

        // oneOf[string, object with properties] -> StringOrObject with inline class
        if (hasString && hasObject && objectElement.HasValue &&
            objectElement.Value.TryGetProperty("properties", out var objProps))
        {
            var className = parentClassName + pm.CSharpName + "Config";
            pm.Type = PropertyType.StringOrObject;
            pm.OneOfObjectClassName = className;
            pm.OneOfObjectProperties = ParseProperties(objProps, className);
            return;
        }

        // oneOf[string, integer]
        if (hasString && hasInteger) { pm.Type = PropertyType.StringOrInteger; return; }

        // oneOf[string, boolean]
        if (hasString && hasBoolean) { pm.Type = PropertyType.StringOrBoolean; return; }

        // oneOf[string, array]
        if (hasString && hasArray) { pm.Type = PropertyType.StringOrList; return; }

        // oneOf[null, $ref] -> nullable ref
        if (hasNull && hasRef && refName is not null)
        {
            pm.Ref = refName;
            pm.Type = MapRefToType(refName);
            return;
        }

        // oneOf with only refs -> use first ref (applying MapRefToType)
        if (hasRef && refName is not null)
        {
            var mappedType = MapRefToType(refName);
            pm.Ref = mappedType == PropertyType.Ref ? refName : null;
            pm.Type = mappedType;
            return;
        }

        // oneOf[null, object] or oneOf[null, string] -> nullable
        if (hasNull && items.Count == 2)
        {
            var nonNull = items.First(i =>
                !(i.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "null"));
            if (nonNull.TryGetProperty("$ref", out var r2))
            {
                pm.Ref = ExtractRef(r2.GetString()!);
                pm.Type = PropertyType.Ref;
            }
            return;
        }

        // Fallback
        pm.Type = PropertyType.String;
    }

    private static string ExtractRef(string refPath)
    {
        const string prefix1 = "#/definitions/";
        const string prefix2 = "#/$defs/";
        if (refPath.StartsWith(prefix1))
            return refPath.Substring(prefix1.Length);
        if (refPath.StartsWith(prefix2))
            return refPath.Substring(prefix2.Length);
        return refPath;
    }

    private static PropertyType MapRefToType(string refName)
    {
        return refName switch
        {
            "string_or_list" or "stringOrList" => PropertyType.StringOrList,
            "string_file_list" => PropertyType.StringOrList,
            "script" or "optional_script" or "before_script" or "after_script" => PropertyType.StringOrList,
            "tags" or "filter_refs" => PropertyType.Array,
            "image" => PropertyType.String,
            "services" or "rules" or "includeRules" or "steps" => PropertyType.Array,
            "identity" or "when" or "workflowName" or "if" or "timeout" or "start_in" => PropertyType.String,
            "interruptible" => PropertyType.Boolean,
            "retry_max" => PropertyType.Integer,
            "retry" or "retry_errors" => PropertyType.Object,
            "configInputs" or "jobInputs" or "inputs" => PropertyType.Object,
            "globalVariables" or "jobVariables" or "rulesVariables" => PropertyType.Object,
            "id_tokens" or "secrets" => PropertyType.Object,
            "allow_failure" => PropertyType.Boolean,
            "cache" => PropertyType.Array,
            "filter" => PropertyType.Object,
            "parallel" or "parallel_matrix" => PropertyType.Object,
            "include_item" => PropertyType.StringOrObject,
            "rulesNeeds" => PropertyType.Array,
            "changes" or "exists" => PropertyType.StringOrList,
            "stepName" or "stepNamedStrings" or "stepNamedValues" => PropertyType.String,
            "step" or "stepGitReference" or "stepOciReference" or "stepFuncReference" => PropertyType.Object,
            "!reference" => PropertyType.Array,
            _ => PropertyType.Ref
        };
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

    private static PropertyType MapMultiType(List<string> types)
    {
        var nonNull = types.Where(t => t != "null").ToList();
        if (nonNull.Count == 1)
            return MapSingleType(nonNull[0]);

        if (nonNull.Contains("boolean") && nonNull.Contains("string"))
            return PropertyType.StringOrBoolean;
        if (nonNull.Contains("integer") && nonNull.Contains("string"))
            return PropertyType.StringOrInteger;
        if (nonNull.Contains("number") && nonNull.Contains("string"))
            return PropertyType.StringOrInteger;

        return PropertyType.String;
    }
}
