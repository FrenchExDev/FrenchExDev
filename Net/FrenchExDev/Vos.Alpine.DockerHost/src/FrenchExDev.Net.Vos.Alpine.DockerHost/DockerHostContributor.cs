using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Alpine.DockerHost;

/// <summary>
/// Contributes "docker-host" machine type defaults to a <see cref="VosMachineType"/>.
/// Adds Docker provisioning, shared folders for docker-compose/posh/k8s paths,
/// and Docker-specific variables.
/// Maps to the PowerShell <c>New-VosAlpineDocker</c> function.
/// </summary>
public sealed class DockerHostContributor : IMachineTypeContributor
{
    public string MachineTypeName => "docker-host";

    public void Contribute(VosMachineType machineType)
    {
        // Apply Alpine base first
        new AlpineVirtualBoxContributor().Contribute(machineType);

        // Docker-specific provisioning
        machineType.Provisioning.Add(new VosProvisioningStep
        {
            Key = "docker",
            Version = "1.0",
            Enabled = true,
            Privileged = true,
            Env = new Dictionary<string, string>
            {
                ["DOCKER_COMPOSE_VERSION"] = "latest"
            }
        });

        // Shared folders for Docker workflows
        machineType.SharedFolders.Add(new VosSharedFolder
        {
            HostPath = "./docker-compose",
            GuestPath = "/opt/docker-compose",
            Type = "virtualbox"
        });

        machineType.SharedFolders.Add(new VosSharedFolder
        {
            HostPath = "./data",
            GuestPath = "/data"
        });

        // Docker variables
        machineType.Variables.TryAdd("DOCKER_HOST_TYPE", "alpine");
        machineType.Variables.TryAdd("DOCKER_BRIDGE", "docker0");
    }
}
