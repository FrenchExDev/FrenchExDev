namespace FrenchExDev.Net.GitLab.Ci.Yaml;

/// <summary>
/// Fluent extension methods for applying <see cref="IGitLabCiContributor"/> instances to a <see cref="GitLabCiFile"/>.
/// </summary>
public static class GitLabCiFileExtensions
{
    /// <summary>Applies a contributor to this CI file.</summary>
    public static GitLabCiFile Apply(this GitLabCiFile file, IGitLabCiContributor contributor)
    {
        contributor.Contribute(file);
        return file;
    }

    /// <summary>Applies multiple contributors in order.</summary>
    public static GitLabCiFile Apply(this GitLabCiFile file, params IGitLabCiContributor[] contributors)
    {
        foreach (var c in contributors)
            c.Contribute(file);
        return file;
    }
}
