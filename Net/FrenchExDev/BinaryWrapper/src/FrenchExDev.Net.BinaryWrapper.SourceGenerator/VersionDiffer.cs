using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenchExDev.Net.BinaryWrapper.SourceGenerator;

/// <summary>
/// Computes a unified command tree from multiple versioned trees,
/// annotating each command and option with sinceVersion/untilVersion.
/// </summary>
internal static class VersionDiffer
{
    /// <summary>
    /// Merges multiple versioned command trees into a single unified tree.
    /// </summary>
    public static UnifiedCommandTree Merge(IReadOnlyList<(string Version, CommandTreeModel Tree)> versionedTrees)
    {
        if (versionedTrees.Count == 0)
            return new UnifiedCommandTree { BinaryName = "", Commands = new List<UnifiedCommand>() };

        var sorted = versionedTrees.OrderBy(v => v.Version, StringComparer.OrdinalIgnoreCase).ToList();
        var versions = sorted.Select(v => v.Version).ToList();
        var binaryName = sorted[0].Tree.BinaryName;

        // Collect all leaf commands across all versions
        var commandVersions = new Dictionary<string, List<(string Version, CommandNodeModel Node, List<string> PathSegments)>>();

        foreach (var item in sorted)
        {
            CollectLeafCommands(item.Tree.Root, new List<string>(), item.Version, commandVersions);
        }

        // Build unified commands
        var unifiedCommands = new List<UnifiedCommand>();
        foreach (var kvp in commandVersions)
        {
            var commandPath = kvp.Key;
            var entries = kvp.Value;
            var entryVersions = entries.Select(e => e.Version).ToList();
            var sinceVersion = entryVersions.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).First();
            var lastVersion = entryVersions.OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase).First();
            var untilVersion = string.Equals(lastVersion, versions[versions.Count - 1], StringComparison.OrdinalIgnoreCase)
                ? null : NextVersion(lastVersion, versions);

            // Merge options across all versions of this command
            var unifiedOptions = MergeOptions(entries, versions);
            var unifiedArguments = MergeArguments(entries);

            // Use the latest version's node for description
            var latestNode = entries[entries.Count - 1].Node;

            unifiedCommands.Add(new UnifiedCommand
            {
                CommandPath = commandPath,
                PathSegments = entries[0].PathSegments,
                Name = latestNode.Name,
                Description = latestNode.Description,
                SinceVersion = sinceVersion,
                UntilVersion = untilVersion,
                Options = unifiedOptions,
                Arguments = unifiedArguments
            });
        }

        return new UnifiedCommandTree
        {
            BinaryName = binaryName,
            Commands = unifiedCommands
        };
    }

    /// <summary>
    /// When only one version exists, creates a simple unified tree with no version annotations.
    /// </summary>
    public static UnifiedCommandTree FromSingle(string version, CommandTreeModel tree)
    {
        var commandVersions = new Dictionary<string, List<(string Version, CommandNodeModel Node, List<string> PathSegments)>>();
        CollectLeafCommands(tree.Root, new List<string>(), version, commandVersions);

        var commands = new List<UnifiedCommand>();
        foreach (var kvp in commandVersions)
        {
            var entries = kvp.Value;
            var node = entries[0].Node;
            commands.Add(new UnifiedCommand
            {
                CommandPath = kvp.Key,
                PathSegments = entries[0].PathSegments,
                Name = node.Name,
                Description = node.Description,
                SinceVersion = null,
                UntilVersion = null,
                Options = node.Options.Select(o => new UnifiedOption
                {
                    LongName = o.LongName,
                    ShortName = o.ShortName,
                    Description = o.Description,
                    ValueKind = o.ValueKind,
                    ClrType = o.ClrType,
                    DefaultValue = o.DefaultValue,
                    IsRequired = o.IsRequired,
                    SinceVersion = null,
                    UntilVersion = null
                }).ToList(),
                Arguments = node.Arguments.Select(a => new UnifiedArgument
                {
                    Name = a.Name,
                    Position = a.Position,
                    Description = a.Description,
                    ClrType = a.ClrType,
                    IsRequired = a.IsRequired,
                    IsVariadic = a.IsVariadic,
                    DefaultValue = a.DefaultValue
                }).ToList()
            });
        }

        return new UnifiedCommandTree { BinaryName = tree.BinaryName, Commands = commands };
    }

    private static void CollectLeafCommands(
        CommandNodeModel node, List<string> parentPath, string version,
        Dictionary<string, List<(string, CommandNodeModel, List<string>)>> result)
    {
        var currentPath = new List<string>(parentPath) { node.Name };

        if (node.SubCommands.Count == 0)
        {
            var pathKey = string.Join(".", currentPath);
            if (!result.TryGetValue(pathKey, out var list))
            {
                list = new List<(string, CommandNodeModel, List<string>)>();
                result[pathKey] = list;
            }
            list.Add((version, node, currentPath));
            return;
        }

        foreach (var sub in node.SubCommands)
            CollectLeafCommands(sub, currentPath, version, result);
    }

    private static List<UnifiedOption> MergeOptions(
        List<(string Version, CommandNodeModel Node, List<string> PathSegments)> entries,
        List<string> allVersions)
    {
        var optionVersions = new Dictionary<string, List<(string Version, OptionModel Option)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            foreach (var opt in entry.Node.Options)
            {
                if (!optionVersions.TryGetValue(opt.LongName, out var list))
                {
                    list = new List<(string, OptionModel)>();
                    optionVersions[opt.LongName] = list;
                }
                list.Add((entry.Version, opt));
            }
        }

        var result = new List<UnifiedOption>();
        foreach (var kvp2 in optionVersions)
        {
            var optEntries = kvp2.Value;
            var versions = optEntries.Select(e => e.Version).ToList();
            var since = versions.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).First();
            var last = versions.OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase).First();
            var until = string.Equals(last, allVersions[allVersions.Count - 1], StringComparison.OrdinalIgnoreCase)
                ? null : NextVersion(last, allVersions);

            // If option exists in all entry versions, no need for since/until
            var entryVersions = entries.Select(e => e.Version).ToList();
            var existsInAll = entryVersions.All(v => versions.Contains(v));

            var latest = optEntries[optEntries.Count - 1].Option;
            result.Add(new UnifiedOption
            {
                LongName = latest.LongName,
                ShortName = latest.ShortName,
                Description = latest.Description,
                ValueKind = latest.ValueKind,
                ClrType = latest.ClrType,
                DefaultValue = latest.DefaultValue,
                IsRequired = latest.IsRequired,
                SinceVersion = existsInAll ? null : since,
                UntilVersion = existsInAll ? null : until
            });
        }

        return result;
    }

    private static List<UnifiedArgument> MergeArguments(
        List<(string Version, CommandNodeModel Node, List<string> PathSegments)> entries)
    {
        // Use latest version's arguments (arguments rarely change between versions)
        var latest = entries[entries.Count - 1].Node;
        return latest.Arguments.Select(a => new UnifiedArgument
        {
            Name = a.Name,
            Position = a.Position,
            Description = a.Description,
            ClrType = a.ClrType,
            IsRequired = a.IsRequired,
            IsVariadic = a.IsVariadic,
            DefaultValue = a.DefaultValue
        }).ToList();
    }

    private static string? NextVersion(string version, List<string> allVersions)
    {
        var idx = allVersions.IndexOf(version);
        return idx >= 0 && idx + 1 < allVersions.Count ? allVersions[idx + 1] : null;
    }
}

// ── Unified Models ──────────────────────────────────────────────────────────

internal sealed class UnifiedCommandTree
{
    public string BinaryName { get; set; } = "";
    public List<UnifiedCommand> Commands { get; set; } = new();
}

internal sealed class UnifiedCommand
{
    public string CommandPath { get; set; } = "";
    public List<string> PathSegments { get; set; } = new();
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }
    public List<UnifiedOption> Options { get; set; } = new();
    public List<UnifiedArgument> Arguments { get; set; } = new();
}

internal sealed class UnifiedOption
{
    public string LongName { get; set; } = "";
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public string ValueKind { get; set; } = "single";
    public string ClrType { get; set; } = "string";
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }
}

internal sealed class UnifiedArgument
{
    public string Name { get; set; } = "";
    public int Position { get; set; }
    public string? Description { get; set; }
    public string ClrType { get; set; } = "string";
    public bool IsRequired { get; set; } = true;
    public bool IsVariadic { get; set; }
    public string? DefaultValue { get; set; }
}
