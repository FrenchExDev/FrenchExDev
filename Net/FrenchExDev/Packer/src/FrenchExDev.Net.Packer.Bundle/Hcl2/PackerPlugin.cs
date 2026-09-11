namespace FrenchExDev.Net.Packer.Bundle.Hcl2;

/// <summary>
/// A required plugin declaration inside <c>packer { required_plugins { } }</c>.
/// </summary>
public sealed record PackerPlugin(string Version, string Source);
