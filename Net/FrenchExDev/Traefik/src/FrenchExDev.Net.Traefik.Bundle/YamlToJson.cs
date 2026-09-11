using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace FrenchExDev.Net.Traefik.Bundle;

/// <summary>
/// Converts a YAML document tree (YamlDotNet's <see cref="YamlStream"/>
/// representation model) to a <see cref="JsonNode"/> tree, preserving
/// primitive types per the YAML 1.2 core schema. Used by
/// <see cref="TraefikSerializer"/> to feed the original YAML — including
/// keys the typed deserializer would silently drop — to JsonSchema.Net.
/// </summary>
internal static class YamlToJson
{
    // YAML 1.2 core schema scalar resolution: a plain (untagged) scalar
    // matching one of these patterns is a primitive, otherwise it is a string.
    private static readonly Regex BoolPattern = new(
        @"^(true|True|TRUE|false|False|FALSE)$",
        RegexOptions.Compiled);

    private static readonly Regex IntPattern = new(
        @"^[-+]?(0|[1-9][0-9]*)$",
        RegexOptions.Compiled);

    private static readonly Regex HexPattern = new(
        @"^0x[0-9a-fA-F]+$",
        RegexOptions.Compiled);

    private static readonly Regex OctPattern = new(
        @"^0o[0-7]+$",
        RegexOptions.Compiled);

    private static readonly Regex FloatPattern = new(
        @"^[-+]?([0-9]+\.[0-9]*|\.[0-9]+|[0-9]+)([eE][-+]?[0-9]+)?$",
        RegexOptions.Compiled);

    private static readonly Regex SpecialFloatPattern = new(
        @"^[-+]?(\.inf|\.Inf|\.INF|\.nan|\.NaN|\.NAN)$",
        RegexOptions.Compiled);

    private static readonly Regex NullPattern = new(
        @"^(~|null|Null|NULL|)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses a YAML document and returns its first document as a
    /// <see cref="JsonNode"/>. Throws if the YAML is malformed.
    /// </summary>
    public static JsonNode? Parse(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0) return null;
        return Convert(stream.Documents[0].RootNode);
    }

    private static JsonNode? Convert(YamlNode node)
    {
        return node switch
        {
            YamlMappingNode m => ConvertMapping(m),
            YamlSequenceNode s => ConvertSequence(s),
            YamlScalarNode sc => ConvertScalar(sc),
            _ => null,
        };
    }

    private static JsonObject ConvertMapping(YamlMappingNode mapping)
    {
        var obj = new JsonObject();
        foreach (var entry in mapping.Children)
        {
            var key = entry.Key is YamlScalarNode keyScalar
                ? keyScalar.Value ?? ""
                : entry.Key.ToString() ?? "";
            obj[key] = Convert(entry.Value);
        }
        return obj;
    }

    private static JsonArray ConvertSequence(YamlSequenceNode sequence)
    {
        var arr = new JsonArray();
        foreach (var child in sequence.Children)
        {
            arr.Add(Convert(child));
        }
        return arr;
    }

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        if (value is null) return null;

        // Quoted scalars are always strings, regardless of content. Plain
        // scalars participate in core-schema type resolution.
        var isPlain = scalar.Style == YamlDotNet.Core.ScalarStyle.Plain;

        // Honour explicit YAML tags first (e.g. !!int, !!str). The
        // TagName struct throws on .Value for non-specific tags ("?" / "!"),
        // so guard with IsEmpty / IsNonSpecific.
        if (!scalar.Tag.IsEmpty && !scalar.Tag.IsNonSpecific)
        {
            var tag = scalar.Tag.Value;
            if (tag == "tag:yaml.org,2002:str") return JsonValue.Create(value);
            if (tag == "tag:yaml.org,2002:bool") return ParseBool(value) ?? JsonValue.Create(value);
            if (tag == "tag:yaml.org,2002:int") return ParseInt(value) ?? JsonValue.Create(value);
            if (tag == "tag:yaml.org,2002:float") return ParseFloat(value) ?? JsonValue.Create(value);
            if (tag == "tag:yaml.org,2002:null") return null;
        }

        if (!isPlain)
        {
            return JsonValue.Create(value);
        }

        if (NullPattern.IsMatch(value)) return null;
        if (BoolPattern.IsMatch(value))
        {
            return JsonValue.Create(value.Equals("true", System.StringComparison.OrdinalIgnoreCase));
        }
        var intVal = ParseInt(value);
        if (intVal is not null) return intVal;
        var floatVal = ParseFloat(value);
        if (floatVal is not null) return floatVal;

        return JsonValue.Create(value);
    }

    private static JsonNode? ParseBool(string value)
    {
        if (value.Equals("true", System.StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create(true);
        if (value.Equals("false", System.StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create(false);
        return null;
    }

    private static JsonNode? ParseInt(string value)
    {
        if (IntPattern.IsMatch(value) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return JsonValue.Create(l);
        }
        if (HexPattern.IsMatch(value) && long.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
        {
            return JsonValue.Create(hex);
        }
        if (OctPattern.IsMatch(value))
        {
            try
            {
                return JsonValue.Create(System.Convert.ToInt64(value.Substring(2), 8));
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private static JsonNode? ParseFloat(string value)
    {
        if (SpecialFloatPattern.IsMatch(value))
        {
            // JSON has no NaN/Infinity literals; preserve as a string so the
            // schema can still see something rather than dropping the value.
            return JsonValue.Create(value);
        }
        if (FloatPattern.IsMatch(value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return JsonValue.Create(d);
        }
        return null;
    }
}
