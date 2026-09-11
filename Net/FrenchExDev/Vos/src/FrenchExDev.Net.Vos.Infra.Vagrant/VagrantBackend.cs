using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.Packer;
using FrenchExDev.Net.Packer.Bundle;
using FrenchExDev.Net.Packer.Bundle.Hcl2;
using FrenchExDev.Net.Vagrant;
using FrenchExDev.Net.Vos;
using FrenchExDev.Net.Vos.Config;

namespace FrenchExDev.Net.Vos.Infra.Vagrant;

/// <summary>
/// <see cref="IVosBackend"/> implementation that executes real Vagrant CLI commands
/// via the <see cref="VagrantClient"/> BinaryWrapper, and Packer builds via <see cref="PackerClient"/>.
/// </summary>
public sealed class VagrantBackend : IVosBackend
{
    private readonly BinaryBinding _vagrantBinding;
    private readonly BinaryBinding _packerBinding;
    private readonly CommandExecutor _executor;
    private readonly VagrantClient _client;
    private readonly PackerClient _packerClient;
    private readonly IPackerBundleWriter _bundleWriter;

    public VagrantBackend(string vagrantPath = "vagrant", string packerPath = "packer", IPackerBundleWriter? bundleWriter = null)
    {
        _vagrantBinding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("vagrant"),
            ExecutablePath = vagrantPath
        };
        _packerBinding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("packer"),
            ExecutablePath = packerPath
        };
        _executor = new CommandExecutor(new DictionaryBinaryResolver(_vagrantBinding, _packerBinding));
        _client = FrenchExDev.Net.Vagrant.Vagrant.Create(_vagrantBinding);
        _packerClient = FrenchExDev.Net.Packer.Packer.Create(_packerBinding);
        _bundleWriter = bundleWriter ?? new PackerBundleWriter();
    }

    public string Name => "vagrant";

    private static readonly HashSet<string> Supported = new()
    {
        "up", "halt", "destroy", "reload", "provision", "status",
        "ssh", "ssh-command", "ssh-config", "suspend", "resume",
        "snapshot-save", "snapshot-restore", "snapshot-delete", "snapshot-list", "snapshot-push", "snapshot-pop",
        "port", "global-status", "package", "validate", "rdp",
        "powershell", "winrm", "winrm-config", "upload",
        "image-add", "image-remove", "image-list", "image-update", "image-prune",
        "image-outdated", "image-repackage", "image-create", "image-build",
        "init", "version"
    };

    public IReadOnlySet<string> SupportedActions => Supported;

    // ── VM Lifecycle ─────────────────────────────────────────────────

    public async Task<VosActionResult> UpAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.UpAsync(b => b.WithProvider(instance.ProviderType));
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> HaltAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default)
    {
        var command = await _client.HaltAsync(b => { if (force) b.WithForce(true); });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> DestroyAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default)
    {
        var command = await _client.DestroyAsync(b => { if (force) b.WithForce(true); });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> ReloadAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.ReloadAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> ProvisionAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.ProvisionAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> StatusAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.StatusAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> SuspendAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.SuspendAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> ResumeAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.ResumeAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    // ── Project Scaffolding ──────────────────────────────────────────

    public async Task<VosActionResult> InitAsync(string box, string outputPath, string? boxVersion = null, CancellationToken ct = default)
    {
        var command = await _client.InitAsync(b =>
        {
            b.WithOutput(outputPath);
            if (boxVersion is not null) b.WithBoxVersion(boxVersion);
        });
        return await ExecuteVagrant(command, ct);
    }

    // ── SSH / Remote Access ──────────────────────────────────────────

    public async Task<VosActionResult> SshAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.SshAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> SshCommandAsync(ResolvedInstance instance, string command, CancellationToken ct = default)
    {
        var cmd = await _client.SshAsync(b => b.WithCommand(command));
        return await ExecuteVagrant(cmd, ct);
    }

    public async Task<VosActionResult> SshConfigAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.SshConfigAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> RdpAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.RdpAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> PowershellAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.PowershellAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> WinrmAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.WinrmAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> WinrmConfigAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.WinrmConfigAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> UploadAsync(ResolvedInstance instance, string source, string destination, CancellationToken ct = default)
    {
        var command = await _client.UploadAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    // ── Snapshots ────────────────────────────────────────────────────

    public async Task<VosActionResult> SnapshotSaveAsync(ResolvedInstance instance, string name, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.SaveAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> SnapshotRestoreAsync(ResolvedInstance instance, string name, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.RestoreAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> SnapshotDeleteAsync(ResolvedInstance instance, string name, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.DeleteAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> SnapshotListAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.ListAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> SnapshotPushAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.PushAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> SnapshotPopAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.Snapshot.PopAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    // ── Image Management ─────────────────────────────────────────────

    public async Task<VosActionResult> ImageAddAsync(string name, CancellationToken ct = default)
    {
        var command = await _client.Box.AddAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> ImageRemoveAsync(string name, CancellationToken ct = default)
    {
        var command = await _client.Box.RemoveAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> ImageListAsync(CancellationToken ct = default)
    {
        var command = await _client.Box.ListAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> ImageUpdateAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.Box.UpdateAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> ImagePruneAsync(CancellationToken ct = default)
    {
        var command = await _client.Box.PruneAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> ImageOutdatedAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.Box.OutdatedAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> ImageRepackageAsync(string name, string provider, string version, CancellationToken ct = default)
    {
        var command = await _client.Box.RepackageAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    // ── Image Build (Packer integration) ─────────────────────────────

    public async Task<VosActionResult> ImageCreateAsync(string boxName, string outputPath, CancellationToken ct = default)
    {
        var bundle = new PackerBundle();
        // Scaffold a minimal Packer project structure for a Vagrant box
        bundle.Config.WithRequiredVersion(">= 1.7.0");
        bundle.Variables.Add(new PackerVariable { Name = "box_name", Default = boxName, Description = "Name of the output box" });
        await _bundleWriter.WriteAsync(bundle, outputPath, ct);
        return new VosActionResult(true, $"Packer project created at {outputPath}");
    }

    public async Task<VosImageBuildResult> ImageBuildAsync(string projectPath, IReadOnlyDictionary<string, string>? variables = null, bool force = false, CancellationToken ct = default)
    {
        // 1. packer init (install plugins)
        var initCmd = await _packerClient.InitAsync(b => { });
        var initResult = await ExecutePacker(initCmd, ct);
        if (!initResult.Success)
            return new VosImageBuildResult(false, initResult.Output, initResult.Error, []);

        // 2. Write variables to a temp var-file if provided
        string? varFilePath = null;
        try
        {
            if (variables is { Count: > 0 })
            {
                varFilePath = Path.Combine(Path.GetTempPath(), $"vos-vars-{Guid.NewGuid():N}.pkrvars.hcl");
                var lines = variables.Select(kv => $"{kv.Key} = \"{kv.Value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"");
                await File.WriteAllLinesAsync(varFilePath, lines, ct);
            }

            // 3. packer build
            var buildCmd = await _packerClient.BuildAsync(b =>
            {
                if (force) b.WithForce(true);
                if (varFilePath is not null) b.WithVarFile(varFilePath);
                b.WithMachineReadable(true);
            });

            var buildResult = await _executor.ExecuteAsync(
                _packerBinding.Identifier,
                buildCmd,
                new PackerMachineReadableParser(),
                new PackerBuildCollector(),
                ct);

            return buildResult.Match(
                onSuccess: result => new VosImageBuildResult(
                    result.Success,
                    result.Success ? "Build completed successfully" : "Build failed",
                    result.Success ? null : string.Join("; ", result.Errors),
                    result.Artifacts.Select(a => new VosArtifact(a.BuilderType, a.BuildName, a.ArtifactId)).ToList()),
                onFailure: error => new VosImageBuildResult(false, "", error.ToString(), []));
        }
        finally
        {
            if (varFilePath is not null && File.Exists(varFilePath))
                File.Delete(varFilePath);
        }
    }

    // ── Networking ───────────────────────────────────────────────────

    public async Task<VosActionResult> PortAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.PortAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    // ── Global / Diagnostics ─────────────────────────────────────────

    public async Task<VosActionResult> GlobalStatusAsync(CancellationToken ct = default)
    {
        var command = await _client.GlobalStatusAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> PackageAsync(ResolvedInstance instance, CancellationToken ct = default)
    {
        var command = await _client.PackageAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> ValidateAsync(CancellationToken ct = default)
    {
        var command = await _client.ValidateAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    public async Task<VosActionResult> VersionAsync(CancellationToken ct = default)
    {
        var command = await _client.VersionAsync(b => { });
        return await ExecuteVagrant(command, ct);
    }

    // ── Execution helpers ────────────────────────────────────────────

    private async Task<VosActionResult> ExecuteVagrant(ICliCommand command, CancellationToken ct)
    {
        var result = await _executor.ExecuteAsync(_vagrantBinding.Identifier, command, ct);
        return result.Match(
            onSuccess: output => new VosActionResult(
                output.ExitCode == 0,
                output.StandardOutput,
                output.ExitCode != 0 ? output.StandardError : null),
            onFailure: error => new VosActionResult(false, "", error.ToString()));
    }

    private async Task<VosActionResult> ExecutePacker(ICliCommand command, CancellationToken ct)
    {
        var result = await _executor.ExecuteAsync(_packerBinding.Identifier, command, ct);
        return result.Match(
            onSuccess: output => new VosActionResult(
                output.ExitCode == 0,
                output.StandardOutput,
                output.ExitCode != 0 ? output.StandardError : null),
            onFailure: error => new VosActionResult(false, "", error.ToString()));
    }
}
