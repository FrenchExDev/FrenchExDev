namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// Extension point for composing a <see cref="PackerBundle"/>.
/// Contributors add/modify files, variables, sources, and build configuration.
/// </summary>
public interface IPackerBundleContributor
{
    void Contribute(PackerBundle bundle);
}
