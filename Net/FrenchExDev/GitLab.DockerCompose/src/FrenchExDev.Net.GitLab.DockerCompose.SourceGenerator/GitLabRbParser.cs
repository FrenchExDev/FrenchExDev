using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator;

/// <summary>
/// Parses a gitlab.rb.template file into a <see cref="GitLabRbModel"/>.
/// Handles ### section headers, ##! doc comments, # prefix['key'] = value settings,
/// multi-line hash values, YAML blocks, and standalone URL assignments.
/// </summary>
internal static class GitLabRbParser
{
    // # prefix['key'] = value
    // # prefix['k1']['k2'] = value
    private static readonly Regex SettingRegex = new Regex(
        @"^#\s+([a-z_]+)(\['.+?'\](?:\['.+?'\])*)\s*=\s*(.+)$",
        RegexOptions.Compiled);

    // Extract bracket keys: ['key1']['key2'] → ["key1", "key2"]
    private static readonly Regex BracketKeyRegex = new Regex(
        @"\['([^']+)'\]",
        RegexOptions.Compiled);

    // # standalone_func 'value' or # standalone_func "value"
    private static readonly Regex StandaloneRegex = new Regex(
        @"^#?\s*(external_url|registry_external_url|pages_external_url|mattermost_external_url|gitlab_kas_external_url|runtime_dir)\s+['""](.+?)['""]",
        RegexOptions.Compiled);

    // # roles ['...', '...']
    private static readonly Regex RolesRegex = new Regex(
        @"^#\s*roles\s+\[",
        RegexOptions.Compiled);

    // YAML.load <<-'EOS'
    private static readonly Regex YamlBlockStartRegex = new Regex(
        @"^#\s+([a-z_]+)\['([^']+)'\]\s*=\s*YAML\.load\s+<<-'EOS'",
        RegexOptions.Compiled);

    public static GitLabRbModel Parse(string content, string version)
    {
        var model = new GitLabRbModel { Version = version };
        var prefixGroups = new Dictionary<string, GitLabRbPrefixGroup>();
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

        string? pendingDoc = null;
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i].TrimEnd();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Banner lines (####... or ##! or ## Title)
            if (line.StartsWith("##"))
            {
                // Doc comment: ##! or ###!
                if (line.StartsWith("##!") || line.StartsWith("###!"))
                {
                    var docText = line.StartsWith("###!")
                        ? line.Substring(4).Trim()
                        : line.Substring(3).Trim();
                    pendingDoc = pendingDoc == null ? docText : pendingDoc + " " + docText;
                }
                // else: section header, banner — skip (we group by prefix, not by section)
                i++;
                continue;
            }

            // Standalone URLs
            var standaloneMatch = StandaloneRegex.Match(line);
            if (standaloneMatch.Success)
            {
                model.StandaloneUrls.Add(new GitLabRbStandaloneUrl
                {
                    RubyKey = standaloneMatch.Groups[1].Value,
                    ExampleValue = standaloneMatch.Groups[2].Value,
                    DocComment = pendingDoc,
                });
                pendingDoc = null;
                i++;
                continue;
            }

            // Roles (standalone list)
            if (RolesRegex.IsMatch(line))
            {
                model.StandaloneUrls.Add(new GitLabRbStandaloneUrl
                {
                    RubyKey = "roles",
                    ExampleValue = ExtractListValue(line),
                    DocComment = pendingDoc,
                });
                pendingDoc = null;
                i++;
                continue;
            }

            // YAML block: # prefix['key'] = YAML.load <<-'EOS' ... EOS
            var yamlMatch = YamlBlockStartRegex.Match(line);
            if (yamlMatch.Success)
            {
                var prefix = yamlMatch.Groups[1].Value;
                var key = yamlMatch.Groups[2].Value;
                var yamlContent = CollectYamlBlock(lines, ref i);
                var group = GetOrCreateGroup(prefixGroups, prefix);
                var node = GetOrCreateChild(group.Root, key);
                // YAML blocks become string (opaque) — too complex for hierarchy
                node.LeafType = GitLabRbValueType.String;
                node.ExampleValue = "YAML";
                node.DocComment = pendingDoc;
                pendingDoc = null;
                continue;
            }

            // Setting: # prefix['key'] = value
            var settingMatch = SettingRegex.Match(line);
            if (settingMatch.Success)
            {
                var prefix = settingMatch.Groups[1].Value;
                var bracketsRaw = settingMatch.Groups[2].Value;
                var valueRaw = settingMatch.Groups[3].Value;

                // Strip inline comments: "true # some comment" → "true"
                valueRaw = StripInlineComment(valueRaw);

                var keys = ExtractBracketKeys(bracketsRaw);

                var group = GetOrCreateGroup(prefixGroups, prefix);

                // Check if value starts a multi-line hash
                if (IsHashStart(valueRaw))
                {
                    var hashContent = CollectMultiLineHash(lines, ref i, valueRaw);
                    var parentNode = NavigateToParent(group.Root, keys);
                    var leafKey = keys[keys.Count - 1];
                    var childNode = GetOrCreateChild(parentNode, leafKey);
                    childNode.DocComment = pendingDoc;
                    ParseHashIntoNode(childNode, hashContent);
                    pendingDoc = null;
                    continue;
                }

                // Simple scalar value
                var targetNode = NavigateToParent(group.Root, keys);
                var finalKey = keys[keys.Count - 1];
                var leaf = GetOrCreateChild(targetNode, finalKey);
                leaf.LeafType = InferValueType(valueRaw);
                leaf.ExampleValue = valueRaw.Trim();
                leaf.DocComment = pendingDoc;
                pendingDoc = null;
                i++;
                continue;
            }

            // Unrecognized line — skip
            pendingDoc = null;
            i++;
        }

        model.PrefixGroups = prefixGroups.Values.ToList();
        return model;
    }

    public static string ExtractVersion(string filename)
    {
        // "gitlab-18.10.1.rb" → "18.10.1"
        var name = System.IO.Path.GetFileNameWithoutExtension(filename);
        if (name.StartsWith("gitlab-"))
            return name.Substring("gitlab-".Length);
        return name;
    }

    private static List<string> ExtractBracketKeys(string brackets)
    {
        var keys = new List<string>();
        foreach (Match m in BracketKeyRegex.Matches(brackets))
            keys.Add(m.Groups[1].Value);
        return keys;
    }

    private static GitLabRbPrefixGroup GetOrCreateGroup(
        Dictionary<string, GitLabRbPrefixGroup> groups, string prefix)
    {
        if (!groups.TryGetValue(prefix, out var group))
        {
            group = new GitLabRbPrefixGroup { Prefix = prefix, Root = new GitLabRbObjectNode { Name = prefix } };
            groups[prefix] = group;
        }
        return group;
    }

    private static GitLabRbObjectNode GetOrCreateChild(GitLabRbObjectNode parent, string key)
    {
        if (!parent.Children.TryGetValue(key, out var child))
        {
            child = new GitLabRbObjectNode { Name = key };
            parent.Children[key] = child;
        }
        return child;
    }

    /// <summary>Navigate through bracket keys, creating intermediate branch nodes.
    /// Returns the parent of the last key.</summary>
    private static GitLabRbObjectNode NavigateToParent(GitLabRbObjectNode root, List<string> keys)
    {
        var current = root;
        for (int j = 0; j < keys.Count - 1; j++)
            current = GetOrCreateChild(current, keys[j]);
        return current;
    }

    private static string StripInlineComment(string value)
    {
        // "true # comment" → "true"
        // Be careful not to strip inside strings: "'value # with hash'" should stay
        var trimmed = value.TrimStart();
        if (trimmed.StartsWith("'") || trimmed.StartsWith("\"") || trimmed.StartsWith("{") ||
            trimmed.StartsWith("[") || trimmed.StartsWith("YAML"))
            return value;

        var hashIdx = value.IndexOf('#');
        if (hashIdx > 0)
            return value.Substring(0, hashIdx).TrimEnd();
        return value;
    }

    private static bool IsHashStart(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.StartsWith("{") && !trimmed.TrimEnd().EndsWith("}");
    }

    private static string CollectMultiLineHash(string[] lines, ref int i, string firstLineValue)
    {
        // Collect lines until braces balance
        var content = firstLineValue;
        int depth = CountChar(content, '{') - CountChar(content, '}');
        i++;

        while (i < lines.Length && depth > 0)
        {
            var raw = lines[i].TrimEnd();
            // Strip leading "# " from continuation lines
            if (raw.StartsWith("#"))
            {
                raw = raw.Substring(1);
                // Skip pure comment lines inside hashes (##! or # # or lines without => or : key patterns)
                var stripped = raw.TrimStart();
                if (stripped.StartsWith("#") || stripped.StartsWith("!"))
                {
                    i++;
                    continue;
                }
                raw = stripped;
            }

            content += "\n" + raw;
            depth += CountChar(raw, '{') - CountChar(raw, '}');
            i++;
        }

        return content;
    }

    private static string CollectYamlBlock(string[] lines, ref int i)
    {
        var content = "";
        i++; // skip the YAML.load line

        while (i < lines.Length)
        {
            var raw = lines[i].TrimEnd();
            if (raw.TrimStart('#').Trim() == "EOS")
            {
                i++;
                break;
            }
            if (raw.StartsWith("#"))
                raw = raw.Substring(1);
            content += raw + "\n";
            i++;
        }

        return content;
    }

    private static string ExtractListValue(string line)
    {
        var idx = line.IndexOf('[');
        if (idx < 0) return "[]";
        var end = line.LastIndexOf(']');
        if (end < idx) return "[]";
        return line.Substring(idx, end - idx + 1);
    }

    private static int CountChar(string s, char c)
    {
        int count = 0;
        for (int j = 0; j < s.Length; j++)
            if (s[j] == c) count++;
        return count;
    }

    /// <summary>Parse a Ruby hash string into child nodes of the given node.</summary>
    private static void ParseHashIntoNode(GitLabRbObjectNode node, string hashContent)
    {
        // Trim outer braces
        var trimmed = hashContent.Trim();
        if (trimmed.StartsWith("{")) trimmed = trimmed.Substring(1);
        if (trimmed.EndsWith("}")) trimmed = trimmed.Substring(0, trimmed.Length - 1);

        // Detect symbol-key hash (key: value) vs string-key hash ('key' => value)
        var isSymbolKey = trimmed.Contains(":") && !trimmed.Contains("=>");

        if (isSymbolKey)
            ParseSymbolKeyHash(node, trimmed);
        else
            ParseStringKeyHash(node, trimmed);
    }

    /// <summary>Parse symbol-key hash: { listen_addr: 'value', auth: { token: '...' } }</summary>
    private static void ParseSymbolKeyHash(GitLabRbObjectNode node, string content)
    {
        // Split by top-level commas (respecting nested braces/brackets)
        var entries = SplitTopLevel(content, ',');

        foreach (var entry in entries)
        {
            var trimmed = entry.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // key: value
            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx <= 0) continue;

            var key = trimmed.Substring(0, colonIdx).Trim().Trim('\'', '"');
            var value = trimmed.Substring(colonIdx + 1).Trim();

            // Remove trailing comma
            if (value.EndsWith(",")) value = value.Substring(0, value.Length - 1).Trim();

            // Skip empty keys or comment-like keys
            if (string.IsNullOrEmpty(key) || key.StartsWith("#")) continue;

            var child = GetOrCreateChild(node, key);

            if (value.StartsWith("{"))
            {
                // Nested hash → recurse
                ParseHashIntoNode(child, value);
            }
            else if (value.StartsWith("["))
            {
                // Array — check if array of objects
                if (value.Contains("{"))
                {
                    child.IsArrayOfObjects = true;
                    ParseArrayOfObjectsIntoNode(child, value);
                }
                else
                {
                    child.LeafType = InferValueType(value);
                    child.ExampleValue = value;
                }
            }
            else
            {
                child.LeafType = InferValueType(value);
                child.ExampleValue = value;
            }
        }
    }

    /// <summary>Parse string-key hash: { 'key' => 'value', 'k2' => 'v2' }</summary>
    private static void ParseStringKeyHash(GitLabRbObjectNode node, string content)
    {
        // Check if this is a simple flat dict
        var entries = SplitTopLevel(content, ',');
        bool allSimple = true;

        foreach (var entry in entries)
        {
            var t = entry.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            if (t.Contains("{") || t.Contains("["))
            {
                allSimple = false;
                break;
            }
        }

        if (allSimple && entries.Count > 0)
        {
            // Flat string dict — treat as leaf
            node.LeafType = GitLabRbValueType.StringDict;
            node.ExampleValue = "{" + content + "}";
            return;
        }

        // Complex nested — parse entries
        foreach (var entry in entries)
        {
            var t = entry.Trim();
            if (string.IsNullOrEmpty(t)) continue;

            // 'key' => value  or  "key" => value
            var arrowIdx = t.IndexOf("=>");
            if (arrowIdx <= 0) continue;

            var key = t.Substring(0, arrowIdx).Trim().Trim('\'', '"');
            var value = t.Substring(arrowIdx + 2).Trim();

            if (value.EndsWith(",")) value = value.Substring(0, value.Length - 1).Trim();

            var child = GetOrCreateChild(node, key);

            if (value.StartsWith("{"))
            {
                ParseHashIntoNode(child, value);
            }
            else
            {
                child.LeafType = InferValueType(value);
                child.ExampleValue = value;
            }
        }
    }

    /// <summary>Parse [{name: 'a', path: 'b'}, {name: 'c', path: 'd'}] into node children
    /// representing the shape of the items.</summary>
    private static void ParseArrayOfObjectsIntoNode(GitLabRbObjectNode node, string arrayContent)
    {
        // Extract first object to determine shape
        var trimmed = arrayContent.Trim();
        if (trimmed.StartsWith("[")) trimmed = trimmed.Substring(1);
        if (trimmed.EndsWith("]")) trimmed = trimmed.Substring(0, trimmed.Length - 1);

        var firstBrace = trimmed.IndexOf('{');
        if (firstBrace < 0) return;

        int depth = 0;
        int end = firstBrace;
        for (int j = firstBrace; j < trimmed.Length; j++)
        {
            if (trimmed[j] == '{') depth++;
            else if (trimmed[j] == '}') { depth--; if (depth == 0) { end = j; break; } }
        }

        var firstObject = trimmed.Substring(firstBrace, end - firstBrace + 1);
        ParseHashIntoNode(node, firstObject);
    }

    /// <summary>Split string by delimiter at top level (not inside {} or [] or '' or "").</summary>
    private static List<string> SplitTopLevel(string content, char delimiter)
    {
        var parts = new List<string>();
        int depth = 0;
        bool inSingle = false;
        bool inDouble = false;
        int start = 0;

        for (int j = 0; j < content.Length; j++)
        {
            var c = content[j];
            if (!inSingle && !inDouble)
            {
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                else if (c == '\'') inSingle = true;
                else if (c == '"') inDouble = true;
                else if (c == delimiter && depth == 0)
                {
                    parts.Add(content.Substring(start, j - start));
                    start = j + 1;
                }
            }
            else if (inSingle && c == '\'') inSingle = false;
            else if (inDouble && c == '"') inDouble = false;
        }

        if (start < content.Length)
            parts.Add(content.Substring(start));

        return parts;
    }

    internal static GitLabRbValueType InferValueType(string value)
    {
        var v = value.Trim();

        if (v == "true" || v == "false") return GitLabRbValueType.Boolean;
        if (v == "nil") return GitLabRbValueType.Nil;
        if (v == "{}") return GitLabRbValueType.StringDict;
        if (v == "[]") return GitLabRbValueType.StringList;

        // Quoted string
        if ((v.StartsWith("'") && v.EndsWith("'")) ||
            (v.StartsWith("\"") && v.EndsWith("\"")))
            return GitLabRbValueType.String;

        // Array
        if (v.StartsWith("["))
        {
            if (v.Contains("0.") || v.Contains("1."))
                return GitLabRbValueType.FloatList;
            return GitLabRbValueType.StringList;
        }

        // Hash
        if (v.StartsWith("{")) return GitLabRbValueType.StringDict;

        // Number
        if (long.TryParse(v, out var longVal))
        {
            if (longVal > int.MaxValue || longVal < int.MinValue)
                return GitLabRbValueType.Long;
            return GitLabRbValueType.Integer;
        }

        if (double.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return GitLabRbValueType.Float;

        // Fallback
        return GitLabRbValueType.String;
    }
}
