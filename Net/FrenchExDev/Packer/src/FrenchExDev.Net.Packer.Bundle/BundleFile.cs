namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// A file in the Packer bundle (script, config, template, .env, etc.).
/// Mutable — contributors can modify name, content, or extension.
/// </summary>
public sealed class BundleFile
{
    public required string Name { get; set; }
    public required string Extension { get; set; }
    public required string Content { get; set; }
    public required string Directory { get; set; }

    /// <summary>Relative path: Directory/Name + Extension (e.g. "scripts/06docker.sh").</summary>
    public string RelativePath => string.IsNullOrEmpty(Directory)
        ? $"{Name}{Extension}"
        : $"{Directory}/{Name}{Extension}";
}
