namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// Strongly-typed VirtualBox VBoxManage configuration.
/// Call <see cref="ToCommands"/> to produce the <c>vboxmanage</c> list-of-lists for the source block.
/// </summary>
public sealed class VBoxManageConfig
{
    public int? Cpus { get; set; }
    public int? Memory { get; set; }
    public int? VideoMemory { get; set; }
    public string? Chipset { get; set; }
    public string? ParavirtProvider { get; set; }
    public string? OsType { get; set; }
    public string? GraphicsController { get; set; }

    // Flags
    public bool? IoApic { get; set; }
    public bool? HwVirtEx { get; set; }
    public bool? NestedHwVirt { get; set; }
    public bool? NestedPaging { get; set; }
    public bool? LargePages { get; set; }
    public bool? VtxUx { get; set; }
    public bool? VtxVPid { get; set; }
    public bool? Pae { get; set; }
    public bool? Acpi { get; set; }
    public bool? HPet { get; set; }
    public bool? PageFusion { get; set; }
    public bool? Vrde { get; set; }
    public bool? Usb { get; set; }
    public bool? HwVirtExclusive { get; set; }

    // NAT
    public bool? DnsProxy { get; set; }
    public bool? LocalhostReachable { get; set; }
    public bool? DnsHostResolver { get; set; }

    // SATA
    public string? StorageControllerName { get; set; }
    public bool? HostIoCache { get; set; }
    public bool? NonRotational { get; set; }
    public bool? Discard { get; set; }

    /// <summary>Converts this config into a list of VBoxManage command arrays.</summary>
    public List<List<string>> ToCommands()
    {
        var commands = new List<List<string>>();

        AddModifyVm(commands, "memory", Memory);
        AddModifyVm(commands, "cpus", Cpus);
        AddModifyVm(commands, "vram", VideoMemory);
        AddModifyVm(commands, "chipset", Chipset);
        AddModifyVm(commands, "paravirtprovider", ParavirtProvider);
        AddModifyVm(commands, "ostype", OsType);
        AddModifyVm(commands, "graphicscontroller", GraphicsController);

        AddModifyVmBool(commands, "ioapic", IoApic);
        AddModifyVmBool(commands, "hwvirtex", HwVirtEx);
        AddModifyVmBool(commands, "nested-hw-virt", NestedHwVirt);
        AddModifyVmBool(commands, "nestedpaging", NestedPaging);
        AddModifyVmBool(commands, "largepages", LargePages);
        AddModifyVmBool(commands, "vtxux", VtxUx);
        AddModifyVmBool(commands, "vtxvpid", VtxVPid);
        AddModifyVmBool(commands, "pae", Pae);
        AddModifyVmBool(commands, "acpi", Acpi);
        AddModifyVmBool(commands, "hpet", HPet);
        AddModifyVmBool(commands, "pagefusion", PageFusion);
        AddModifyVmBool(commands, "vrde", Vrde);
        AddModifyVmBool(commands, "usb", Usb);

        AddModifyVmBool(commands, "natdnsproxy1", DnsProxy);
        AddModifyVmBool(commands, "natlocalhostreach1", LocalhostReachable);
        AddModifyVmBool(commands, "natdnshostresolver1", DnsHostResolver);

        if (HwVirtExclusive is not null)
            commands.Add(VBoxManageCommand.SetProperty("hwvirtexcl", HwVirtExclusive.Value ? "on" : "off"));

        var ctlName = StorageControllerName ?? "SATA Controller";
        if (HostIoCache is not null)
            commands.Add(VBoxManageCommand.StorageCtl(ctlName, "hostiocache", HostIoCache.Value ? "on" : "off"));
        if (NonRotational is not null)
            commands.Add(VBoxManageCommand.StorageAttach(ctlName, "0", "nonrotational", NonRotational.Value ? "on" : "off"));
        if (Discard is not null && NonRotational is true)
        {
            commands.Add(VBoxManageCommand.SetExtraData(
                $"VBoxInternal/Devices/ahci/0/Config/Port0/NonRotational",
                Discard.Value ? "1" : "0"));
        }

        return commands;
    }

    private static void AddModifyVm(List<List<string>> commands, string key, int? value)
    {
        if (value is not null)
            commands.Add(VBoxManageCommand.ModifyVm(key, value.Value.ToString()));
    }

    private static void AddModifyVm(List<List<string>> commands, string key, string? value)
    {
        if (value is not null)
            commands.Add(VBoxManageCommand.ModifyVm(key, value));
    }

    private static void AddModifyVmBool(List<List<string>> commands, string key, bool? value)
    {
        if (value is not null)
            commands.Add(VBoxManageCommand.ModifyVm(key, value.Value ? "on" : "off"));
    }
}
