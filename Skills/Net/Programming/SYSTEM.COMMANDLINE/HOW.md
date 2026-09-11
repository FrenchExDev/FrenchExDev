# System.CommandLine v2 — How-To

All examples use the Vos CLI as reference (`vos halt`, `vos snapshot save`, `vos type vboxmanage add`).

## Step 1: Define the Exception Hierarchy

One abstract base per CLI tool. One sealed class per failure mode. Primary constructors for brevity.

```csharp
// VosCliException.cs
public abstract class VosCliException(string message) : Exception(message);
public abstract class VosCliException(string message, Exception inner) : Exception(message, inner);
```

```csharp
// Exceptions/ConfigExceptions.cs
public sealed class ConfigNotFoundException(string path)
    : VosCliException($"Config not found: '{path}'. Run 'vos init' first.");

public sealed class ConfigParseException(string path, Exception inner)
    : VosCliException($"Failed to parse config: '{path}'. {inner.Message}", inner);

public sealed class ConfigValidationException(IReadOnlyList<string> errors)
    : VosCliException($"Config has {errors.Count} error(s):\n{string.Join("\n", errors.Select(e => $"  - {e}"))}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
```

```csharp
// Exceptions/LookupExceptions.cs
public sealed class MachineTypeNotFoundException(string name)
    : VosCliException($"Machine type '{name}' not found.");

public sealed class MachineTypeAlreadyExistsException(string name)
    : VosCliException($"Machine type '{name}' already exists.");

public sealed class MachineTypeInUseException(string name, IReadOnlyList<string> referencedBy)
    : VosCliException($"Cannot remove machine type '{name}': referenced by: {string.Join(", ", referencedBy)}.");

public sealed class MachineNotFoundException(string name)
    : VosCliException($"Machine '{name}' not found.");

public sealed class InstanceNotFoundException(string machine, string instance)
    : VosCliException($"Instance '{instance}' not found in machine '{machine}'.");

public sealed class SnapshotNotFoundException(string instance, string snapshot)
    : VosCliException($"Snapshot '{snapshot}' not found for instance '{instance}'.");
```

```csharp
// Exceptions/CliExceptions.cs
public sealed class RequiredOptionMissingException(string optionName)
    : VosCliException($"Option '{optionName}' is required.");
```

```csharp
// Exceptions/NetworkExceptions.cs
public sealed class SubnetExhaustedException(string subnet)
    : VosCliException($"Subnet {subnet} exhausted — no more IPs available.");

public sealed class IpConflictException(IReadOnlyList<string> conflicts)
    : VosCliException($"{conflicts.Count} IP conflict(s):\n{string.Join("\n", conflicts.Select(c => $"  - {c}"))}")
{
    public IReadOnlyList<string> Conflicts { get; } = conflicts;
}
```

```csharp
// Exceptions/BackendExceptions.cs
public sealed class VagrantExecutionException(string command, int exitCode, string stderr)
    : VosCliException($"Vagrant '{command}' failed (exit {exitCode}): {stderr}")
{
    public int ExitCode { get; } = exitCode;
}

public sealed class ProcessStartException(string executable)
    : VosCliException($"Failed to start '{executable}'.");
```

```csharp
// Exceptions/PackerExceptions.cs
public sealed class HclFileNotFoundException(string path)
    : VosCliException($"HCL file not found: '{path}'.");

public sealed class Hcl2JsonException(string hclPath, int exitCode, string stderr)
    : VosCliException($"hcl2json failed (exit {exitCode}) for '{hclPath}': {stderr}");

public sealed class BundleWriteException(string outputDir, Exception inner)
    : VosCliException($"Failed to write bundle to '{outputDir}': {inner.Message}", inner);
```

## Step 2: Define the Static Throw Class

Every method is `[DoesNotReturn]`. Call sites read as `VosCliThrow.ConfigNotFound(path)` — no `throw new` scattered in business code.

```csharp
// VosCliThrow.cs
using System.Diagnostics.CodeAnalysis;

public static class VosCliThrow
{
    // ── Config ─────────────────────────────────────────────────────
    [DoesNotReturn]
    public static void ConfigNotFound(string path)
        => throw new ConfigNotFoundException(path);

    [DoesNotReturn]
    public static void ConfigParseFailed(string path, Exception inner)
        => throw new ConfigParseException(path, inner);

    [DoesNotReturn]
    public static void ConfigInvalid(IReadOnlyList<string> errors)
        => throw new ConfigValidationException(errors);

    // ── Lookups ────────────────────────────────────────────────────
    [DoesNotReturn]
    public static void MachineTypeNotFound(string name)
        => throw new MachineTypeNotFoundException(name);

    [DoesNotReturn]
    public static void MachineTypeAlreadyExists(string name)
        => throw new MachineTypeAlreadyExistsException(name);

    [DoesNotReturn]
    public static void MachineTypeInUse(string name, IReadOnlyList<string> referencedBy)
        => throw new MachineTypeInUseException(name, referencedBy);

    [DoesNotReturn]
    public static void MachineNotFound(string name)
        => throw new MachineNotFoundException(name);

    [DoesNotReturn]
    public static void InstanceNotFound(string machine, string instance)
        => throw new InstanceNotFoundException(machine, instance);

    [DoesNotReturn]
    public static void SnapshotNotFound(string instance, string snapshot)
        => throw new SnapshotNotFoundException(instance, snapshot);

    // ── Required option ────────────────────────────────────────────
    [DoesNotReturn]
    public static void RequiredOptionMissing(string optionName)
        => throw new RequiredOptionMissingException(optionName);

    // ── Network ────────────────────────────────────────────────────
    [DoesNotReturn]
    public static void SubnetExhausted(string subnet)
        => throw new SubnetExhaustedException(subnet);

    [DoesNotReturn]
    public static void IpConflict(IReadOnlyList<string> conflicts)
        => throw new IpConflictException(conflicts);

    // ── Backend ────────────────────────────────────────────────────
    [DoesNotReturn]
    public static void VagrantFailed(string command, int exitCode, string stderr)
        => throw new VagrantExecutionException(command, exitCode, stderr);

    [DoesNotReturn]
    public static void ProcessStartFailed(string executable)
        => throw new ProcessStartException(executable);

    // ── Packer ─────────────────────────────────────────────────────
    [DoesNotReturn]
    public static void HclFileNotFound(string path)
        => throw new HclFileNotFoundException(path);

    [DoesNotReturn]
    public static void Hcl2JsonFailed(string hclPath, int exitCode, string stderr)
        => throw new Hcl2JsonException(hclPath, exitCode, stderr);

    [DoesNotReturn]
    public static void BundleWriteFailed(string outputDir, Exception inner)
        => throw new BundleWriteException(outputDir, inner);
}
```

## Step 3: Define the Symbols Class

One `static` class per CLI tool. All `Option<T>` and `Argument<T>` instances, grouped by concern.

```csharp
// VosCliSymbols.cs
using System.CommandLine;

public static class VosCliSymbols
{
    // ── Shared (inherited by many commands) ─────────────────────────
    public static Option<string> Config { get; } = new("--config")
    {
        Description = "Path to config file",
        DefaultValueFactory = _ => "config-vos.yaml"
    };

    public static Option<bool> Force { get; } = new("--force")
    {
        Description = "Force the operation"
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
```

## Step 4: Define Resolvers

Each resolver validates its inputs and throws on missing required values. Returns an immutable record.

### Simple resolver (few fields)

```csharp
// Resolvers/HaltCommandResolver.cs
public record HaltInput(string Name, string ConfigPath, bool Force);

public class HaltCommandResolver
{
    public HaltInput Resolve(ParseResult pr)
    {
        var name = pr.GetValue(VosCliSymbols.Name)
            ?? throw new RequiredOptionMissingException("name");

        var config = pr.GetValue(VosCliSymbols.Config)
            ?? throw new RequiredOptionMissingException("--config");

        return new HaltInput(name, config, pr.GetValue(VosCliSymbols.Force));
    }
}
```

### Complex resolver (many fields, derived logic)

```csharp
// Resolvers/TypeSetCommandResolver.cs
public record TypeSetInput(
    string TypeName,
    string ConfigPath,
    int? Memory,
    int? Cpus,
    int? VideoMemory,
    bool NestedVirt,
    bool SataSsd,
    bool Gui,
    string? NicPromisc,
    bool NoLinkedClones);

public class TypeSetCommandResolver
{
    public TypeSetInput Resolve(ParseResult pr)
    {
        var typeName = pr.GetValue(VosCliSymbols.TypeName)
            ?? throw new RequiredOptionMissingException("name");

        var config = pr.GetValue(VosCliSymbols.Config)
            ?? throw new RequiredOptionMissingException("--config");

        return new TypeSetInput(
            typeName,
            config,
            pr.GetValue(VosCliSymbols.Memory),
            pr.GetValue(VosCliSymbols.Cpus),
            pr.GetValue(VosCliSymbols.VideoMemory),
            pr.GetValue(VosCliSymbols.NestedVirt),
            pr.GetValue(VosCliSymbols.SataSsd),
            pr.GetValue(VosCliSymbols.Gui),
            pr.GetValue(VosCliSymbols.NicPromisc),
            pr.GetValue(VosCliSymbols.NoLinkedClones));
    }
}
```

### Resolver with cross-option validation

```csharp
// Resolvers/TypeAddCommandResolver.cs
public record TypeAddInput(string TypeName, string ConfigPath, string Box, int? Memory, int? Cpus);

public class TypeAddCommandResolver
{
    public TypeAddInput Resolve(ParseResult pr)
    {
        var typeName = pr.GetValue(VosCliSymbols.TypeName)
            ?? throw new RequiredOptionMissingException("name");

        var config = pr.GetValue(VosCliSymbols.Config)
            ?? throw new RequiredOptionMissingException("--config");

        var box = pr.GetValue(VosCliSymbols.Box);
        if (string.IsNullOrEmpty(box))
            VosCliThrow.RequiredOptionMissing("--box");

        return new TypeAddInput(
            typeName, config, box,
            pr.GetValue(VosCliSymbols.Memory),
            pr.GetValue(VosCliSymbols.Cpus));
    }
}
```

## Step 5: Define Command Classes

### Pattern A — Simple leaf command (root level)

```csharp
// Commands/StatusCommand.cs
public class StatusCommand : Command
{
    public StatusCommand(VosOrchestratorFactory factory)
        : base("status", "Show VM status")
    {
        Options.Add(VosCliSymbols.Config);

        SetAction(async (pr, ct) =>
        {
            var configPath = pr.GetValue(VosCliSymbols.Config)
                ?? throw new RequiredOptionMissingException("--config");

            var orch = factory.Create(configPath);
            var results = await orch.ExecuteAsync(null, true,
                (b, i, c) => b.StatusAsync(i, c), ct);

            foreach (var (n, r) in results)
                ResultPrinter.Print(n, r);
        });
    }
}
```

### Pattern B — Leaf command with resolver

```csharp
// Commands/HaltCommand.cs
public class HaltCommand : Command
{
    public HaltCommand(HaltCommandResolver resolver, VosOrchestratorFactory factory)
        : base("halt", "Stop VM(s)")
    {
        Arguments.Add(VosCliSymbols.Name);
        Options.Add(VosCliSymbols.Config);
        Options.Add(VosCliSymbols.Force);

        SetAction(async (pr, ct) =>
        {
            var input = resolver.Resolve(pr);
            var orch = factory.Create(input.ConfigPath);
            var results = await orch.ExecuteAsync(input.Name, false,
                (b, i, c) => b.HaltAsync(i, input.Force, c), ct);

            foreach (var (n, r) in results)
                ResultPrinter.Print(n, r);
        });
    }
}
```

### Pattern C — Command group with subcommands

The group itself has no action. Children are injected.

```csharp
// Commands/Snapshot/SnapshotSaveCommand.cs
public class SnapshotSaveCommand : Command
{
    public SnapshotSaveCommand(VosOrchestratorFactory factory)
        : base("save", "Save snapshot")
    {
        Arguments.Add(VosCliSymbols.Name);
        Arguments.Add(VosCliSymbols.SnapshotName);
        Options.Add(VosCliSymbols.Config);

        SetAction(async (pr, ct) =>
        {
            var name = pr.GetValue(VosCliSymbols.Name)
                ?? throw new RequiredOptionMissingException("name");
            var snap = pr.GetValue(VosCliSymbols.SnapshotName)
                ?? throw new RequiredOptionMissingException("snapshot-name");
            var configPath = pr.GetValue(VosCliSymbols.Config)
                ?? throw new RequiredOptionMissingException("--config");

            var orch = factory.Create(configPath);
            var results = await orch.ExecuteAsync(name, false,
                (b, i, c) => b.SnapshotSaveAsync(i, snap, c), ct);

            foreach (var (n, r) in results)
                ResultPrinter.Print(n, r);
        });
    }
}
```

```csharp
// Commands/Snapshot/SnapshotRestoreCommand.cs
public class SnapshotRestoreCommand : Command
{
    public SnapshotRestoreCommand(VosOrchestratorFactory factory)
        : base("restore", "Restore snapshot")
    {
        Arguments.Add(VosCliSymbols.Name);
        Arguments.Add(VosCliSymbols.SnapshotName);
        Options.Add(VosCliSymbols.Config);

        SetAction(async (pr, ct) =>
        {
            var name = pr.GetValue(VosCliSymbols.Name)
                ?? throw new RequiredOptionMissingException("name");
            var snap = pr.GetValue(VosCliSymbols.SnapshotName)
                ?? throw new RequiredOptionMissingException("snapshot-name");
            var configPath = pr.GetValue(VosCliSymbols.Config)
                ?? throw new RequiredOptionMissingException("--config");

            var orch = factory.Create(configPath);
            var results = await orch.ExecuteAsync(name, false,
                (b, i, c) => b.SnapshotRestoreAsync(i, snap, c), ct);

            foreach (var (n, r) in results)
                ResultPrinter.Print(n, r);
        });
    }
}
```

```csharp
// Commands/Snapshot/SnapshotListCommand.cs
public class SnapshotListCommand : Command
{
    public SnapshotListCommand(VosOrchestratorFactory factory)
        : base("list", "List snapshots")
    {
        Arguments.Add(VosCliSymbols.Name);
        Options.Add(VosCliSymbols.Config);

        SetAction(async (pr, ct) =>
        {
            var name = pr.GetValue(VosCliSymbols.Name)
                ?? throw new RequiredOptionMissingException("name");
            var configPath = pr.GetValue(VosCliSymbols.Config)
                ?? throw new RequiredOptionMissingException("--config");

            var orch = factory.Create(configPath);
            var results = await orch.ExecuteAsync(name, false,
                (b, i, c) => b.SnapshotListAsync(i, c), ct);

            foreach (var (n, r) in results)
                ResultPrinter.Print(n, r);
        });
    }
}
```

```csharp
// Commands/Snapshot/SnapshotGroupCommand.cs
public class SnapshotGroupCommand : Command
{
    public SnapshotGroupCommand(
        SnapshotSaveCommand save,
        SnapshotRestoreCommand restore,
        SnapshotListCommand list)
        : base("snapshot", "Manage VM snapshots")
    {
        Subcommands.Add(save);
        Subcommands.Add(restore);
        Subcommands.Add(list);
    }
}
```

### Pattern D — Deep nesting (type > vboxmanage > add/list)

```csharp
// Commands/Type/VboxManage/TypeVboxAddCommand.cs
public class TypeVboxAddCommand : Command
{
    public TypeVboxAddCommand(VosConfigService configService)
        : base("add", "Add a VBoxManage command")
    {
        Arguments.Add(VosCliSymbols.TypeName);
        Arguments.Add(VosCliSymbols.VboxArgs);
        Options.Add(VosCliSymbols.Config);

        SetAction(async (pr, ct) =>
        {
            var typeName = pr.GetValue(VosCliSymbols.TypeName)
                ?? throw new RequiredOptionMissingException("name");
            var args = pr.GetValue(VosCliSymbols.VboxArgs)
                ?? throw new RequiredOptionMissingException("args");
            var configPath = pr.GetValue(VosCliSymbols.Config)
                ?? throw new RequiredOptionMissingException("--config");

            var (mgr, path) = await configService.LoadManagerAsync(configPath);
            mgr.AddVboxManageCommand(typeName, args.ToList());
            await configService.SaveAsync(mgr.Config, path);
            Console.WriteLine("Added VBoxManage command");
        });
    }
}
```

```csharp
// Commands/Type/VboxManage/TypeVboxListCommand.cs
public class TypeVboxListCommand : Command
{
    public TypeVboxListCommand(VosConfigService configService)
        : base("list", "List VBoxManage commands")
    {
        Arguments.Add(VosCliSymbols.TypeName);
        Options.Add(VosCliSymbols.Config);

        SetAction(async (pr, ct) =>
        {
            var typeName = pr.GetValue(VosCliSymbols.TypeName)
                ?? throw new RequiredOptionMissingException("name");
            var configPath = pr.GetValue(VosCliSymbols.Config)
                ?? throw new RequiredOptionMissingException("--config");

            var (mgr, _) = await configService.LoadManagerAsync(configPath);
            var cmds = mgr.ListVboxManageCommands(typeName);
            for (var i = 0; i < cmds.Count; i++)
                Console.WriteLine($"  [{i}] {string.Join(" ", cmds[i])}");
        });
    }
}
```

```csharp
// Commands/Type/VboxManage/TypeVboxManageGroupCommand.cs
public class TypeVboxManageGroupCommand : Command
{
    public TypeVboxManageGroupCommand(
        TypeVboxAddCommand add,
        TypeVboxListCommand list,
        TypeVboxRemoveCommand remove,
        TypeVboxClearCommand clear)
        : base("vboxmanage", "Manage raw VBoxManage commands")
    {
        Subcommands.Add(add);
        Subcommands.Add(list);
        Subcommands.Add(remove);
        Subcommands.Add(clear);
    }
}
```

```csharp
// Commands/Type/TypeGroupCommand.cs
public class TypeGroupCommand : Command
{
    public TypeGroupCommand(
        TypeAddCommand add,
        TypeListCommand list,
        TypeShowCommand show,
        TypeRemoveCommand remove,
        TypeSetCommand set,
        TypeVboxManageGroupCommand vboxmanage)
        : base("type", "Machine type management")
    {
        Subcommands.Add(add);
        Subcommands.Add(list);
        Subcommands.Add(show);
        Subcommands.Add(remove);
        Subcommands.Add(set);
        Subcommands.Add(vboxmanage);
    }
}
```

## Step 6: Wire DI in Program.cs

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

var services = new ServiceCollection();

// Domain services
services.AddSingleton<VosOrchestratorFactory>();
services.AddSingleton<VosConfigService>();

// Resolvers
services.AddTransient<HaltCommandResolver>();
services.AddTransient<TypeSetCommandResolver>();
services.AddTransient<TypeAddCommandResolver>();

// Leaf commands
services.AddTransient<StatusCommand>();
services.AddTransient<HaltCommand>();
services.AddTransient<DestroyCommand>();
services.AddTransient<UpCommand>();
services.AddTransient<ReloadCommand>();
services.AddTransient<ProvisionCommand>();
services.AddTransient<SuspendCommand>();
services.AddTransient<ResumeCommand>();
services.AddTransient<SshCommand>();
services.AddTransient<SshCommandCommand>();
services.AddTransient<UploadCommand>();
services.AddTransient<ValidateCommand>();
services.AddTransient<InitCommand>();
services.AddTransient<VersionCommand>();
services.AddTransient<GlobalStatusCommand>();
services.AddTransient<ResolveCommand>();

// Snapshot group
services.AddTransient<SnapshotSaveCommand>();
services.AddTransient<SnapshotRestoreCommand>();
services.AddTransient<SnapshotListCommand>();
services.AddTransient<SnapshotGroupCommand>();

// Type group (deep nesting)
services.AddTransient<TypeAddCommand>();
services.AddTransient<TypeListCommand>();
services.AddTransient<TypeShowCommand>();
services.AddTransient<TypeRemoveCommand>();
services.AddTransient<TypeSetCommand>();
services.AddTransient<TypeVboxAddCommand>();
services.AddTransient<TypeVboxListCommand>();
services.AddTransient<TypeVboxRemoveCommand>();
services.AddTransient<TypeVboxClearCommand>();
services.AddTransient<TypeVboxManageGroupCommand>();
services.AddTransient<TypeGroupCommand>();

// Machine group
services.AddTransient<MachineAddCommand>();
services.AddTransient<MachineListCommand>();
services.AddTransient<MachineRemoveCommand>();
services.AddTransient<MachineEnableCommand>();
services.AddTransient<MachineDisableCommand>();
services.AddTransient<MachineGroupCommand>();

// Instance group
services.AddTransient<InstanceAddCommand>();
services.AddTransient<InstanceRemoveCommand>();
services.AddTransient<InstanceListCommand>();
services.AddTransient<InstanceGroupCommand>();

// Network group
services.AddTransient<NetworkGenerateCommand>();
services.AddTransient<NetworkShowCommand>();
services.AddTransient<NetworkGroupCommand>();

// Box group
services.AddTransient<BoxListCommand>();
services.AddTransient<BoxAddCommand>();
services.AddTransient<BoxRemoveCommand>();
services.AddTransient<BoxUpdateCommand>();
services.AddTransient<BoxPruneCommand>();
services.AddTransient<BoxOutdatedCommand>();
services.AddTransient<BoxRepackageCommand>();
services.AddTransient<BoxInitCommand>();
services.AddTransient<BoxBuildCommand>();
services.AddTransient<BoxGroupCommand>();

// Config group
services.AddTransient<ConfigShowCommand>();
services.AddTransient<ConfigGroupCommand>();

var provider = services.BuildServiceProvider();

var root = new RootCommand("vos - Vagrant superset with YAML-driven multi-machine management");

// Root-level leaf commands
root.Subcommands.Add(provider.GetRequiredService<StatusCommand>());
root.Subcommands.Add(provider.GetRequiredService<HaltCommand>());
root.Subcommands.Add(provider.GetRequiredService<DestroyCommand>());
root.Subcommands.Add(provider.GetRequiredService<UpCommand>());
root.Subcommands.Add(provider.GetRequiredService<ReloadCommand>());
root.Subcommands.Add(provider.GetRequiredService<ProvisionCommand>());
root.Subcommands.Add(provider.GetRequiredService<SuspendCommand>());
root.Subcommands.Add(provider.GetRequiredService<ResumeCommand>());
root.Subcommands.Add(provider.GetRequiredService<SshCommand>());
root.Subcommands.Add(provider.GetRequiredService<SshCommandCommand>());
root.Subcommands.Add(provider.GetRequiredService<UploadCommand>());
root.Subcommands.Add(provider.GetRequiredService<ValidateCommand>());
root.Subcommands.Add(provider.GetRequiredService<InitCommand>());
root.Subcommands.Add(provider.GetRequiredService<VersionCommand>());
root.Subcommands.Add(provider.GetRequiredService<GlobalStatusCommand>());
root.Subcommands.Add(provider.GetRequiredService<ResolveCommand>());

// Groups (children already wired in their constructors)
root.Subcommands.Add(provider.GetRequiredService<SnapshotGroupCommand>());
root.Subcommands.Add(provider.GetRequiredService<TypeGroupCommand>());
root.Subcommands.Add(provider.GetRequiredService<MachineGroupCommand>());
root.Subcommands.Add(provider.GetRequiredService<InstanceGroupCommand>());
root.Subcommands.Add(provider.GetRequiredService<NetworkGroupCommand>());
root.Subcommands.Add(provider.GetRequiredService<BoxGroupCommand>());
root.Subcommands.Add(provider.GetRequiredService<ConfigGroupCommand>());

// Run — top-level catch for all CLI exceptions
try
{
    return await root.Parse(args).InvokeAsync();
}
catch (VosCliException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
```

## Step 7: Result Integration

### Service returning Result

```csharp
// VosConfigService.cs
public class VosConfigService
{
    public async Task<Result<VosConfig, VosCliException>> LoadConfigAsync(string path)
    {
        if (!File.Exists(path))
            return Result<VosConfig, VosCliException>.Failure(
                new ConfigNotFoundException(path));

        try
        {
            var config = await new VosConfigSerializer().DeserializeAsync(path);
            return Result<VosConfig, VosCliException>.Success(config);
        }
        catch (Exception ex)
        {
            return Result<VosConfig, VosCliException>.Failure(
                new ConfigParseException(path, ex));
        }
    }

    // Convenience: load-or-throw for commands that don't need Result branching
    public async Task<VosConfig> LoadOrThrowAsync(string path)
    {
        if (!File.Exists(path))
            VosCliThrow.ConfigNotFound(path);

        try
        {
            return await new VosConfigSerializer().DeserializeAsync(path);
        }
        catch (Exception ex)
        {
            VosCliThrow.ConfigParseFailed(path, ex);
            return default; // unreachable — [DoesNotReturn] satisfies compiler
        }
    }

    public async Task<(VosConfigManager Mgr, string Path)> LoadManagerAsync(string configPath)
    {
        var config = await LoadOrThrowAsync(configPath);
        return (new VosConfigManager(config), configPath);
    }

    public async Task SaveAsync(VosConfig config, string path)
    {
        await new VosConfigSerializer().SerializeAsync(config, path);
    }
}
```

### Using FromTry

```csharp
// Wrap an operation that throws into a Result
var result = await Result.FromTryAsync<VosConfig, ConfigParseException>(
    async () => await new VosConfigSerializer().DeserializeAsync(path));

// result.IsFailure is true if ConfigParseException was thrown
// Other exceptions propagate normally
```

### Command using Result with Match

```csharp
// Commands/ValidateCommand.cs
public class ValidateCommand : Command
{
    public ValidateCommand(VosConfigService configService)
        : base("validate", "Validate Vos configuration")
    {
        Options.Add(VosCliSymbols.Config);

        SetAction(async (pr, ct) =>
        {
            var configPath = pr.GetValue(VosCliSymbols.Config)
                ?? throw new RequiredOptionMissingException("--config");

            var result = await configService.LoadConfigAsync(configPath);

            result.Match(
                onSuccess: config =>
                {
                    var errors = VosConfigValidator.Validate(config);
                    if (errors.Count > 0)
                        VosCliThrow.ConfigInvalid(errors);

                    var all = VosConfigMerger.ResolveAll(config);
                    Console.WriteLine($"Configuration valid: {all.Count} instances resolved.");
                    return 0;
                },
                onFailure: ex => throw ex);
        });
    }
}
```
