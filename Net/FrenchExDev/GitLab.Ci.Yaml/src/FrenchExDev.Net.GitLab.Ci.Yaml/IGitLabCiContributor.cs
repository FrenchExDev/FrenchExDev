namespace FrenchExDev.Net.GitLab.Ci.Yaml;

/// <summary>
/// Extension point for composing a <see cref="GitLabCiFile"/>.
/// Contributors add/modify jobs, stages, variables, and other CI elements.
/// </summary>
public interface IGitLabCiContributor
{
    void Contribute(GitLabCiFile ciFile);
}
