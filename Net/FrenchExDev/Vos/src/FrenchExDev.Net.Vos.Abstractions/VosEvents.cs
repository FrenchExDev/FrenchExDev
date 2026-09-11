namespace FrenchExDev.Net.Vos.Abstractions;

public abstract record VosEvent
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

// -- Config lifecycle ────────────────────────────────────────────────
public sealed record ConfigLoading(string Path) : VosEvent;
public sealed record ConfigLoaded(string Path, int MachineTypeCount, int MachineCount) : VosEvent;
public sealed record ConfigNotFound(string Path) : VosEvent;
public sealed record ConfigSaved(string Path) : VosEvent;

// -- Project init ────────────────────────────────────────────────────
public sealed record ProjectInitializing(string OutputDir) : VosEvent;
public sealed record FileCreated(string Path) : VosEvent;
public sealed record VagrantfileGenerated(string Path) : VosEvent;
public sealed record ProjectInitialized(string OutputDir) : VosEvent;

// -- Validation ──────────────────────────────────────────────────────
public sealed record ValidationStarted(string Path) : VosEvent;
public sealed record ValidationError(string Message) : VosEvent;
public sealed record ValidationCompleted(int ErrorCount, int ResolvedInstanceCount) : VosEvent;

// -- Config show / resolve ───────────────────────────────────────────
public sealed record ConfigShowStarted(string Path) : VosEvent;
public sealed record ConfigShowCompleted(int InstanceCount) : VosEvent;
public sealed record ResolveStarted(string Path, string? InstanceName) : VosEvent;
public sealed record InstanceResolved(string MachineName, string InstanceName) : VosEvent;
public sealed record ResolveCompleted(int Count) : VosEvent;

// -- Machine type CRUD ───────────────────────────────────────────────
public sealed record MachineTypeAdding(string Name, string Box) : VosEvent;
public sealed record MachineTypeAdded(string Name, string Box) : VosEvent;
public sealed record MachineTypeRemoving(string Name) : VosEvent;
public sealed record MachineTypeRemoved(string Name) : VosEvent;
public sealed record MachineTypeListed(int Count) : VosEvent;
public sealed record MachineTypeShown(string Name) : VosEvent;
public sealed record MachineTypeUpdating(string Name) : VosEvent;
public sealed record MachineTypeUpdated(string Name) : VosEvent;

// -- VBoxManage ──────────────────────────────────────────────────────
public sealed record VboxManageCommandAdding(string MachineTypeName, IReadOnlyList<string> Args) : VosEvent;
public sealed record VboxManageCommandAdded(string MachineTypeName) : VosEvent;
public sealed record VboxManageCommandRemoving(string MachineTypeName, int Index) : VosEvent;
public sealed record VboxManageCommandRemoved(string MachineTypeName, int Index) : VosEvent;
public sealed record VboxManageCommandsClearing(string MachineTypeName) : VosEvent;
public sealed record VboxManageCommandsCleared(string MachineTypeName) : VosEvent;
public sealed record VboxManageCommandsListed(string MachineTypeName, int Count) : VosEvent;

// -- Provisioning config ─────────────────────────────────────────────
public sealed record ProvisioningStepAdding(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningStepAdded(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningStepRemoving(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningStepRemoved(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningStepsListed(string TypeName, int Count) : VosEvent;
public sealed record ProvisioningStepShown(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningStepEnabling(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningStepEnabled(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningStepDisabling(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningStepDisabled(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningStepMoving(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningStepMoved(string TypeName, string Key) : VosEvent;
public sealed record ProvisioningEnvSetting(string TypeName, string Key, string EnvKey) : VosEvent;
public sealed record ProvisioningEnvSet(string TypeName, string Key, string EnvKey) : VosEvent;
public sealed record ProvisioningEnvRemoving(string TypeName, string Key, string EnvKey) : VosEvent;
public sealed record ProvisioningEnvRemoved(string TypeName, string Key, string EnvKey) : VosEvent;
public sealed record ProvisioningScriptCreating(string Key, string Path) : VosEvent;
public sealed record ProvisioningScriptCreated(string Key, string Path) : VosEvent;
public sealed record ProvisioningValidating : VosEvent;
public sealed record ProvisioningValidated(int ErrorCount) : VosEvent;
public sealed record ProvisioningScriptMissing(string Key, string ExpectedPath) : VosEvent;

// -- Shared folder / Plugin ──────────────────────────────────────────
public sealed record SharedFolderAdding(string TypeName, string HostPath, string GuestPath) : VosEvent;
public sealed record SharedFolderAdded(string TypeName, string HostPath, string GuestPath) : VosEvent;
public sealed record SharedFolderRemoving(string TypeName, int Index) : VosEvent;
public sealed record SharedFolderRemoved(string TypeName, int Index) : VosEvent;
public sealed record SharedFoldersListed(string TypeName, int Count) : VosEvent;
public sealed record PluginAdding(string TypeName, string Name) : VosEvent;
public sealed record PluginAdded(string TypeName, string Name) : VosEvent;
public sealed record PluginRemoving(string TypeName, string Name) : VosEvent;
public sealed record PluginRemoved(string TypeName, string Name) : VosEvent;
public sealed record PluginsListed(string TypeName, int Count) : VosEvent;

// -- Machine CRUD ────────────────────────────────────────────────────
public sealed record MachineAdding(string Name, string TypeName, int InstanceCount) : VosEvent;
public sealed record MachineAdded(string Name, string TypeName, int InstanceCount) : VosEvent;
public sealed record MachineRemoving(string Name) : VosEvent;
public sealed record MachineRemoved(string Name) : VosEvent;
public sealed record MachineListed(int Count) : VosEvent;
public sealed record MachineEnabling(string Name) : VosEvent;
public sealed record MachineEnabled(string Name) : VosEvent;
public sealed record MachineDisabling(string Name) : VosEvent;
public sealed record MachineDisabled(string Name) : VosEvent;

// -- Instance CRUD ───────────────────────────────────────────────────
public sealed record InstanceAdding(string MachineName, string InstanceName) : VosEvent;
public sealed record InstanceAdded(string MachineName, string InstanceName) : VosEvent;
public sealed record InstanceRemoving(string MachineName, string InstanceName) : VosEvent;
public sealed record InstanceRemoved(string MachineName, string InstanceName) : VosEvent;
public sealed record InstanceListed(int Count) : VosEvent;

// -- Network ─────────────────────────────────────────────────────────
public sealed record NetworkGenerating(string Subnet, int StartAt) : VosEvent;
public sealed record NetworkGenerated(int AssignedCount) : VosEvent;
public sealed record NetworkConflictDetected(string Message) : VosEvent;
public sealed record NetworkShowing : VosEvent;
public sealed record NetworkShown(int Count) : VosEvent;

// -- VM lifecycle ────────────────────────────────────────────────────
public sealed record VmStarting(string InstanceName) : VosEvent;
public sealed record VmStarted(string InstanceName) : VosEvent;
public sealed record VmHalting(string InstanceName, bool Force) : VosEvent;
public sealed record VmHalted(string InstanceName) : VosEvent;
public sealed record VmDestroying(string InstanceName, bool Force) : VosEvent;
public sealed record VmDestroyed(string InstanceName) : VosEvent;
public sealed record VmReloading(string InstanceName) : VosEvent;
public sealed record VmReloaded(string InstanceName) : VosEvent;
public sealed record VmProvisioning(string InstanceName) : VosEvent;
public sealed record VmProvisioned(string InstanceName) : VosEvent;
public sealed record VmStatusQuerying(string InstanceName) : VosEvent;
public sealed record VmStatusQueried(string InstanceName, string Output) : VosEvent;
public sealed record VmSuspending(string InstanceName) : VosEvent;
public sealed record VmSuspended(string InstanceName) : VosEvent;
public sealed record VmResuming(string InstanceName) : VosEvent;
public sealed record VmResumed(string InstanceName) : VosEvent;
public sealed record VmOperationFailed(string Operation, string InstanceName, string Error) : VosEvent;

// -- SSH / Remote ────────────────────────────────────────────────────
public sealed record SshConnecting(string InstanceName) : VosEvent;
public sealed record SshConnected(string InstanceName) : VosEvent;
public sealed record SshCommandExecuting(string InstanceName, string Command) : VosEvent;
public sealed record SshCommandExecuted(string InstanceName, string Output) : VosEvent;
public sealed record SshConfigQuerying(string InstanceName) : VosEvent;
public sealed record SshConfigQueried(string InstanceName) : VosEvent;
public sealed record FileUploading(string InstanceName, string Source, string Destination) : VosEvent;
public sealed record FileUploaded(string InstanceName, string Source, string Destination) : VosEvent;
public sealed record RdpConnecting(string InstanceName) : VosEvent;
public sealed record RdpConnected(string InstanceName) : VosEvent;
public sealed record PowershellConnecting(string InstanceName) : VosEvent;
public sealed record PowershellConnected(string InstanceName) : VosEvent;
public sealed record WinrmConnecting(string InstanceName) : VosEvent;
public sealed record WinrmConnected(string InstanceName) : VosEvent;
public sealed record WinrmConfigQuerying(string InstanceName) : VosEvent;
public sealed record WinrmConfigQueried(string InstanceName) : VosEvent;
public sealed record PortQuerying(string InstanceName) : VosEvent;
public sealed record PortQueried(string InstanceName) : VosEvent;
public sealed record PackageCreating(string InstanceName) : VosEvent;
public sealed record PackageCreated(string InstanceName) : VosEvent;

// -- Snapshots ───────────────────────────────────────────────────────
public sealed record SnapshotSaving(string InstanceName, string SnapshotName) : VosEvent;
public sealed record SnapshotSaved(string InstanceName, string SnapshotName) : VosEvent;
public sealed record SnapshotRestoring(string InstanceName, string SnapshotName) : VosEvent;
public sealed record SnapshotRestored(string InstanceName, string SnapshotName) : VosEvent;
public sealed record SnapshotDeleting(string InstanceName, string SnapshotName) : VosEvent;
public sealed record SnapshotDeleted(string InstanceName, string SnapshotName) : VosEvent;
public sealed record SnapshotListing(string InstanceName) : VosEvent;
public sealed record SnapshotListed(string InstanceName) : VosEvent;
public sealed record SnapshotPushing(string InstanceName) : VosEvent;
public sealed record SnapshotPushed(string InstanceName) : VosEvent;
public sealed record SnapshotPopping(string InstanceName) : VosEvent;
public sealed record SnapshotPopped(string InstanceName) : VosEvent;

// -- Box / Image ─────────────────────────────────────────────────────
public sealed record BoxListing : VosEvent;
public sealed record BoxListed(string Output) : VosEvent;
public sealed record BoxAdding(string Name) : VosEvent;
public sealed record BoxAdded(string Name) : VosEvent;
public sealed record BoxRemoving(string Name) : VosEvent;
public sealed record BoxRemoved(string Name) : VosEvent;
public sealed record BoxUpdating(string InstanceName) : VosEvent;
public sealed record BoxUpdated(string InstanceName) : VosEvent;
public sealed record BoxPruning : VosEvent;
public sealed record BoxPruned : VosEvent;
public sealed record BoxOutdatedChecking(string InstanceName) : VosEvent;
public sealed record BoxOutdatedChecked(string InstanceName) : VosEvent;
public sealed record BoxRepackaging(string Name, string Provider, string Version) : VosEvent;
public sealed record BoxRepackaged(string Name) : VosEvent;
public sealed record BoxInitializing(string BoxName, string OutputPath) : VosEvent;
public sealed record BoxInitialized(string BoxName, string OutputPath) : VosEvent;
public sealed record BoxBuilding(string ProjectPath, bool Force) : VosEvent;
public sealed record BoxBuilt(string ProjectPath, int ArtifactCount) : VosEvent;
public sealed record BoxBuildFailed(string ProjectPath, string Error) : VosEvent;
public sealed record BoxArtifactProduced(string BuilderType, string Name, string? Path) : VosEvent;

// -- Diagnostics ─────────────────────────────────────────────────────
public sealed record GlobalStatusQuerying : VosEvent;
public sealed record GlobalStatusQueried(string Output) : VosEvent;
public sealed record VagrantValidating : VosEvent;
public sealed record VagrantValidated(string Output) : VosEvent;

// -- Streaming output ────────────────────────────────────────────────
public sealed record VagrantOutput(string InstanceName, string Line) : VosEvent;
public sealed record PackerOutput(string Line) : VosEvent;

// -- Dry-run ─────────────────────────────────────────────────────────
public sealed record DryRunAction(string Operation, string Target, string Description) : VosEvent;

// -- Health check ────────────────────────────────────────────────────
public sealed record HealthCheckStarting(string InstanceName) : VosEvent;
public sealed record HealthCheckCompleted(string InstanceName, bool AllPassed) : VosEvent;
public sealed record HealthCheckFailed(string InstanceName, string Check, string Expected, string Actual) : VosEvent;

// -- Secrets ─────────────────────────────────────────────────────────
public sealed record SecretResolved(string VarName) : VosEvent;
public sealed record SecretNotFound(string VarName, string ConfigPath) : VosEvent;

// -- Partial failure ─────────────────────────────────────────────────
public sealed record PartialFailureDetected(string FailedInstance, int SucceededCount, FailureStrategy Strategy) : VosEvent;
public sealed record RollbackStarting(int InstanceCount) : VosEvent;
public sealed record RollbackCompleted(int DestroyedCount) : VosEvent;

// -- Cancellation ────────────────────────────────────────────────────
public sealed record OperationCancelled(int StartedCount, int SkippedCount) : VosEvent;
