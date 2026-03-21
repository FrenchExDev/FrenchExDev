using FrenchExDev.Net.Packer.Bundle;

namespace FrenchExDev.Net.Packer.Alpine.DockerHost;

/// <summary>
/// Contributes Docker provisioning to an Alpine <see cref="PackerBundle"/>.
/// Adds the <c>06docker.sh</c> script that installs Docker, docker-cli-compose,
/// openrc, and adds the vagrant user to the docker group.
/// Maps to the PowerShell <c>New-PackerAlpineDocker</c> function.
/// </summary>
public sealed class DockerContributor : IPackerBundleContributor
{
    public void Contribute(PackerBundle bundle)
    {
        // Add Docker provisioning script
        bundle.AddScript("06docker", DockerScript);

        // Add .env template variable for Docker bridge
        bundle.EnvTemplate.Variables.Add(new EnvVariable
        {
            Key = "DOCKER_BRIDGE",
            Description = "Docker bridge network name"
        });

        // Rebuild the shell provisioner scripts list to include 06docker
        // (it will be sorted lexicographically, so 06docker comes after 05cron)
        var shellProvisioner = bundle.Build.Provisioners
            .FirstOrDefault(p => p.Type == "shell");

        if (shellProvisioner is not null)
        {
            shellProvisioner.Arguments["scripts"] =
                bundle.Scripts.Select(s => s.RelativePath).ToList();
        }
    }

    public const string DockerScript = """
        #!/bin/sh
        set -eux
        echo '*** Docker Installation ***'
        apk add docker docker-cli-compose openrc
        rc-update add docker default
        addgroup vagrant docker
        service docker start || true
        """;
}
