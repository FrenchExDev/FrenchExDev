namespace FrenchExDev.Net.Packer.Bundle.Design;

/// <summary>
/// An entry in the plugin registry: org/repo + list of primary .hcl2spec.go paths.
/// </summary>
public sealed record PluginRegistryEntry(string Org, string Repo, string Path, string PluginKind, string TypeName);

/// <summary>
/// Hardcoded registry of all Packer plugin repos and their primary <c>.hcl2spec.go</c> file paths.
/// Derived from GitHub API tree listing of 25 repos (145 total files, ~75 primary configs).
/// </summary>
public static class PluginRegistry
{
    public static IReadOnlyList<PluginRegistryEntry> Entries { get; } = new List<PluginRegistryEntry>
    {
        // ── VirtualBox ──
        E("hashicorp", "packer-plugin-virtualbox", "builder/virtualbox/iso/builder.hcl2spec.go", "builder", "virtualbox-iso"),
        E("hashicorp", "packer-plugin-virtualbox", "builder/virtualbox/ovf/config.hcl2spec.go", "builder", "virtualbox-ovf"),
        E("hashicorp", "packer-plugin-virtualbox", "builder/virtualbox/vm/config.hcl2spec.go", "builder", "virtualbox-vm"),

        // ── Hyper-V ──
        E("hashicorp", "packer-plugin-hyperv", "builder/hyperv/iso/builder.hcl2spec.go", "builder", "hyperv-iso"),
        E("hashicorp", "packer-plugin-hyperv", "builder/hyperv/vmcx/builder.hcl2spec.go", "builder", "hyperv-vmcx"),

        // ── QEMU ──
        E("hashicorp", "packer-plugin-qemu", "builder/qemu/config.hcl2spec.go", "builder", "qemu"),

        // ── Docker ──
        E("hashicorp", "packer-plugin-docker", "builder/docker/config.hcl2spec.go", "builder", "docker"),
        E("hashicorp", "packer-plugin-docker", "post-processor/docker-import/post-processor.hcl2spec.go", "post-processor", "docker-import"),
        E("hashicorp", "packer-plugin-docker", "post-processor/docker-push/post-processor.hcl2spec.go", "post-processor", "docker-push"),
        E("hashicorp", "packer-plugin-docker", "post-processor/docker-save/post-processor.hcl2spec.go", "post-processor", "docker-save"),
        E("hashicorp", "packer-plugin-docker", "post-processor/docker-tag/post-processor.hcl2spec.go", "post-processor", "docker-tag"),

        // ── Amazon ──
        E("hashicorp", "packer-plugin-amazon", "builder/ebs/builder.hcl2spec.go", "builder", "amazon-ebs"),
        E("hashicorp", "packer-plugin-amazon", "builder/ebssurrogate/builder.hcl2spec.go", "builder", "amazon-ebssurrogate"),
        E("hashicorp", "packer-plugin-amazon", "builder/ebsvolume/builder.hcl2spec.go", "builder", "amazon-ebsvolume"),
        E("hashicorp", "packer-plugin-amazon", "builder/instance/builder.hcl2spec.go", "builder", "amazon-instance"),
        E("hashicorp", "packer-plugin-amazon", "builder/chroot/builder.hcl2spec.go", "builder", "amazon-chroot"),
        E("hashicorp", "packer-plugin-amazon", "datasource/ami/data.hcl2spec.go", "datasource", "amazon-ami"),
        E("hashicorp", "packer-plugin-amazon", "datasource/parameterstore/data.hcl2spec.go", "datasource", "amazon-parameterstore"),
        E("hashicorp", "packer-plugin-amazon", "datasource/secretsmanager/data.hcl2spec.go", "datasource", "amazon-secretsmanager"),
        E("hashicorp", "packer-plugin-amazon", "post-processor/import/post-processor.hcl2spec.go", "post-processor", "amazon-import"),

        // ── Azure ──
        E("hashicorp", "packer-plugin-azure", "builder/azure/arm/config.hcl2spec.go", "builder", "azure-arm"),
        E("hashicorp", "packer-plugin-azure", "builder/azure/chroot/builder.hcl2spec.go", "builder", "azure-chroot"),
        E("hashicorp", "packer-plugin-azure", "builder/azure/dtl/config.hcl2spec.go", "builder", "azure-dtl"),
        E("hashicorp", "packer-plugin-azure", "datasource/keyvaultsecret/data.hcl2spec.go", "datasource", "azure-keyvaultsecret"),

        // ── Google Compute ──
        E("hashicorp", "packer-plugin-googlecompute", "builder/googlecompute/config.hcl2spec.go", "builder", "googlecompute"),
        E("hashicorp", "packer-plugin-googlecompute", "datasource/image/data.hcl2spec.go", "datasource", "googlecompute-image"),
        E("hashicorp", "packer-plugin-googlecompute", "post-processor/googlecompute-export/post-processor.hcl2spec.go", "post-processor", "googlecompute-export"),
        E("hashicorp", "packer-plugin-googlecompute", "post-processor/googlecompute-import/post-processor.hcl2spec.go", "post-processor", "googlecompute-import"),

        // ── Vagrant ──
        E("hashicorp", "packer-plugin-vagrant", "builder/vagrant/builder.hcl2spec.go", "builder", "vagrant"),
        E("hashicorp", "packer-plugin-vagrant", "post-processor/vagrant/post-processor.hcl2spec.go", "post-processor", "vagrant"),
        E("hashicorp", "packer-plugin-vagrant", "post-processor/vagrant-cloud/post-processor.hcl2spec.go", "post-processor", "vagrant-cloud"),

        // ── Ansible ──
        E("hashicorp", "packer-plugin-ansible", "provisioner/ansible/provisioner.hcl2spec.go", "provisioner", "ansible"),
        E("hashicorp", "packer-plugin-ansible", "provisioner/ansible-local/provisioner.hcl2spec.go", "provisioner", "ansible-local"),

        // ── Proxmox ──
        E("hashicorp", "packer-plugin-proxmox", "builder/proxmox/iso/config.hcl2spec.go", "builder", "proxmox-iso"),
        E("hashicorp", "packer-plugin-proxmox", "builder/proxmox/clone/config.hcl2spec.go", "builder", "proxmox-clone"),

        // ── OpenStack ──
        E("hashicorp", "packer-plugin-openstack", "builder/openstack/builder.hcl2spec.go", "builder", "openstack"),

        // ── Alicloud ──
        E("hashicorp", "packer-plugin-alicloud", "builder/ecs/builder.hcl2spec.go", "builder", "alicloud-ecs"),
        E("hashicorp", "packer-plugin-alicloud", "post-processor/alicloud-import/post-processor.hcl2spec.go", "post-processor", "alicloud-import"),

        // ── Oracle ──
        E("hashicorp", "packer-plugin-oracle", "builder/classic/builder.hcl2spec.go", "builder", "oracle-classic"),
        E("hashicorp", "packer-plugin-oracle", "builder/oci/config.hcl2spec.go", "builder", "oracle-oci"),

        // ── LXD / LXC ──
        E("hashicorp", "packer-plugin-lxd", "builder/lxd/config.hcl2spec.go", "builder", "lxd"),
        E("hashicorp", "packer-plugin-lxc", "builder/lxc/config.hcl2spec.go", "builder", "lxc"),

        // ── CloudStack / ncloud / Yandex / Tencent / HyperOne / Triton ──
        E("hashicorp", "packer-plugin-cloudstack", "builder/cloudstack/config.hcl2spec.go", "builder", "cloudstack"),
        E("hashicorp", "packer-plugin-ncloud", "builder/ncloud/config.hcl2spec.go", "builder", "ncloud"),
        E("hashicorp", "packer-plugin-yandex", "builder/yandex/config.hcl2spec.go", "builder", "yandex"),
        E("hashicorp", "packer-plugin-yandex", "post-processor/yandex-export/post-processor.hcl2spec.go", "post-processor", "yandex-export"),
        E("hashicorp", "packer-plugin-yandex", "post-processor/yandex-import/post-processor.hcl2spec.go", "post-processor", "yandex-import"),
        E("hashicorp", "packer-plugin-tencentcloud", "builder/tencentcloud/cvm/builder.hcl2spec.go", "builder", "tencentcloud-cvm"),
        E("hashicorp", "packer-plugin-hyperone", "builder/hyperone/config.hcl2spec.go", "builder", "hyperone"),
        E("hashicorp", "packer-plugin-triton", "builder/triton/config.hcl2spec.go", "builder", "triton"),

        // ── VMware (vmware org) ──
        E("vmware", "packer-plugin-vmware", "builder/vmware/iso/config.hcl2spec.go", "builder", "vmware-iso"),
        E("vmware", "packer-plugin-vmware", "builder/vmware/vmx/config.hcl2spec.go", "builder", "vmware-vmx"),

        // ── vSphere (hashicorp org) ──
        E("hashicorp", "packer-plugin-vsphere", "builder/vsphere/iso/config.hcl2spec.go", "builder", "vsphere-iso"),
        E("hashicorp", "packer-plugin-vsphere", "builder/vsphere/clone/config.hcl2spec.go", "builder", "vsphere-clone"),
        E("hashicorp", "packer-plugin-vsphere", "builder/vsphere/supervisor/config.hcl2spec.go", "builder", "vsphere-supervisor"),
        E("hashicorp", "packer-plugin-vsphere", "datasource/virtualmachine/data.hcl2spec.go", "datasource", "vsphere-virtualmachine"),
        E("hashicorp", "packer-plugin-vsphere", "post-processor/vsphere-template/post-processor.hcl2spec.go", "post-processor", "vsphere-template"),
        E("hashicorp", "packer-plugin-vsphere", "post-processor/vsphere/post-processor.hcl2spec.go", "post-processor", "vsphere"),

        // ── Parallels (Parallels org) ──
        E("Parallels", "packer-plugin-parallels", "builder/parallels/iso/builder.hcl2spec.go", "builder", "parallels-iso"),
        E("Parallels", "packer-plugin-parallels", "builder/parallels/pvm/config.hcl2spec.go", "builder", "parallels-pvm"),
        E("Parallels", "packer-plugin-parallels", "builder/parallels/ipsw/builder.hcl2spec.go", "builder", "parallels-ipsw"),
        E("Parallels", "packer-plugin-parallels", "builder/parallels/macvm/config.hcl2spec.go", "builder", "parallels-macvm"),

        // ── Built-in (hashicorp/packer main repo) ──
        E("hashicorp", "packer", "builder/file/config.hcl2spec.go", "builder", "file"),
        E("hashicorp", "packer", "builder/null/config.hcl2spec.go", "builder", "null"),
        E("hashicorp", "packer", "provisioner/shell/provisioner.hcl2spec.go", "provisioner", "shell"),
        E("hashicorp", "packer", "provisioner/file/provisioner.hcl2spec.go", "provisioner", "file"),
        E("hashicorp", "packer", "provisioner/powershell/provisioner.hcl2spec.go", "provisioner", "powershell"),
        E("hashicorp", "packer", "provisioner/windows-shell/provisioner.hcl2spec.go", "provisioner", "windows-shell"),
        E("hashicorp", "packer", "provisioner/windows-restart/provisioner.hcl2spec.go", "provisioner", "windows-restart"),
        E("hashicorp", "packer", "provisioner/breakpoint/provisioner.hcl2spec.go", "provisioner", "breakpoint"),
        E("hashicorp", "packer", "provisioner/sleep/provisioner.hcl2spec.go", "provisioner", "sleep"),
        E("hashicorp", "packer", "provisioner/hcp-sbom/provisioner.hcl2spec.go", "provisioner", "hcp-sbom"),
        E("hashicorp", "packer", "post-processor/manifest/post-processor.hcl2spec.go", "post-processor", "manifest"),
        E("hashicorp", "packer", "post-processor/compress/post-processor.hcl2spec.go", "post-processor", "compress"),
        E("hashicorp", "packer", "post-processor/checksum/post-processor.hcl2spec.go", "post-processor", "checksum"),
        E("hashicorp", "packer", "post-processor/artifice/post-processor.hcl2spec.go", "post-processor", "artifice"),
        E("hashicorp", "packer", "datasource/http/data.hcl2spec.go", "datasource", "http"),
        E("hashicorp", "packer", "datasource/null/data.hcl2spec.go", "datasource", "null"),
        E("hashicorp", "packer", "datasource/hcp-packer-version/data.hcl2spec.go", "datasource", "hcp-packer-version"),
        E("hashicorp", "packer", "datasource/hcp-packer-artifact/data.hcl2spec.go", "datasource", "hcp-packer-artifact"),
    };

    private static PluginRegistryEntry E(string org, string repo, string path, string kind, string typeName)
        => new(org, repo, path, kind, typeName);
}
