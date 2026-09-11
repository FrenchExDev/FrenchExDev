using FrenchExDev.Net.Packer.Bundle;
using FrenchExDev.Net.Packer.Bundle.Hcl2;

namespace FrenchExDev.Net.Packer.Alpine;

/// <summary>
/// Contributes Alpine Linux base configuration to a <see cref="PackerBundle"/>.
/// Adds provisioning scripts, answer file, vagrant box files, and HCL2 source/build config.
/// Maps to the PowerShell <c>New-PackerAlpine</c> function.
/// </summary>
public sealed class AlpineBaseContributor : IPackerBundleContributor
{
    private readonly AlpinePackerConfig _config;
    private readonly AlpineAnswerFileConfig _answerFileConfig;

    public AlpineBaseContributor(AlpinePackerConfig? config = null, AlpineAnswerFileConfig? answerFileConfig = null)
    {
        _config = config ?? new AlpinePackerConfig();
        _answerFileConfig = answerFileConfig ?? new AlpineAnswerFileConfig();
    }

    public void Contribute(PackerBundle bundle)
    {
        ContributeConfig(bundle);
        ContributeVariables(bundle);
        ContributeLocals(bundle);
        ContributeSource(bundle);
        ContributeBuild(bundle);
        ContributeScripts(bundle);
        ContributeHttpFiles(bundle);
        ContributeVagrantFiles(bundle);
        ContributeEnvTemplate(bundle);
    }

    private void ContributeConfig(PackerBundle bundle)
    {
        bundle.Config
            .WithRequiredVersion(">= 1.7.0")
            .WithRequiredPlugin("virtualbox", ">= 1.1.0", "github.com/hashicorp/virtualbox");
    }

    private void ContributeVariables(PackerBundle bundle)
    {
        bundle.Variables.Add(new PackerVariable { Name = "alpine_version", Type = "string", Default = _config.AlpineVersion, Description = "Alpine Linux version" });
        bundle.Variables.Add(new PackerVariable { Name = "cpus", Type = "number", Default = _config.Cpus });
        bundle.Variables.Add(new PackerVariable { Name = "memory", Type = "number", Default = _config.Memory });
        bundle.Variables.Add(new PackerVariable { Name = "video_memory", Type = "number", Default = _config.VideoMemory });
        bundle.Variables.Add(new PackerVariable { Name = "disk_size", Type = "number", Default = _config.DiskSize });
        bundle.Variables.Add(new PackerVariable { Name = "boot_wait", Type = "string", Default = _config.BootWait });
        bundle.Variables.Add(new PackerVariable { Name = "ssh_timeout", Type = "string", Default = _config.SshTimeout });
        bundle.Variables.Add(new PackerVariable { Name = "ssh_username", Type = "string", Default = _config.SshUsername });
        bundle.Variables.Add(new PackerVariable { Name = "ssh_password", Type = "string", Default = _config.SshPassword, Sensitive = true });
        bundle.Variables.Add(new PackerVariable { Name = "root_password", Type = "string", Default = _config.RootPassword, Sensitive = true });
        bundle.Variables.Add(new PackerVariable { Name = "box_version", Type = "string", Default = _config.BoxVersion });
    }

    private void ContributeLocals(PackerBundle bundle)
    {
        bundle.Locals.Add(new PackerLocal
        {
            Name = "vm_name",
            Expression = $"\"alpine-${{var.alpine_version}}-{_config.Flavor}\""
        });
        bundle.Locals.Add(new PackerLocal
        {
            Name = "iso_url",
            Expression = $"\"http://dl-cdn.alpinelinux.org/alpine/v${{var.alpine_version}}/releases/{_config.Arch}/alpine-{_config.Flavor}-${{var.alpine_version}}.0-{_config.Arch}.iso\""
        });
    }

    private void ContributeSource(PackerBundle bundle)
    {
        var vboxConfig = new VBoxManageConfig
        {
            Cpus = _config.Cpus,
            Memory = _config.Memory,
            VideoMemory = _config.VideoMemory,
            IoApic = true,
            HwVirtEx = true,
            NestedHwVirt = true,
            NestedPaging = true,
            LargePages = true,
            Pae = true,
            Acpi = true,
            StorageControllerName = "SATA Controller",
            HostIoCache = true,
            NonRotational = true,
            Discard = true
        };

        bundle.Sources.Add(new PackerSource
        {
            Type = "virtualbox-iso",
            Name = "alpine",
            Arguments = new Dictionary<string, object?>
            {
                ["vm_name"] = Hcl.Local("vm_name"),
                ["guest_os_type"] = "Linux_64",
                ["iso_url"] = Hcl.Local("iso_url"),
                ["iso_checksum"] = Hcl.Raw("\"sha256:TODO\""),
                ["disk_size"] = Hcl.Var("disk_size"),
                ["headless"] = false,
                ["http_directory"] = "http",
                ["ssh_username"] = "root",
                ["ssh_password"] = Hcl.Var("root_password"),
                ["ssh_timeout"] = Hcl.Var("ssh_timeout"),
                ["shutdown_command"] = "/sbin/poweroff",
                ["format"] = "ova",
                ["hard_drive_interface"] = "sata",
                ["hard_drive_discard"] = true,
                ["hard_drive_nonrotational"] = true,
                ["nested_virt"] = true,
                ["boot_wait"] = Hcl.Var("boot_wait"),
                ["boot_command"] = new List<string>
                {
                    "<wait10>",
                    "root<enter><wait>",
                    "ifconfig eth0 up && udhcpc -i eth0<enter><wait5>",
                    "wget http://{{ .HTTPIP }}:{{ .HTTPPort }}/answers<enter><wait>",
                    "setup-alpine -f answers<enter><wait5>",
                    "${var.root_password}<enter><wait>",
                    "${var.root_password}<enter><wait>",
                    "<wait10>reboot<enter>"
                },
                ["vboxmanage"] = vboxConfig.ToCommands()
                    .Select(c => (IReadOnlyList<string>)c.AsReadOnly()).ToList()
            }
        });
    }

    private void ContributeBuild(PackerBundle bundle)
    {
        bundle.Build
            .WithName("alpine")
            .WithSource("source.virtualbox-iso.alpine");

        // Provisioners will be added after scripts are known
        // The CLI will finalize the build with script paths
    }

    private void ContributeScripts(PackerBundle bundle)
    {
        bundle.AddScript("00base", AlpineScripts.Base);
        bundle.AddScript("01alpine", AlpineScripts.Alpine);
        bundle.AddScript("01networking", AlpineScripts.Networking);
        bundle.AddScript("02sshd", AlpineScripts.Sshd);
        bundle.AddScript("03vagrant", AlpineScripts.Vagrant);
        bundle.AddScript("04sudoers", AlpineScripts.Sudoers);
        bundle.AddScript("05cron", AlpineScripts.Cron);
        bundle.AddScript("08virtualbox-guest-additions", AlpineScripts.VBoxGuestAdditions);
        bundle.AddScript("99disable-ssh-root", AlpineScripts.DisableSshRoot);
        bundle.AddScript("99minimize", AlpineScripts.Minimize);
        bundle.AddScript("99reboot", AlpineScripts.Reboot);

        // Add shell provisioner with all scripts
        bundle.Build.WithProvisioner(new PackerProvisioner
        {
            Type = "shell",
            Arguments = new Dictionary<string, object?>
            {
                ["scripts"] = bundle.Scripts.Select(s => s.RelativePath).ToList()
            }
        });
    }

    private void ContributeHttpFiles(PackerBundle bundle)
    {
        var answerFileGenerator = new AlpineAnswerFileGenerator();
        bundle.AddFile("http", "answers", "", answerFileGenerator.Generate(_answerFileConfig));

        bundle.AddFile("http", "ssh.keys", "", VagrantSshKey.InsecurePublicKey);
    }

    private void ContributeVagrantFiles(PackerBundle bundle)
    {
        bundle.AddFile("vagrant", "metadata", ".json",
            $"{{\"provider\": \"virtualbox\", \"architecture\": \"{_config.Arch}\"}}");

        bundle.AddFile("vagrant", "info", ".json",
            $"{{\"author\": \"{_config.Author}\", \"description\": \"{_config.Description}\"}}");

        bundle.Vagrantfile.Cpus = _config.Cpus;
        bundle.Vagrantfile.Memory = _config.Memory;
        bundle.Vagrantfile.VideoMemory = _config.VideoMemory;
        bundle.Vagrantfile.Provider = "virtualbox";
    }

    private void ContributeEnvTemplate(PackerBundle bundle)
    {
        bundle.EnvTemplate.Variables.Add(new EnvVariable
            { Key = "ALPINE_VERSION", DefaultValue = _config.AlpineVersion, Description = "Alpine Linux version" });
        bundle.EnvTemplate.Variables.Add(new EnvVariable
            { Key = "BOX_VERSION", DefaultValue = _config.BoxVersion, Description = "Vagrant box version" });
    }
}
