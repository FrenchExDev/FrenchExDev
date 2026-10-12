using System.CommandLine;

namespace FrenchExDev.Net.Vos.Cli.Symbols;

public static class VosCliSymbols
{
    // ── Shared ──────────────────────────────────────────────────────
    public static Option<string> Config { get; } = new("--config")
    {
        Description = "Path to config file",
        DefaultValueFactory = _ => "config-vos.yaml"
    };

    public static Option<bool> Force { get; } = new("--force")
    {
        Description = "Force the operation"
    };

    public static Option<bool> Local { get; } = new("--local")
    {
        Description = "Write to local override (gitignored) instead of base config"
    };

    public static Argument<string> Name { get; } = new("name")
    {
        Description = "VM instance name"
    };

    // ── Snapshot ────────────────────────────────────────────────────
    public static Argument<string> SnapshotName { get; } = new("snapshot-name")
    {
        Description = "Snapshot name"
    };

    // ── Type management ────────────────────────────────────────────
    public static Argument<string> TypeName { get; } = new("name")
    {
        Description = "Machine type name"
    };

    public static Option<string> Box { get; } = new("--box")
    {
        Description = "Vagrant box name"
    };

    public static Option<int?> Memory { get; } = new("--memory")
    {
        Description = "Memory in MB"
    };

    public static Option<int?> Cpus { get; } = new("--cpus")
    {
        Description = "Number of CPUs"
    };

    public static Option<int?> VideoMemory { get; } = new("--video-memory")
    {
        Description = "Video memory in MB"
    };

    public static Option<bool> NestedVirt { get; } = new("--nested-virt")
    {
        Description = "Enable nested virtualization"
    };

    public static Option<bool> SataSsd { get; } = new("--sata-ssd")
    {
        Description = "Enable SATA SSD"
    };

    public static Option<bool> Gui { get; } = new("--gui")
    {
        Description = "Enable GUI"
    };

    public static Option<string?> NicPromisc { get; } = new("--nic-promisc")
    {
        Description = "NIC promiscuous mode"
    };

    public static Option<bool> NoLinkedClones { get; } = new("--no-linked-clones")
    {
        Description = "Disable linked clones"
    };

    // ── Machine management ─────────────────────────────────────────
    public static Argument<string> MachineName { get; } = new("name")
    {
        Description = "Machine name"
    };

    public static Option<string> Type { get; } = new("--type")
    {
        Description = "Machine type name"
    };

    public static Option<int> Instances { get; } = new("--instances")
    {
        Description = "Number of instances",
        DefaultValueFactory = _ => 1
    };

    // ── Instance management ────────────────────────────────────────
    public static Argument<string> InstMachine { get; } = new("machine")
    {
        Description = "Machine name"
    };

    public static Argument<string> InstName { get; } = new("instance-name")
    {
        Description = "Instance name"
    };

    public static Option<string?> Ip { get; } = new("--ip")
    {
        Description = "IP address"
    };

    // ── Network ────────────────────────────────────────────────────
    public static Option<string> Subnet { get; } = new("--subnet")
    {
        Description = "Subnet (e.g. 192.168.56.0/24)",
        DefaultValueFactory = _ => "192.168.56.0/24"
    };

    public static Option<int> StartAt { get; } = new("--start-at")
    {
        Description = "First host number to assign",
        DefaultValueFactory = _ => 10
    };

    // ── SSH ────────────────────────────────────────────────────────
    public static Argument<string> SshCmdArg { get; } = new("command")
    {
        Description = "Command to execute"
    };

    // ── Upload ─────────────────────────────────────────────────────
    public static Argument<string> UploadSource { get; } = new("source")
    {
        Description = "Source file/directory"
    };

    public static Argument<string> UploadDestination { get; } = new("destination")
    {
        Description = "Destination path on VM"
    };

    // ── Box ────────────────────────────────────────────────────────
    public static Argument<string> BoxName { get; } = new("name")
    {
        Description = "Box name"
    };

    public static Argument<string> BoxBuildPath { get; } = new("path")
    {
        Description = "Path to Packer project"
    };

    public static Option<bool> BoxBuildForce { get; } = new("--force")
    {
        Description = "Force build"
    };

    public static Option<string[]> BoxBuildVar { get; } = new("--var")
    {
        Description = "Variable in key=value format",
        AllowMultipleArgumentsPerToken = true
    };

    // ── Box repackage ──────────────────────────────────────────────
    public static Argument<string> BoxProvider { get; } = new("provider")
    {
        Description = "Provider name (e.g. virtualbox)"
    };

    public static Argument<string> BoxVersion { get; } = new("version")
    {
        Description = "Box version"
    };

    // ── Box init ───────────────────────────────────────────────────
    public static Option<string> BoxInitOutput { get; } = new("--output")
    {
        Description = "Output directory",
        DefaultValueFactory = _ => "."
    };

    // ── Resolve ────────────────────────────────────────────────────
    public static Argument<string?> ResolveName { get; } = new("name")
    {
        Description = "Instance name (optional)",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static Option<bool> ResolveAll { get; } = new("--all")
    {
        Description = "Show all instances"
    };

    // ── VBoxManage ─────────────────────────────────────────────────
    public static Argument<string[]> VboxArgs { get; } = new("args")
    {
        Description = "VBoxManage command arguments",
        Arity = ArgumentArity.OneOrMore
    };

    public static Argument<int> VboxRemoveIndex { get; } = new("index")
    {
        Description = "Command index to remove"
    };
}
