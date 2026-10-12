using System.Collections.Generic;

namespace FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator;

/// <summary>
/// Parsed result of a single versioned gitlab.rb.template file.
/// </summary>
internal sealed class GitLabRbModel
{
    public string Version { get; set; } = "";
    public List<GitLabRbPrefixGroup> PrefixGroups { get; set; } = new List<GitLabRbPrefixGroup>();
    public List<GitLabRbStandaloneUrl> StandaloneUrls { get; set; } = new List<GitLabRbStandaloneUrl>();
}

/// <summary>
/// All settings sharing the same Ruby prefix (e.g., "nginx", "redis", "gitlab_rails").
/// </summary>
internal sealed class GitLabRbPrefixGroup
{
    public string Prefix { get; set; } = "";
    public GitLabRbObjectNode Root { get; set; } = new GitLabRbObjectNode { Name = "" };
}

/// <summary>
/// A node in the property tree. Leaf nodes have a <see cref="LeafType"/>;
/// branch nodes have <see cref="Children"/>.
/// Hash values and bracket nesting both produce branches.
/// </summary>
internal sealed class GitLabRbObjectNode
{
    public string Name { get; set; } = "";
    public string? DocComment { get; set; }
    public Dictionary<string, GitLabRbObjectNode> Children { get; set; } = new Dictionary<string, GitLabRbObjectNode>();
    public GitLabRbValueType? LeafType { get; set; }
    public string? ExampleValue { get; set; }
    public bool IsArrayOfObjects { get; set; }
}

/// <summary>
/// A standalone URL assignment like <c>external_url 'https://...'</c>.
/// </summary>
internal sealed class GitLabRbStandaloneUrl
{
    public string RubyKey { get; set; } = "";
    public string? ExampleValue { get; set; }
    public string? DocComment { get; set; }
}

/// <summary>
/// Inferred Ruby value type from the example value in the template.
/// </summary>
internal enum GitLabRbValueType
{
    String,
    Integer,
    Long,
    Boolean,
    Float,
    StringList,
    FloatList,
    StringDict,
    Nil,
}
