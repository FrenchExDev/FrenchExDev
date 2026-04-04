namespace FrenchExDev.Net.DockerCompose.Bundle;

/// <summary>
/// Extension point for composing a <see cref="ComposeFile"/>.
/// Contributors add/modify services, networks, volumes, and other compose elements.
/// </summary>
public interface IComposeFileContributor
{
    void Contribute(ComposeFile composeFile);
}
