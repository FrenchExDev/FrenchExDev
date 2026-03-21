namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// Static factories for VBoxManage command arrays used in the <c>vboxmanage</c> source field.
/// </summary>
public static class VBoxManageCommand
{
    public static List<string> ModifyVm(string key, string value)
        => new() { "modifyvm", "{{ .Name }}", $"--{key}", value };

    public static List<string> SetExtraData(string key, string value)
        => new() { "setextradata", "{{ .Name }}", key, value };

    public static List<string> SetProperty(string key, string value)
        => new() { "setproperty", key, value };

    public static List<string> StorageCtl(string name, string key, string value)
        => new() { "storagectl", "{{ .Name }}", $"--name={name}", $"--{key}", value };

    public static List<string> StorageAttach(string storageCtl, string port, string key, string value)
        => new() { "storageattach", "{{ .Name }}", $"--storagectl={storageCtl}", $"--port={port}", $"--{key}", value };
}
