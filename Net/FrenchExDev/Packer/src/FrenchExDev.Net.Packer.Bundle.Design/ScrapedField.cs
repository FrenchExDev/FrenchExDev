namespace FrenchExDev.Net.Packer.Bundle.Design;

/// <summary>
/// A single field extracted from a <c>.hcl2spec.go</c> file's <c>HCL2Spec()</c> method.
/// </summary>
public sealed record ScrapedField(string HclName, string CtyType, bool IsRequired);

/// <summary>
/// A nested block reference extracted from <c>hcldec.BlockListSpec</c>.
/// </summary>
public sealed record ScrapedNestedBlock(string HclName, string NestedTypeName);

/// <summary>
/// A fully scraped plugin type — all fields and nested blocks from one <c>.hcl2spec.go</c>.
/// </summary>
public sealed record ScrapedPluginType
{
    public required string PluginKind { get; init; }   // "builder", "provisioner", "post-processor", "datasource"
    public required string TypeName { get; init; }     // "virtualbox-iso", "shell", "vagrant"
    public required string Repo { get; init; }         // "hashicorp/packer-plugin-virtualbox"
    public required string SourcePath { get; init; }   // "builder/virtualbox/iso/builder.hcl2spec.go"
    public required List<ScrapedField> Fields { get; init; }
    public List<ScrapedNestedBlock> NestedBlocks { get; init; } = new();
}
