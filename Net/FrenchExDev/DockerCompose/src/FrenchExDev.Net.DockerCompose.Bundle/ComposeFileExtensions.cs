namespace FrenchExDev.Net.DockerCompose.Bundle;

/// <summary>
/// Fluent extension methods for applying <see cref="IComposeFileContributor"/> instances to a <see cref="ComposeFile"/>.
/// </summary>
public static class ComposeFileExtensions
{
    /// <summary>Applies a contributor to this compose file.</summary>
    public static ComposeFile Apply(this ComposeFile file, IComposeFileContributor contributor)
    {
        contributor.Contribute(file);
        return file;
    }

    /// <summary>Applies multiple contributors in order.</summary>
    public static ComposeFile Apply(this ComposeFile file, params IComposeFileContributor[] contributors)
    {
        foreach (var c in contributors)
            c.Contribute(file);
        return file;
    }
}
