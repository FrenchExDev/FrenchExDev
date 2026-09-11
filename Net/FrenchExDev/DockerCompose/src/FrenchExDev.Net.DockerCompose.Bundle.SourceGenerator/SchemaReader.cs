using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FrenchExDev.Net.DockerCompose.Bundle.SourceGenerator;

internal static class SchemaReader
{
    public static SchemaModel Parse(string json, string version)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var model = new SchemaModel { Version = version };

        if (root.TryGetProperty("properties", out var rootProps))
            model.RootProperties = ParseProperties(rootProps, "");

        if (root.TryGetProperty("definitions", out var defs))
        {
            foreach (var def in defs.EnumerateObject())
            {
                var defModel = ParseDefinition(def.Name, def.Value);
                if (defModel is not null)
                    model.Definitions[def.Name] = defModel;
            }
        }

        return model;
    }

    public static string ExtractVersion(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        var prefix = "compose-spec-v";
        if (name.StartsWith(prefix))
            return name.Substring(prefix.Length);
        return name;
    }

    private static DefinitionModel ParseDefinition(string name, JsonElement element)
    {
        var def = new DefinitionModel { Name = name };

        if (element.TryGetProperty("description", out var desc))
            def.Description = desc.GetString();

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
            CSharpName = NamingHelper.ToPascalCase(name)
        };

        if (element.TryGetProperty("description", out var desc))
            pm.Description = desc.GetString();

        if (element.TryGetProperty("deprecated", out var dep) && dep.GetBoolean())
            pm.IsDeprecated = true;

        // $ref
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

                // Inline object with properties → generate a class
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
                            // Array of inline objects
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
                        // Array of oneOf items (e.g., ports, volumes, devices)
                        pm.Items = new PropertyModel { Type = PropertyType.String };
                        ParseOneOf(pm.Items, itemOneOf, parentClassName + pm.CSharpName);
                    }
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

        // oneOf[string, object with properties] → StringOrObject with inline class
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

        // oneOf[$ref(list_of_strings), object] → list-or-map union
        if (hasRef && hasObject && refName == "list_of_strings" && objectElement.HasValue)
        {
            // Extract the object's patternProperties value type
            if (objectElement.Value.TryGetProperty("patternProperties", out var pp))
            {
                foreach (var ppEntry in pp.EnumerateObject())
                {
                    if (ppEntry.Value.TryGetProperty("properties", out var condProps))
                    {
                        var condClassName = parentClassName + pm.CSharpName + "Condition";
                        pm.Type = PropertyType.StringOrObject;
                        pm.OneOfObjectClassName = condClassName;
                        pm.OneOfObjectProperties = ParseProperties(condProps, condClassName);
                        return;
                    }
                    if (ppEntry.Value.TryGetProperty("oneOf", out _) || ppEntry.Value.TryGetProperty("type", out _))
                    {
                        // Simple map value (e.g., networks with nested config)
                        pm.Type = PropertyType.StringOrObject;
                        pm.OneOfObjectClassName = null;
                        return;
                    }
                }
            }
            pm.Type = PropertyType.StringOrList;
            return;
        }

        // oneOf[null, $ref] → nullable ref
        if (hasNull && hasRef && refName is not null)
        {
            pm.Ref = refName;
            pm.Type = MapRefToType(refName);
            return;
        }

        // oneOf[null, object] or oneOf[null, string] → nullable
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
        const string prefix = "#/definitions/";
        return refPath.StartsWith(prefix) ? refPath.Substring(prefix.Length) : refPath;
    }

    private static PropertyType MapRefToType(string refName)
    {
        return refName switch
        {
            "list_or_dict" => PropertyType.ListOrDict,
            "string_or_list" => PropertyType.StringOrList,
            "command" => PropertyType.Command,
            "service_config_or_secret" => PropertyType.ServiceConfigOrSecret,
            "extra_hosts" => PropertyType.ExtraHosts,
            "ulimits" => PropertyType.Ulimits,
            "list_of_strings" => PropertyType.StringOrList,
            "env_file" => PropertyType.StringOrList,
            "label_file" => PropertyType.StringOrList,
            "gpus" => PropertyType.StringOrList,
            "include" => PropertyType.Array,
            "generic_resources" => PropertyType.Array,
            "devices" => PropertyType.Array,
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
