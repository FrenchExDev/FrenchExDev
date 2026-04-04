namespace FrenchExDev.Net.Vos.Bundle;

/// <summary>
/// Mutable in-memory workspace for a complete Vos project.
/// Like PackerBundle: compose from contributors, then materialize to disk via VosBundleWriter.
/// </summary>
public sealed class VosBundle
{
    public VosConfig Config { get; set; } = new();
    public VosConfig? LocalOverrides { get; set; }
    public SortedList<string, VosBundleFile> Files { get; } = new(StringComparer.Ordinal);

    public IEnumerable<VosBundleFile> ProvisioningScripts =>
        Files.Values.Where(f => f.Directory.StartsWith("provisioning", StringComparison.OrdinalIgnoreCase));

    public IEnumerable<VosBundleFile> SharedFiles =>
        Files.Values.Where(f => f.Directory.StartsWith("files", StringComparison.OrdinalIgnoreCase));

    public VosBundle Apply(params IVosBundleContributor[] contributors)
    {
        foreach (var c in contributors) c.Contribute(this);
        return this;
    }

    public void AddProvisioningScript(string key, string version, string content, string extension = "sh")
    {
        var dir = $"provisioning/{version}";
        var fileName = $"{key}.{extension}";
        Files[$"{dir}/{fileName}"] = new VosBundleFile(dir, fileName, extension, content);
    }

    public void AddFile(string directory, string name, string extension, string content)
    {
        Files[$"{directory}/{name}"] = new VosBundleFile(directory, name, extension, content);
    }
}
