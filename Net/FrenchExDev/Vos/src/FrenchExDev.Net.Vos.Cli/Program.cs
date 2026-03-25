using System.CommandLine;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;
using FrenchExDev.Net.Vos.Infra.Vagrant;
using FrenchExDev.Net.Vos.Infra.FileSystem;

// ── Shared symbols ──────────────────────────────────────────────────
var nameArg = new Argument<string>("name") { Description = "VM instance name" };
var forceOption = new Option<bool>("--force") { Description = "Force the operation" };
var configOption = new Option<string>("--config") { Description = "Path to config file", DefaultValueFactory = _ => "config-vos.yaml" };

var rootCommand = new RootCommand("vos - Vagrant superset with YAML-driven multi-machine management");

// ═══════════════════════════════════════════════════════════════════
// VAGRANT PROXY COMMANDS
// ═══════════════════════════════════════════════════════════════════

void VagrantCmd(string name, string desc, Func<IVosBackend, ResolvedInstance, CancellationToken, Task<VosActionResult>> action)
{
    var cmd = new Command(name, desc);
    cmd.Arguments.Add(nameArg);
    cmd.Options.Add(configOption);
    cmd.SetAction(async (pr, ct) =>
    {
        var orch = await CreateOrchestrator(pr.GetValue(configOption)!);
        if (orch is null) return;
        var results = await orch.ExecuteAsync(pr.GetValue(nameArg), false, action, ct);
        foreach (var (n, r) in results) PrintResult(n, r);
    });
    rootCommand.Subcommands.Add(cmd);
}

// Core VM lifecycle
VagrantCmd("up", "Start VM(s)", (b, i, ct) => b.UpAsync(i, ct));
VagrantCmd("reload", "Restart VM(s)", (b, i, ct) => b.ReloadAsync(i, ct));
VagrantCmd("provision", "Run provisioners", (b, i, ct) => b.ProvisionAsync(i, ct));
VagrantCmd("suspend", "Suspend VM(s)", (b, i, ct) => b.SuspendAsync(i, ct));
VagrantCmd("resume", "Resume VM(s)", (b, i, ct) => b.ResumeAsync(i, ct));
VagrantCmd("ssh-config", "Show SSH config", (b, i, ct) => b.SshConfigAsync(i, ct));
VagrantCmd("port", "Show port mappings", (b, i, ct) => b.PortAsync(i, ct));
VagrantCmd("package", "Package VM as box", (b, i, ct) => b.PackageAsync(i, ct));
VagrantCmd("rdp", "RDP into Windows VM", (b, i, ct) => b.RdpAsync(i, ct));
VagrantCmd("powershell", "PowerShell remoting", (b, i, ct) => b.PowershellAsync(i, ct));
VagrantCmd("winrm", "WinRM connection", (b, i, ct) => b.WinrmAsync(i, ct));
VagrantCmd("winrm-config", "WinRM config", (b, i, ct) => b.WinrmConfigAsync(i, ct));

// halt (with --force)
var haltCmd = new Command("halt", "Stop VM(s)");
haltCmd.Arguments.Add(nameArg);
haltCmd.Options.Add(forceOption);
haltCmd.Options.Add(configOption);
haltCmd.SetAction(async (pr, ct) =>
{
    var orch = await CreateOrchestrator(pr.GetValue(configOption)!);
    if (orch is null) return;
    var force = pr.GetValue(forceOption);
    var results = await orch.ExecuteAsync(pr.GetValue(nameArg), false, (b, i, c) => b.HaltAsync(i, force, c), ct);
    foreach (var (n, r) in results) PrintResult(n, r);
});
rootCommand.Subcommands.Add(haltCmd);

// destroy (with --force)
var destroyCmd = new Command("destroy", "Remove VM(s)");
destroyCmd.Arguments.Add(nameArg);
destroyCmd.Options.Add(forceOption);
destroyCmd.Options.Add(configOption);
destroyCmd.SetAction(async (pr, ct) =>
{
    var orch = await CreateOrchestrator(pr.GetValue(configOption)!);
    if (orch is null) return;
    var force = pr.GetValue(forceOption);
    var results = await orch.ExecuteAsync(pr.GetValue(nameArg), false, (b, i, c) => b.DestroyAsync(i, force, c), ct);
    foreach (var (n, r) in results) PrintResult(n, r);
});
rootCommand.Subcommands.Add(destroyCmd);

// status (optional name)
var statusCmd = new Command("status", "Show VM status");
statusCmd.Options.Add(configOption);
statusCmd.SetAction(async (pr, ct) =>
{
    var orch = await CreateOrchestrator(pr.GetValue(configOption)!);
    if (orch is null) return;
    var results = await orch.ExecuteAsync(null, true, (b, i, c) => b.StatusAsync(i, c), ct);
    foreach (var (n, r) in results) PrintResult(n, r);
});
rootCommand.Subcommands.Add(statusCmd);

// ssh (interactive)
var sshCmd = new Command("ssh", "SSH into VM");
sshCmd.Arguments.Add(nameArg);
sshCmd.Options.Add(configOption);
sshCmd.SetAction(async (pr, ct) =>
{
    var orch = await CreateOrchestrator(pr.GetValue(configOption)!);
    if (orch is null) return;
    var targets = orch.ResolveTargets(pr.GetValue(nameArg), false);
    if (targets.Count > 0) await new VagrantBackend().SshAsync(targets[0], ct);
});
rootCommand.Subcommands.Add(sshCmd);

// ssh-command
var sshCmdArg = new Argument<string>("command") { Description = "Command to execute" };
var sshCommandCmd = new Command("ssh-command", "Execute SSH command");
sshCommandCmd.Arguments.Add(nameArg);
sshCommandCmd.Arguments.Add(sshCmdArg);
sshCommandCmd.Options.Add(configOption);
sshCommandCmd.SetAction(async (pr, ct) =>
{
    var orch = await CreateOrchestrator(pr.GetValue(configOption)!);
    if (orch is null) return;
    var targets = orch.ResolveTargets(pr.GetValue(nameArg), false);
    if (targets.Count > 0)
    {
        var result = await new VagrantBackend().SshCommandAsync(targets[0], pr.GetValue(sshCmdArg)!, ct);
        Console.WriteLine(result.Output);
    }
});
rootCommand.Subcommands.Add(sshCommandCmd);

// upload
var uploadSrcArg = new Argument<string>("source") { Description = "Source file/directory" };
var uploadDstArg = new Argument<string>("destination") { Description = "Destination path on VM" };
var uploadCmd = new Command("upload", "Upload files to VM");
uploadCmd.Arguments.Add(nameArg);
uploadCmd.Arguments.Add(uploadSrcArg);
uploadCmd.Arguments.Add(uploadDstArg);
uploadCmd.Options.Add(configOption);
uploadCmd.SetAction(async (pr, ct) =>
{
    var orch = await CreateOrchestrator(pr.GetValue(configOption)!);
    if (orch is null) return;
    var targets = orch.ResolveTargets(pr.GetValue(nameArg), false);
    if (targets.Count > 0)
        PrintResult(targets[0].Name, await new VagrantBackend().UploadAsync(targets[0], pr.GetValue(uploadSrcArg)!, pr.GetValue(uploadDstArg)!, ct));
});
rootCommand.Subcommands.Add(uploadCmd);

// global-status (no instance name)
var globalStatusCmd = new Command("global-status", "Show status of all Vagrant VMs");
globalStatusCmd.SetAction(async (pr, ct) =>
{
    var result = await new VagrantBackend().GlobalStatusAsync(ct);
    Console.WriteLine(result.Output);
});
rootCommand.Subcommands.Add(globalStatusCmd);

// validate
var vagrantValidateCmd = new Command("vagrant-validate", "Validate Vagrantfile");
vagrantValidateCmd.SetAction(async (pr, ct) =>
{
    var result = await new VagrantBackend().ValidateAsync(ct);
    Console.WriteLine(result.Output);
});
rootCommand.Subcommands.Add(vagrantValidateCmd);

// ── snapshot ────────────────────────────────────────────────────────
var snapshotCmd = new Command("snapshot", "Manage VM snapshots");
var snapNameArg = new Argument<string>("snapshot-name") { Description = "Snapshot name" };

void SnapCmd(string name, string desc, Func<IVosBackend, ResolvedInstance, string, CancellationToken, Task<VosActionResult>> action)
{
    var cmd = new Command(name, desc);
    cmd.Arguments.Add(nameArg);
    cmd.Arguments.Add(snapNameArg);
    cmd.Options.Add(configOption);
    cmd.SetAction(async (pr, ct) =>
    {
        var orch = await CreateOrchestrator(pr.GetValue(configOption)!);
        if (orch is null) return;
        var snap = pr.GetValue(snapNameArg)!;
        var results = await orch.ExecuteAsync(pr.GetValue(nameArg), false, (b, i, c) => action(b, i, snap, c), ct);
        foreach (var (n, r) in results) PrintResult(n, r);
    });
    snapshotCmd.Subcommands.Add(cmd);
}

SnapCmd("save", "Save snapshot", (b, i, s, ct) => b.SnapshotSaveAsync(i, s, ct));
SnapCmd("restore", "Restore snapshot", (b, i, s, ct) => b.SnapshotRestoreAsync(i, s, ct));
SnapCmd("delete", "Delete snapshot", (b, i, s, ct) => b.SnapshotDeleteAsync(i, s, ct));

// snapshot list/push/pop (no snapshot name)
void SnapSimpleCmd(string name, string desc, Func<IVosBackend, ResolvedInstance, CancellationToken, Task<VosActionResult>> action)
{
    var cmd = new Command(name, desc);
    cmd.Arguments.Add(nameArg);
    cmd.Options.Add(configOption);
    cmd.SetAction(async (pr, ct) =>
    {
        var orch = await CreateOrchestrator(pr.GetValue(configOption)!);
        if (orch is null) return;
        var results = await orch.ExecuteAsync(pr.GetValue(nameArg), false, action, ct);
        foreach (var (n, r) in results) PrintResult(n, r);
    });
    snapshotCmd.Subcommands.Add(cmd);
}

SnapSimpleCmd("list", "List snapshots", (b, i, ct) => b.SnapshotListAsync(i, ct));
SnapSimpleCmd("push", "Push snapshot", (b, i, ct) => b.SnapshotPushAsync(i, ct));
SnapSimpleCmd("pop", "Pop snapshot", (b, i, ct) => b.SnapshotPopAsync(i, ct));
rootCommand.Subcommands.Add(snapshotCmd);

// ═══════════════════════════════════════════════════════════════════
// VOS-SPECIFIC COMMANDS
// ═══════════════════════════════════════════════════════════════════

// ── init ────────────────────────────────────────────────────────────
var initCmd = new Command("init", "Initialize a Vos project (creates config-vos.yaml + Vagrantfile)");
initCmd.SetAction((parseResult) =>
{
    var cfg = new VosConfig
    {
        MachineTypes = new()
        {
            ["default"] = new VosMachineType
            {
                Box = "ubuntu/jammy64",
                Provider = new VosProviderConfig { Memory = 2048, Cpus = 2 }
            }
        },
        Machines = new()
        {
            ["default"] = new VosMachine
            {
                MachineTypeName = "default",
                Instances = new() { new VosInstance { Name = "default-01" } }
            }
        }
    };
    new VosConfigSerializer().SerializeAsync(cfg, "config-vos.yaml").GetAwaiter().GetResult();
    Console.WriteLine("Created: config-vos.yaml");

    var vagrantfile = VagrantfileRenderer.Render(cfg);
    File.WriteAllText("Vagrantfile", vagrantfile);
    Console.WriteLine("Created: Vagrantfile");
});
rootCommand.Subcommands.Add(initCmd);

// ── config ──────────────────────────────────────────────────────────
var configCmd = new Command("config", "Configuration management");

var configShowCmd = new Command("show", "Show resolved configuration");
configShowCmd.Options.Add(configOption);
configShowCmd.SetAction(async (pr, ct) =>
{
    var config = await LoadConfig(pr.GetValue(configOption)!);
    if (config is null) return;
    var all = VosConfigMerger.ResolveAll(config);
    Console.WriteLine($"Backend: {config.Backend}");
    Console.WriteLine($"Machine types: {config.MachineTypes.Count}");
    Console.WriteLine($"Instances: {all.Count}");
    foreach (var (machineName, inst) in all)
        Console.WriteLine($"  {inst.Name} ({machineName}) — {inst.ProviderType}, {inst.Memory}MB, {inst.Cpus} CPUs, box={inst.Box}, ip={inst.Ip ?? "none"}");
});
configCmd.Subcommands.Add(configShowCmd);
rootCommand.Subcommands.Add(configCmd);

// ── validate ────────────────────────────────────────────────────────
var validateCmd = new Command("validate", "Validate Vos configuration");
validateCmd.Options.Add(configOption);
validateCmd.SetAction(async (pr, ct) =>
{
    var config = await LoadConfig(pr.GetValue(configOption)!);
    if (config is null) return;
    var errors = VosConfigValidator.Validate(config);
    if (errors.Count > 0)
    {
        Console.Error.WriteLine($"Configuration has {errors.Count} error(s):");
        foreach (var err in errors) Console.Error.WriteLine($"  - {err}");
        return;
    }
    var all = VosConfigMerger.ResolveAll(config);
    Console.WriteLine($"Configuration valid: {all.Count} instances resolved.");
});
rootCommand.Subcommands.Add(validateCmd);

// ── type ────────────────────────────────────────────────────────────
var typeCmd = new Command("type", "Machine type management");
var typeNameArg = new Argument<string>("name") { Description = "Machine type name" };
var boxOption = new Option<string>("--box") { Description = "Vagrant box name" };
var memoryOption = new Option<int?>("--memory") { Description = "Memory in MB" };
var cpusOption = new Option<int?>("--cpus") { Description = "Number of CPUs" };
var videoMemOption = new Option<int?>("--video-memory") { Description = "Video memory in MB" };
var nestedVirtOption = new Option<bool>("--nested-virt") { Description = "Enable nested virtualization" };
var sataSsdOption = new Option<bool>("--sata-ssd") { Description = "Enable SATA SSD (host I/O cache + non-rotational)" };
var guiOption = new Option<bool>("--gui") { Description = "Enable GUI" };
var nicPromiscOption = new Option<string?>("--nic-promisc") { Description = "NIC promiscuous mode (e.g. allow-all)" };
var noLinkedClonesOption = new Option<bool>("--no-linked-clones") { Description = "Disable linked clones" };

var typeAddCmd = new Command("add", "Add a machine type");
typeAddCmd.Arguments.Add(typeNameArg);
typeAddCmd.Options.Add(boxOption);
typeAddCmd.Options.Add(memoryOption);
typeAddCmd.Options.Add(cpusOption);
typeAddCmd.Options.Add(configOption);
typeAddCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    var box = pr.GetValue(boxOption);
    if (string.IsNullOrEmpty(box)) { Console.Error.WriteLine("--box is required"); return; }
    mgr.AddMachineType(pr.GetValue(typeNameArg)!, box, pr.GetValue(memoryOption) ?? 2048, pr.GetValue(cpusOption) ?? 2);
    await Save(mgr.Config, path!);
    Console.WriteLine($"Added machine type '{pr.GetValue(typeNameArg)}'");
});
typeCmd.Subcommands.Add(typeAddCmd);

var typeListCmd = new Command("list", "List machine types");
typeListCmd.Options.Add(configOption);
typeListCmd.SetAction(async (pr, ct) =>
{
    var config = await LoadConfig(pr.GetValue(configOption)!);
    if (config is null) return;
    foreach (var (name, mt) in config.MachineTypes)
        Console.WriteLine($"  {name}: box={mt.Box}, memory={mt.Provider?.Memory}, cpus={mt.Provider?.Cpus}, enabled={mt.IsEnabled}");
});
typeCmd.Subcommands.Add(typeListCmd);

var typeShowCmd = new Command("show", "Show machine type details");
typeShowCmd.Arguments.Add(typeNameArg);
typeShowCmd.Options.Add(configOption);
typeShowCmd.SetAction(async (pr, ct) =>
{
    var config = await LoadConfig(pr.GetValue(configOption)!);
    if (config is null) return;
    var name = pr.GetValue(typeNameArg)!;
    if (!config.MachineTypes.TryGetValue(name, out var mt)) { Console.Error.WriteLine($"Not found: {name}"); return; }
    Console.WriteLine($"Machine type: {name}");
    Console.WriteLine($"  Box: {mt.Box}");
    Console.WriteLine($"  Memory: {mt.Provider?.Memory}MB, CPUs: {mt.Provider?.Cpus}, Video: {mt.Provider?.VideoMemory}MB");
    Console.WriteLine($"  GUI: {mt.Provider?.Gui}, Linked clones: {mt.Provider?.LinkedClones}");
    Console.WriteLine($"  Plugins: {string.Join(", ", mt.Plugins)}");
    Console.WriteLine($"  Shared folders: {mt.SharedFolders.Count}");
    Console.WriteLine($"  Provisioning: {mt.Provisioning.Count} steps");
    Console.WriteLine($"  VBoxManage: {mt.Provider?.VboxManage.Count ?? 0} commands");
});
typeCmd.Subcommands.Add(typeShowCmd);

var typeRemoveCmd = new Command("remove", "Remove machine type");
typeRemoveCmd.Arguments.Add(typeNameArg);
typeRemoveCmd.Options.Add(configOption);
typeRemoveCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    mgr.RemoveMachineType(pr.GetValue(typeNameArg)!);
    await Save(mgr.Config, path!);
    Console.WriteLine($"Removed machine type '{pr.GetValue(typeNameArg)}'");
});
typeCmd.Subcommands.Add(typeRemoveCmd);

var typeSetCmd = new Command("set", "Set machine type properties");
typeSetCmd.Arguments.Add(typeNameArg);
typeSetCmd.Options.Add(memoryOption);
typeSetCmd.Options.Add(cpusOption);
typeSetCmd.Options.Add(videoMemOption);
typeSetCmd.Options.Add(nestedVirtOption);
typeSetCmd.Options.Add(sataSsdOption);
typeSetCmd.Options.Add(guiOption);
typeSetCmd.Options.Add(nicPromiscOption);
typeSetCmd.Options.Add(noLinkedClonesOption);
typeSetCmd.Options.Add(configOption);
typeSetCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    var name = pr.GetValue(typeNameArg)!;
    mgr.SetMachineTypeProperty(name, mt =>
    {
        mt.Provider ??= new VosProviderConfig();
        if (pr.GetValue(memoryOption) is { } mem) mt.Provider.Memory = mem;
        if (pr.GetValue(cpusOption) is { } cpu) mt.Provider.Cpus = cpu;
        if (pr.GetValue(videoMemOption) is { } vid) mt.Provider.VideoMemory = vid;
        if (pr.GetValue(guiOption)) mt.Provider.Gui = true;
        if (pr.GetValue(noLinkedClonesOption)) mt.Provider.LinkedClones = false;
        if (pr.GetValue(nestedVirtOption))
        {
            mt.Provider.VboxManage.Add(new() { "modifyvm", "{{ .Name }}", "--nested-hw-virt", "on" });
            mt.Provider.VboxManage.Add(new() { "modifyvm", "{{ .Name }}", "--nestedpaging", "on" });
        }
        if (pr.GetValue(sataSsdOption))
        {
            mt.Provider.VboxManage.Add(new() { "storagectl", "{{ .Name }}", "--name=SATA Controller", "--hostiocache", "on" });
            mt.Provider.VboxManage.Add(new() { "storageattach", "{{ .Name }}", "--storagectl=SATA Controller", "--port=0", "--nonrotational", "on" });
        }
        if (pr.GetValue(nicPromiscOption) is { } promisc)
            mt.Provider.VboxManage.Add(new() { "modifyvm", "{{ .Name }}", "--nicpromisc2", promisc });
    });
    await Save(mgr.Config, path!);
    Console.WriteLine($"Updated machine type '{name}'");
});
typeCmd.Subcommands.Add(typeSetCmd);

// type vboxmanage
var typeVboxCmd = new Command("vboxmanage", "Manage raw VBoxManage commands");
var vboxArgsArg = new Argument<string[]>("args") { Description = "VBoxManage command arguments", Arity = ArgumentArity.OneOrMore };

var vboxAddCmd = new Command("add", "Add a VBoxManage command");
vboxAddCmd.Arguments.Add(typeNameArg);
vboxAddCmd.Arguments.Add(vboxArgsArg);
vboxAddCmd.Options.Add(configOption);
vboxAddCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    mgr.AddVboxManageCommand(pr.GetValue(typeNameArg)!, pr.GetValue(vboxArgsArg)!.ToList());
    await Save(mgr.Config, path!);
    Console.WriteLine("Added VBoxManage command");
});
typeVboxCmd.Subcommands.Add(vboxAddCmd);

var vboxListCmd = new Command("list", "List VBoxManage commands");
vboxListCmd.Arguments.Add(typeNameArg);
vboxListCmd.Options.Add(configOption);
vboxListCmd.SetAction(async (pr, ct) =>
{
    var config = await LoadConfig(pr.GetValue(configOption)!);
    if (config is null) return;
    var mgr = new VosConfigManager(config);
    var cmds = mgr.ListVboxManageCommands(pr.GetValue(typeNameArg)!);
    for (var i = 0; i < cmds.Count; i++)
        Console.WriteLine($"  [{i}] {string.Join(" ", cmds[i])}");
});
typeVboxCmd.Subcommands.Add(vboxListCmd);

var vboxRemoveIdx = new Argument<int>("index") { Description = "Command index to remove" };
var vboxRemoveCmd = new Command("remove", "Remove VBoxManage command by index");
vboxRemoveCmd.Arguments.Add(typeNameArg);
vboxRemoveCmd.Arguments.Add(vboxRemoveIdx);
vboxRemoveCmd.Options.Add(configOption);
vboxRemoveCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    mgr.RemoveVboxManageCommand(pr.GetValue(typeNameArg)!, pr.GetValue(vboxRemoveIdx));
    await Save(mgr.Config, path!);
    Console.WriteLine("Removed VBoxManage command");
});
typeVboxCmd.Subcommands.Add(vboxRemoveCmd);

var vboxClearCmd = new Command("clear", "Clear all VBoxManage commands");
vboxClearCmd.Arguments.Add(typeNameArg);
vboxClearCmd.Options.Add(configOption);
vboxClearCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    mgr.ClearVboxManageCommands(pr.GetValue(typeNameArg)!);
    await Save(mgr.Config, path!);
    Console.WriteLine("Cleared all VBoxManage commands");
});
typeVboxCmd.Subcommands.Add(vboxClearCmd);

typeCmd.Subcommands.Add(typeVboxCmd);
rootCommand.Subcommands.Add(typeCmd);

// ── machine ─────────────────────────────────────────────────────────
var machineCmd = new Command("machine", "Machine management");
var machineNameArg = new Argument<string>("name") { Description = "Machine name" };
var typeOption = new Option<string>("--type") { Description = "Machine type name" };
var instancesOption = new Option<int>("--instances") { Description = "Number of instances", DefaultValueFactory = _ => 1 };

var machineAddCmd = new Command("add", "Add a machine");
machineAddCmd.Arguments.Add(machineNameArg);
machineAddCmd.Options.Add(typeOption);
machineAddCmd.Options.Add(instancesOption);
machineAddCmd.Options.Add(configOption);
machineAddCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    var t = pr.GetValue(typeOption);
    if (string.IsNullOrEmpty(t)) { Console.Error.WriteLine("--type is required"); return; }
    mgr.AddMachine(pr.GetValue(machineNameArg)!, t, pr.GetValue(instancesOption));
    await Save(mgr.Config, path!);
    Console.WriteLine($"Added machine '{pr.GetValue(machineNameArg)}' with {pr.GetValue(instancesOption)} instance(s)");
});
machineCmd.Subcommands.Add(machineAddCmd);

var machineListCmd = new Command("list", "List machines");
machineListCmd.Options.Add(configOption);
machineListCmd.SetAction(async (pr, ct) =>
{
    var config = await LoadConfig(pr.GetValue(configOption)!);
    if (config is null) return;
    foreach (var (name, m) in config.Machines)
    {
        Console.WriteLine($"  {name} (type={m.MachineTypeName}, enabled={m.IsEnabled}):");
        foreach (var inst in m.Instances)
            Console.WriteLine($"    {inst.Name} ip={inst.Ip ?? "none"} hostname={inst.Hostname ?? inst.Name}");
    }
});
machineCmd.Subcommands.Add(machineListCmd);

var machineRemoveCmd = new Command("remove", "Remove machine");
machineRemoveCmd.Arguments.Add(machineNameArg);
machineRemoveCmd.Options.Add(configOption);
machineRemoveCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    mgr.RemoveMachine(pr.GetValue(machineNameArg)!);
    await Save(mgr.Config, path!);
    Console.WriteLine($"Removed machine '{pr.GetValue(machineNameArg)}'");
});
machineCmd.Subcommands.Add(machineRemoveCmd);

var machineEnableCmd = new Command("enable", "Enable machine");
machineEnableCmd.Arguments.Add(machineNameArg);
machineEnableCmd.Options.Add(configOption);
machineEnableCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    mgr.EnableMachine(pr.GetValue(machineNameArg)!);
    await Save(mgr.Config, path!);
});
machineCmd.Subcommands.Add(machineEnableCmd);

var machineDisableCmd = new Command("disable", "Disable machine");
machineDisableCmd.Arguments.Add(machineNameArg);
machineDisableCmd.Options.Add(configOption);
machineDisableCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    mgr.DisableMachine(pr.GetValue(machineNameArg)!);
    await Save(mgr.Config, path!);
});
machineCmd.Subcommands.Add(machineDisableCmd);

rootCommand.Subcommands.Add(machineCmd);

// ── instance ────────────────────────────────────────────────────────
var instanceCmd = new Command("instance", "Instance management");
var instMachineArg = new Argument<string>("machine") { Description = "Machine name" };
var instNameArg = new Argument<string>("instance-name") { Description = "Instance name" };
var ipOption = new Option<string?>("--ip") { Description = "IP address" };

var instanceAddCmd = new Command("add", "Add instance");
instanceAddCmd.Arguments.Add(instMachineArg);
instanceAddCmd.Arguments.Add(instNameArg);
instanceAddCmd.Options.Add(ipOption);
instanceAddCmd.Options.Add(memoryOption);
instanceAddCmd.Options.Add(cpusOption);
instanceAddCmd.Options.Add(configOption);
instanceAddCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    mgr.AddInstance(pr.GetValue(instMachineArg)!, pr.GetValue(instNameArg)!, pr.GetValue(ipOption), pr.GetValue(memoryOption), pr.GetValue(cpusOption));
    await Save(mgr.Config, path!);
    Console.WriteLine($"Added instance '{pr.GetValue(instNameArg)}'");
});
instanceCmd.Subcommands.Add(instanceAddCmd);

var instanceRemoveCmd = new Command("remove", "Remove instance");
instanceRemoveCmd.Arguments.Add(instMachineArg);
instanceRemoveCmd.Arguments.Add(instNameArg);
instanceRemoveCmd.Options.Add(configOption);
instanceRemoveCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    mgr.RemoveInstance(pr.GetValue(instMachineArg)!, pr.GetValue(instNameArg)!);
    await Save(mgr.Config, path!);
    Console.WriteLine($"Removed instance '{pr.GetValue(instNameArg)}'");
});
instanceCmd.Subcommands.Add(instanceRemoveCmd);

var instanceListCmd = new Command("list", "List instances");
instanceListCmd.Options.Add(configOption);
instanceListCmd.SetAction(async (pr, ct) =>
{
    var config = await LoadConfig(pr.GetValue(configOption)!);
    if (config is null) return;
    foreach (var (name, m) in config.Machines)
        foreach (var inst in m.Instances)
            Console.WriteLine($"  {inst.Name} (machine={name}) ip={inst.Ip ?? "none"} hostname={inst.Hostname ?? inst.Name}");
});
instanceCmd.Subcommands.Add(instanceListCmd);

rootCommand.Subcommands.Add(instanceCmd);

// ── network ─────────────────────────────────────────────────────────
var networkCmd = new Command("network", "Network management");
var subnetOption = new Option<string>("--subnet") { Description = "Subnet (e.g. 192.168.56.0/24)", DefaultValueFactory = _ => "192.168.56.0/24" };

var startAtOption = new Option<int>("--start-at") { Description = "First host number to assign (default: 10)", DefaultValueFactory = _ => 10 };

var networkGenerateCmd = new Command("generate", "Auto-assign IPs to all instances (skips conflicts)");
networkGenerateCmd.Options.Add(subnetOption);
networkGenerateCmd.Options.Add(startAtOption);
networkGenerateCmd.Options.Add(configOption);
networkGenerateCmd.SetAction(async (pr, ct) =>
{
    var (mgr, path) = await LoadManager(pr.GetValue(configOption)!);
    if (mgr is null) return;
    var assigned = NetworkGenerator.Generate(mgr.Config, pr.GetValue(subnetOption)!, pr.GetValue(startAtOption));
    await Save(mgr.Config, path!);
    Console.WriteLine($"Assigned {assigned} IPs (subnet: {pr.GetValue(subnetOption)}, start: .{pr.GetValue(startAtOption)})");

    var conflicts = NetworkGenerator.ValidateNoConflicts(mgr.Config);
    if (conflicts.Count > 0)
    {
        Console.Error.WriteLine($"WARNING: {conflicts.Count} IP conflict(s):");
        foreach (var c in conflicts) Console.Error.WriteLine($"  - {c}");
    }
});
networkCmd.Subcommands.Add(networkGenerateCmd);

var networkShowCmd = new Command("show", "Show network assignments and check for conflicts");
networkShowCmd.Options.Add(configOption);
networkShowCmd.SetAction(async (pr, ct) =>
{
    var config = await LoadConfig(pr.GetValue(configOption)!);
    if (config is null) return;

    foreach (var (name, ip, hostname) in NetworkGenerator.Show(config))
        Console.WriteLine($"  {name}: ip={ip ?? "none"}, hostname={hostname ?? name}");

    var conflicts = NetworkGenerator.ValidateNoConflicts(config);
    if (conflicts.Count > 0)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"WARNING: {conflicts.Count} IP conflict(s):");
        foreach (var c in conflicts) Console.Error.WriteLine($"  - {c}");
    }
});
networkCmd.Subcommands.Add(networkShowCmd);

rootCommand.Subcommands.Add(networkCmd);

// ── resolve ─────────────────────────────────────────────────────────
var resolveCmd = new Command("resolve", "Show fully resolved instance configuration");
var resolveNameArg = new Argument<string?>("name") { Description = "Instance name (optional)", Arity = ArgumentArity.ZeroOrOne };
var resolveAllOption = new Option<bool>("--all") { Description = "Show all instances" };
resolveCmd.Arguments.Add(resolveNameArg);
resolveCmd.Options.Add(resolveAllOption);
resolveCmd.Options.Add(configOption);
resolveCmd.SetAction(async (pr, ct) =>
{
    var config = await LoadConfig(pr.GetValue(configOption)!);
    if (config is null) return;
    var all = VosConfigMerger.ResolveAll(config);
    var name = pr.GetValue(resolveNameArg);
    var showAll = pr.GetValue(resolveAllOption) || name is null;

    var targets = showAll ? all : all.Where(x => x.Instance.Name == name).ToList();
    foreach (var (mn, inst) in targets)
    {
        Console.WriteLine($"--- {inst.Name} (machine={mn}) ---");
        Console.WriteLine($"  Box: {inst.Box}");
        Console.WriteLine($"  Provider: {inst.ProviderType}");
        Console.WriteLine($"  Memory: {inst.Memory}MB, CPUs: {inst.Cpus}, Video: {inst.VideoMemory}MB");
        Console.WriteLine($"  Hostname: {inst.Hostname}");
        Console.WriteLine($"  IP: {inst.Ip ?? "none"}");
        Console.WriteLine($"  GUI: {inst.Gui}, Linked clones: {inst.LinkedClones}");
        Console.WriteLine($"  Provisioning: {inst.Provisioning.Count} steps");
        Console.WriteLine($"  Shared folders: {inst.SharedFolders.Count}");
        Console.WriteLine($"  VBoxManage: {inst.VboxManage.Count} commands");
        Console.WriteLine($"  Variables: {inst.Variables.Count}");
        Console.WriteLine($"  Plugins: {string.Join(", ", inst.Plugins)}");
        Console.WriteLine();
    }
});
rootCommand.Subcommands.Add(resolveCmd);

// ── version ─────────────────────────────────────────────────────────
var versionCmd = new Command("version", "Show vos version");
versionCmd.SetAction((_) => Console.WriteLine("vos 0.2.0"));
rootCommand.Subcommands.Add(versionCmd);

// ═══════════════════════════════════════════════════════════════════
// RUN
// ═══════════════════════════════════════════════════════════════════

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

// ═══════════════════════════════════════════════════════════════════
// HELPERS
// ═══════════════════════════════════════════════════════════════════

void PrintResult(string name, VosActionResult result)
{
    if (result.Success)
        Console.WriteLine($"{name}: {result.Output}");
    else
        Console.Error.WriteLine($"{name}: ERROR — {result.Error ?? result.Output}");
}

async Task<VosOrchestrator?> CreateOrchestrator(string configPath)
{
    var config = await LoadConfig(configPath);
    if (config is null) return null;
    return new VosOrchestrator(new VagrantBackend(), config);
}

async Task<VosConfig?> LoadConfig(string path)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Config not found: {path}. Run 'vos init' first.");
        return null;
    }
    return await new VosConfigSerializer().DeserializeAsync(path);
}

async Task<(VosConfigManager? mgr, string? path)> LoadManager(string configPath)
{
    var config = await LoadConfig(configPath);
    if (config is null) return (null, null);
    return (new VosConfigManager(config), configPath);
}

async Task Save(VosConfig config, string path)
{
    await new VosConfigSerializer().SerializeAsync(config, path);
}
