namespace FrenchExDev.Net.Packer.Bundle.Hcl2;

/// <summary>
/// Models the <c>packer { }</c> block: required_version and required_plugins.
/// Immutable record — build via <see cref="PackerConfigBuilder"/>.
/// </summary>
public sealed record PackerConfig
{
    public string? RequiredVersion { get; init; }
    public IReadOnlyDictionary<string, PackerPlugin> RequiredPlugins { get; init; }
        = new Dictionary<string, PackerPlugin>();

    /// <summary>Writes this config as HCL2 to the given writer.</summary>
    public void WriteTo(HclWriter writer)
    {
        using var packer = writer.Block("packer");

        writer.Argument("required_version", RequiredVersion);

        if (RequiredPlugins.Count > 0)
        {
            writer.BlankLine();
            using var plugins = writer.Block("required_plugins");
            foreach (var kvp in RequiredPlugins)
            {
                using var plugin = writer.Block(kvp.Key);
                writer.Argument("version", kvp.Value.Version);
                writer.Argument("source", kvp.Value.Source);
            }
        }
    }
}

/// <summary>
/// Mutable builder for <see cref="PackerConfig"/>. Lives on the <see cref="PackerBundle"/> workspace.
/// </summary>
public sealed class PackerConfigBuilder
{
    public string? RequiredVersion { get; set; }
    public Dictionary<string, PackerPlugin> RequiredPlugins { get; } = new();

    public PackerConfigBuilder WithRequiredVersion(string version)
    {
        RequiredVersion = version;
        return this;
    }

    public PackerConfigBuilder WithRequiredPlugin(string name, string version, string source)
    {
        RequiredPlugins[name] = new PackerPlugin(version, source);
        return this;
    }

    public PackerConfig Build() => new()
    {
        RequiredVersion = RequiredVersion,
        RequiredPlugins = new Dictionary<string, PackerPlugin>(RequiredPlugins)
    };
}
