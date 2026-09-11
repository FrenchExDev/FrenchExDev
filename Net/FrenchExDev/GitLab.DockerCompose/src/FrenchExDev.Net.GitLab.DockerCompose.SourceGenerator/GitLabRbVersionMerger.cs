using System.Collections.Generic;
using System.Linq;

namespace FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator;

/// <summary>
/// Merges multiple versioned <see cref="GitLabRbModel"/> instances into a unified model
/// with <c>SinceVersion</c> / <c>UntilVersion</c> metadata on every node.
/// Same algorithm as <c>SchemaVersionMerger</c> in DockerCompose.Bundle.SourceGenerator.
/// </summary>
internal static class GitLabRbVersionMerger
{
    public static UnifiedGitLabRbModel Merge(List<GitLabRbModel> models)
    {
        if (models.Count == 0)
            return new UnifiedGitLabRbModel();

        models.Sort((a, b) => NamingHelper.CompareVersions(a.Version, b.Version));

        var unified = new UnifiedGitLabRbModel
        {
            Versions = models.Select(m => m.Version).ToList()
        };

        var firstVersion = models[0].Version;
        var lastVersion = models[models.Count - 1].Version;

        // Merge standalone URLs
        var allUrlKeys = models.SelectMany(m => m.StandaloneUrls.Select(u => u.RubyKey)).Distinct().ToList();
        foreach (var key in allUrlKeys)
        {
            var first = models.First(m => m.StandaloneUrls.Any(u => u.RubyKey == key));
            var last = models.Last(m => m.StandaloneUrls.Any(u => u.RubyKey == key));
            var latestUrl = models.Last(m => m.StandaloneUrls.Any(u => u.RubyKey == key))
                .StandaloneUrls.First(u => u.RubyKey == key);

            unified.StandaloneUrls.Add(new UnifiedStandaloneUrl
            {
                Url = latestUrl,
                SinceVersion = first.Version == firstVersion ? null : first.Version,
                UntilVersion = last.Version == lastVersion ? null : last.Version,
            });
        }

        // Merge prefix groups
        var allPrefixes = models.SelectMany(m => m.PrefixGroups.Select(g => g.Prefix)).Distinct().ToList();
        foreach (var prefix in allPrefixes)
        {
            var first = models.First(m => m.PrefixGroups.Any(g => g.Prefix == prefix));
            var last = models.Last(m => m.PrefixGroups.Any(g => g.Prefix == prefix));

            // Collect all versioned roots for this prefix
            var versionedRoots = new List<(string Version, GitLabRbObjectNode Root)>();
            foreach (var model in models)
            {
                var group = model.PrefixGroups.FirstOrDefault(g => g.Prefix == prefix);
                if (group != null)
                    versionedRoots.Add((model.Version, group.Root));
            }

            var mergedRoot = MergeNodes(versionedRoots, firstVersion, lastVersion);

            unified.PrefixGroups.Add(new UnifiedPrefixGroup
            {
                Prefix = prefix,
                Root = mergedRoot,
                SinceVersion = first.Version == firstVersion ? null : first.Version,
                UntilVersion = last.Version == lastVersion ? null : last.Version,
            });
        }

        return unified;
    }

    private static UnifiedObjectNode MergeNodes(
        List<(string Version, GitLabRbObjectNode Node)> versionedNodes,
        string firstVersion, string lastVersion)
    {
        if (versionedNodes.Count == 0)
            return new UnifiedObjectNode();

        var latestNode = versionedNodes[versionedNodes.Count - 1].Node;
        var merged = new UnifiedObjectNode
        {
            Name = latestNode.Name,
            DocComment = latestNode.DocComment,
            LeafType = latestNode.LeafType,
            ExampleValue = latestNode.ExampleValue,
            IsArrayOfObjects = latestNode.IsArrayOfObjects,
        };

        // Collect all child keys across all versions
        var allChildKeys = versionedNodes
            .SelectMany(vn => vn.Node.Children.Keys)
            .Distinct()
            .ToList();

        foreach (var childKey in allChildKeys)
        {
            var childVersions = versionedNodes
                .Where(vn => vn.Node.Children.ContainsKey(childKey))
                .ToList();

            if (childVersions.Count == 0) continue;

            var firstChildVersion = childVersions[0].Version;
            var lastChildVersion = childVersions[childVersions.Count - 1].Version;

            var childNodes = childVersions
                .Select(cv => (cv.Version, cv.Node.Children[childKey]))
                .ToList();

            var mergedChild = MergeNodes(childNodes, firstVersion, lastVersion);
            mergedChild.SinceVersion = firstChildVersion == firstVersion ? null : firstChildVersion;
            mergedChild.UntilVersion = lastChildVersion == lastVersion ? null : lastChildVersion;

            merged.Children[childKey] = mergedChild;
        }

        return merged;
    }
}

/// <summary>Merged model across all versions.</summary>
internal sealed class UnifiedGitLabRbModel
{
    public List<string> Versions { get; set; } = new List<string>();
    public List<UnifiedPrefixGroup> PrefixGroups { get; set; } = new List<UnifiedPrefixGroup>();
    public List<UnifiedStandaloneUrl> StandaloneUrls { get; set; } = new List<UnifiedStandaloneUrl>();
}

internal sealed class UnifiedPrefixGroup
{
    public string Prefix { get; set; } = "";
    public UnifiedObjectNode Root { get; set; } = new UnifiedObjectNode();
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }
}

internal sealed class UnifiedStandaloneUrl
{
    public GitLabRbStandaloneUrl Url { get; set; } = new GitLabRbStandaloneUrl();
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }
}

/// <summary>Merged node with version metadata.</summary>
internal sealed class UnifiedObjectNode
{
    public string Name { get; set; } = "";
    public string? DocComment { get; set; }
    public Dictionary<string, UnifiedObjectNode> Children { get; set; } = new Dictionary<string, UnifiedObjectNode>();
    public GitLabRbValueType? LeafType { get; set; }
    public string? ExampleValue { get; set; }
    public bool IsArrayOfObjects { get; set; }
    public string? SinceVersion { get; set; }
    public string? UntilVersion { get; set; }
}
