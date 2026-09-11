using FrenchExDev.Net.Packer.Bundle.Hcl2;

namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// Mutable in-memory workspace representing a complete Packer project.
/// NOT serializable — contributors modify it in-place, then
/// <see cref="PackerBundleWriter"/> renders it to <c>.pkr.hcl</c> files + companion files on disk.
/// </summary>
public sealed class PackerBundle
{
    // ── HCL2 content ────────────────────────────────────────────────
    // Config and Build are mutable builders; Variables/Locals/Sources are lists of immutable records.

    /// <summary><c>packer { required_plugins { } }</c> — mutable builder.</summary>
    public PackerConfigBuilder Config { get; } = new();

    /// <summary><c>variable "name" { }</c> blocks — add built records.</summary>
    public List<PackerVariable> Variables { get; } = new();

    /// <summary><c>locals { }</c> entries — add built records.</summary>
    public List<PackerLocal> Locals { get; } = new();

    /// <summary><c>source "type" "name" { }</c> blocks — add built records.</summary>
    public List<PackerSource> Sources { get; } = new();

    /// <summary><c>build { }</c> block — mutable builder.</summary>
    public PackerBuildBuilder Build { get; } = new();

    // ── Companion models (mutable, contributors modify in-place) ────

    /// <summary>Configuration for the embedded Vagrantfile (rendered as Ruby).</summary>
    public VagrantfileConfig Vagrantfile { get; set; } = new();

    /// <summary>.env.template model (variable definitions with defaults/descriptions).</summary>
    public EnvTemplate EnvTemplate { get; set; } = new();

    /// <summary>.env model (actual key=value pairs).</summary>
    public EnvValues EnvValues { get; set; } = new();

    // ── Companion files (mutable, contributors add/modify) ──────────

    /// <summary>All companion files in the bundle, keyed by relative path, sorted.</summary>
    public SortedList<string, BundleFile> Files { get; } = new(StringComparer.Ordinal);

    // ── Convenience accessors ───────────────────────────────────────

    /// <summary>All script files (directory = "scripts").</summary>
    public IEnumerable<BundleFile> Scripts => Files.Values.Where(f => f.Directory == "scripts");

    /// <summary>All HTTP-served files (directory = "http").</summary>
    public IEnumerable<BundleFile> HttpFiles => Files.Values.Where(f => f.Directory == "http");

    /// <summary>All Vagrant box files (directory = "vagrant").</summary>
    public IEnumerable<BundleFile> VagrantFiles => Files.Values.Where(f => f.Directory == "vagrant");

    // ── Contributor pipeline ────────────────────────────────────────

    /// <summary>Applies a contributor to this bundle.</summary>
    public PackerBundle Apply(IPackerBundleContributor contributor)
    {
        contributor.Contribute(this);
        return this;
    }

    /// <summary>Applies multiple contributors in order.</summary>
    public PackerBundle Apply(params IPackerBundleContributor[] contributors)
    {
        foreach (var c in contributors)
            c.Contribute(this);
        return this;
    }

    // ── File helpers ────────────────────────────────────────────────

    /// <summary>Adds or replaces a companion file.</summary>
    public void AddFile(string directory, string name, string extension, string content)
    {
        var file = new BundleFile
        {
            Name = name,
            Extension = extension,
            Directory = directory,
            Content = content
        };
        Files[file.RelativePath] = file;
    }

    /// <summary>Adds a shell script to the scripts/ directory.</summary>
    public void AddScript(string name, string content)
    {
        AddFile("scripts", name, ".sh", content);
    }
}
