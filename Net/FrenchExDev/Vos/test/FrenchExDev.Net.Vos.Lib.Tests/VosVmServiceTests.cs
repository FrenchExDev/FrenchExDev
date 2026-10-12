using FrenchExDev.Net.Vos.Lib.Services;
using FrenchExDev.Net.Vos.Lib.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class VosVmServiceTests
{
    private readonly FakeVosFileReader _reader = new();
    private readonly FakeVosBackend _backend = new();
    private readonly VosEventEmitter _emitter = new();
    private readonly TestVosEventCollector _events = new();
    private readonly VosVmService _svc;

    public VosVmServiceTests()
    {
        _events.Subscribe(_emitter);
        _svc = new VosVmService(_backend, _reader, _emitter, NullLogger<VosVmService>.Instance);
    }

    private VosConfig CreateConfig() => new()
    {
        MachineTypes = new() { ["alpine"] = new VosMachineType { Box = "alpine/3.21", Provider = new VosProviderConfig { Memory = 2048, Cpus = 2 } } },
        Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01" }] } }
    };

    [Fact]
    public async Task Up_starts_vm_and_emits_events()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.UpAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value![0].Result.Success.ShouldBeTrue();
        _events.Has<VmStarting>().ShouldBeTrue();
        _events.Has<VmStarted>().ShouldBeTrue();
    }

    [Fact]
    public async Task Halt_emits_halting_halted()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.HaltAsync("c.yaml", "main-01", force: true);

        result.IsSuccess.ShouldBeTrue();
        _events.Single<VmHalting>().Force.ShouldBeTrue();
        _events.Has<VmHalted>().ShouldBeTrue();
    }

    [Fact]
    public async Task Destroy_emits_destroying_destroyed()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.DestroyAsync("c.yaml", "main-01", force: true);

        result.IsSuccess.ShouldBeTrue();
        _events.Has<VmDestroyed>().ShouldBeTrue();
    }

    [Fact]
    public async Task Status_queries_all_instances()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.StatusAsync("c.yaml");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        _events.Has<VmStatusQueried>().ShouldBeTrue();
    }

    [Fact]
    public async Task SshCommand_executes_and_emits()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SshCommandAsync("c.yaml", "main-01", "echo hello");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Output.ShouldContain("echo hello");
        _events.Has<SshCommandExecuting>().ShouldBeTrue();
        _events.Has<SshCommandExecuted>().ShouldBeTrue();
    }

    [Fact]
    public async Task Up_with_failed_backend_emits_failure_event()
    {
        _backend.DefaultResult = new VosActionResult(false, "", "boom");
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.UpAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue(); // Result wraps the list, backend failure is inside
        _events.Has<VmOperationFailed>().ShouldBeTrue();
        _events.Single<VmOperationFailed>().Error.ShouldBe("boom");
    }

    [Fact]
    public async Task Config_not_found_returns_failure()
    {
        var result = await _svc.UpAsync("nope.yaml", "main-01");
        result.IsFailure.ShouldBeTrue();
    }

    // ── Reload ───────────────────────────────────────────────────────

    [Fact]
    public async Task Reload_emits_reloading_reloaded()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.ReloadAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<VmReloading>().ShouldBeTrue();
        _events.Has<VmReloaded>().ShouldBeTrue();
    }

    // ── Provision ────────────────────────────────────────────────────

    [Fact]
    public async Task Provision_emits_provisioning_provisioned()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.ProvisionAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<VmProvisioning>().ShouldBeTrue();
        _events.Has<VmProvisioned>().ShouldBeTrue();
    }

    // ── Suspend ──────────────────────────────────────────────────────

    [Fact]
    public async Task Suspend_emits_suspending_suspended()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SuspendAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<VmSuspending>().ShouldBeTrue();
        _events.Has<VmSuspended>().ShouldBeTrue();
    }

    // ── Resume ───────────────────────────────────────────────────────

    [Fact]
    public async Task Resume_emits_resuming_resumed()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.ResumeAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<VmResuming>().ShouldBeTrue();
        _events.Has<VmResumed>().ShouldBeTrue();
    }

    // ── SSH ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Ssh_emits_connecting_connected()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SshAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<SshConnecting>().ShouldBeTrue();
        _events.Has<SshConnected>().ShouldBeTrue();
    }

    [Fact]
    public async Task Ssh_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SshAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    // ── SshConfig ────────────────────────────────────────────────────

    [Fact]
    public async Task SshConfig_emits_querying_queried()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SshConfigAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<SshConfigQuerying>().ShouldBeTrue();
        _events.Has<SshConfigQueried>().ShouldBeTrue();
    }

    [Fact]
    public async Task SshConfig_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SshConfigAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    // ── SshCommand instance not found ────────────────────────────────

    [Fact]
    public async Task SshCommand_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SshCommandAsync("c.yaml", "unknown", "echo hi");

        result.IsFailure.ShouldBeTrue();
    }

    // ── Upload ───────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_emits_uploading_uploaded()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.UploadAsync("c.yaml", "main-01", "/src", "/dst");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<FileUploading>().ShouldBeTrue();
        _events.Has<FileUploaded>().ShouldBeTrue();
    }

    [Fact]
    public async Task Upload_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.UploadAsync("c.yaml", "unknown", "/src", "/dst");

        result.IsFailure.ShouldBeTrue();
    }

    // ── Port ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Port_emits_querying_queried()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.PortAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<PortQuerying>().ShouldBeTrue();
        _events.Has<PortQueried>().ShouldBeTrue();
    }

    [Fact]
    public async Task Port_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.PortAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    // ── Package ──────────────────────────────────────────────────────

    [Fact]
    public async Task Package_emits_creating_created()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.PackageAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<PackageCreating>().ShouldBeTrue();
        _events.Has<PackageCreated>().ShouldBeTrue();
    }

    [Fact]
    public async Task Package_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.PackageAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    // ── Rdp ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Rdp_returns_success()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.RdpAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Rdp_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.RdpAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    // ── Powershell ───────────────────────────────────────────────────

    [Fact]
    public async Task Powershell_returns_success()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.PowershellAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Powershell_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.PowershellAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    // ── Winrm ────────────────────────────────────────────────────────

    [Fact]
    public async Task Winrm_returns_success()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.WinrmAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Winrm_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.WinrmAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    // ── WinrmConfig ──────────────────────────────────────────────────

    [Fact]
    public async Task WinrmConfig_returns_success()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.WinrmConfigAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task WinrmConfig_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.WinrmConfigAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    // ── Snapshots ────────────────────────────────────────────────────

    [Fact]
    public async Task SnapshotSave_emits_saving_saved()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotSaveAsync("c.yaml", "main-01", "snap1");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<SnapshotSaving>().ShouldBeTrue();
        _events.Has<SnapshotSaved>().ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotSave_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotSaveAsync("c.yaml", "unknown", "snap1");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotRestore_emits_restoring_restored()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotRestoreAsync("c.yaml", "main-01", "snap1");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<SnapshotRestoring>().ShouldBeTrue();
        _events.Has<SnapshotRestored>().ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotRestore_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotRestoreAsync("c.yaml", "unknown", "snap1");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotDelete_emits_deleting_deleted()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotDeleteAsync("c.yaml", "main-01", "snap1");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<SnapshotDeleting>().ShouldBeTrue();
        _events.Has<SnapshotDeleted>().ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotDelete_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotDeleteAsync("c.yaml", "unknown", "snap1");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotList_emits_listing_listed()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotListAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<SnapshotListing>().ShouldBeTrue();
        _events.Has<SnapshotListed>().ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotList_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotListAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotPush_returns_success()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotPushAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotPush_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotPushAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotPop_returns_success()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotPopAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotPop_returns_failure_for_unknown_instance()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.SnapshotPopAsync("c.yaml", "unknown");

        result.IsFailure.ShouldBeTrue();
    }

    // ── GlobalStatus ─────────────────────────────────────────────────

    [Fact]
    public async Task GlobalStatus_emits_querying_queried()
    {
        var result = await _svc.GlobalStatusAsync();

        result.IsSuccess.ShouldBeTrue();
        _events.Has<GlobalStatusQuerying>().ShouldBeTrue();
        _events.Has<GlobalStatusQueried>().ShouldBeTrue();
    }

    // ── VagrantValidate ──────────────────────────────────────────────

    [Fact]
    public async Task VagrantValidate_emits_validating_validated()
    {
        var result = await _svc.VagrantValidateAsync();

        result.IsSuccess.ShouldBeTrue();
        _events.Has<VagrantValidating>().ShouldBeTrue();
        _events.Has<VagrantValidated>().ShouldBeTrue();
    }

    // ── CheckAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task Check_returns_health_check_results()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.CheckAsync("c.yaml");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value![0].InstanceName.ShouldBe("main-01");
        _events.Has<HealthCheckStarting>().ShouldBeTrue();
        _events.Has<HealthCheckCompleted>().ShouldBeTrue();
    }

    [Fact]
    public async Task Check_filters_by_instance_name()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.CheckAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Check_returns_failure_when_config_not_found()
    {
        var result = await _svc.CheckAsync("nope.yaml");
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Check_ssh_ok_when_output_contains_ok()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        // FakeVosBackend.SshCommandAsync returns "executed: echo ok" which contains "ok"
        var result = await _svc.CheckAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        result.Value![0].SshOk.ShouldBeTrue();
    }

    // ── Halt/Destroy with failed backend (error path) ────────────────

    [Fact]
    public async Task Halt_with_failed_backend_emits_failure_event()
    {
        _backend.DefaultResult = new VosActionResult(false, "", "boom");
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.HaltAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<VmOperationFailed>().ShouldBeTrue();
    }

    [Fact]
    public async Task Destroy_with_failed_backend_emits_failure_event()
    {
        _backend.DefaultResult = new VosActionResult(false, "", "boom");
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.DestroyAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<VmOperationFailed>().ShouldBeTrue();
    }

    // ── Up with output but no error (fall back to output for error msg) ──

    [Fact]
    public async Task Up_with_failed_backend_no_error_uses_output()
    {
        _backend.DefaultResult = new VosActionResult(false, "some output");
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.UpAsync("c.yaml", "main-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Single<VmOperationFailed>().Error.ShouldBe("some output");
    }

    // ── ExecuteOnTargets with null instance (all) ────────────────────

    [Fact]
    public async Task Up_with_null_instance_targets_all()
    {
        _reader.SetConfig("c.yaml", CreateConfig());
        var result = await _svc.UpAsync("c.yaml", null);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
    }
}
