using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Alpine;

/// <summary>
/// Contributes Alpine-specific VirtualBox defaults to a <see cref="VosMachineType"/>.
/// Configures SATA SSD storage, nested virtualization, NIC promiscuous mode,
/// and Vagrant plugins (hostmanager, vbguest).
/// Maps to the PowerShell <c>New-VosAlpine</c> function.
/// </summary>
public sealed class AlpineVirtualBoxContributor : IMachineTypeContributor
{
    private readonly string _alpineVersion;
    private readonly string _boxName;

    public AlpineVirtualBoxContributor(string alpineVersion = "3.21", string? boxName = null)
    {
        _alpineVersion = alpineVersion;
        _boxName = boxName ?? $"frenchexdev/alpine-{alpineVersion}-virt";
    }

    public string MachineTypeName => "alpine";

    public void Contribute(VosMachineType machineType)
    {
        machineType.Box ??= _boxName;
        machineType.IsEnabled = true;

        // Provider: VirtualBox with hardware virtualization
        machineType.Provider ??= new VosProviderConfig();
        machineType.Provider.Type = "virtualbox";
        machineType.Provider.Memory = 2048;
        machineType.Provider.Cpus = 2;
        machineType.Provider.LinkedClones = true;

        // VBoxManage: SATA SSD, nested virt, NIC promisc
        machineType.Provider.VboxManage = new List<List<string>>
        {
            new() { "modifyvm", "{{ .Name }}", "--ioapic", "on" },
            new() { "modifyvm", "{{ .Name }}", "--hwvirtex", "on" },
            new() { "modifyvm", "{{ .Name }}", "--nested-hw-virt", "on" },
            new() { "modifyvm", "{{ .Name }}", "--nestedpaging", "on" },
            new() { "modifyvm", "{{ .Name }}", "--largepages", "on" },
            new() { "modifyvm", "{{ .Name }}", "--pae", "on" },
            new() { "storagectl", "{{ .Name }}", "--name=SATA Controller", "--hostiocache", "on" },
            new() { "storageattach", "{{ .Name }}", "--storagectl=SATA Controller", "--port=0", "--nonrotational", "on" },
            new() { "modifyvm", "{{ .Name }}", "--nicpromisc2", "allow-all" },
        };

        // Plugins
        if (!machineType.Plugins.Contains("vagrant-hostmanager"))
            machineType.Plugins.Add("vagrant-hostmanager");
        if (!machineType.Plugins.Contains("vagrant-vbguest"))
            machineType.Plugins.Add("vagrant-vbguest");

        // Variables
        machineType.Variables.TryAdd("ALPINE_VERSION", _alpineVersion);
    }
}
