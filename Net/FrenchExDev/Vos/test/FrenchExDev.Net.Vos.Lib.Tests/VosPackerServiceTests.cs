using FrenchExDev.Net.Vos.Lib.Services;
using FrenchExDev.Net.Vos.Lib.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class VosPackerServiceTests
{
    private readonly FakeVosBackend _backend = new();
    private readonly VosEventEmitter _emitter = new();
    private readonly TestVosEventCollector _events = new();
    private readonly VosPackerService _svc;

    public VosPackerServiceTests()
    {
        _events.Subscribe(_emitter);
        _svc = new VosPackerService(_backend, _emitter, NullLogger<VosPackerService>.Instance);
    }

    [Fact]
    public async Task InitAsync_returns_success_and_emits_events()
    {
        var result = await _svc.InitAsync("alpine/3.21", "/tmp/output");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<BoxInitializing>().ShouldBeTrue();
        _events.Has<BoxInitialized>().ShouldBeTrue();
        _events.Single<BoxInitializing>().BoxName.ShouldBe("alpine/3.21");
        _events.Single<BoxInitializing>().OutputPath.ShouldBe("/tmp/output");
    }

    [Fact]
    public async Task InitAsync_uses_default_output_path()
    {
        var result = await _svc.InitAsync("alpine/3.21");

        result.IsSuccess.ShouldBeTrue();
        _events.Single<BoxInitializing>().OutputPath.ShouldBe(".");
    }

    [Fact]
    public async Task BuildAsync_emits_built_event_on_success()
    {
        var result = await _svc.BuildAsync("/project");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Success.ShouldBeTrue();
        _events.Has<BoxBuilding>().ShouldBeTrue();
        _events.Has<BoxBuilt>().ShouldBeTrue();
        _events.Single<BoxBuilding>().ProjectPath.ShouldBe("/project");
        _events.Single<BoxBuilding>().Force.ShouldBeFalse();
    }

    [Fact]
    public async Task BuildAsync_with_force_option()
    {
        var options = new Abstractions.Options.VosPackerBuildOptions { Force = true };

        var result = await _svc.BuildAsync("/project", options);

        result.IsSuccess.ShouldBeTrue();
        _events.Single<BoxBuilding>().Force.ShouldBeTrue();
    }

    [Fact]
    public async Task BuildAsync_with_vars_option()
    {
        var options = new Abstractions.Options.VosPackerBuildOptions
        {
            Vars = new() { ["key"] = "value" }
        };

        var result = await _svc.BuildAsync("/project", options);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task BuildAsync_emits_failure_event_on_failed_build()
    {
        var backend = new ConfigurableBuildBackend(new VosImageBuildResult(false, "build output", "build failed", []));
        var emitter = new VosEventEmitter();
        var events = new TestVosEventCollector();
        events.Subscribe(emitter);
        var svc = new VosPackerService(backend, emitter, NullLogger<VosPackerService>.Instance);

        var result = await svc.BuildAsync("/project");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Success.ShouldBeFalse();
        events.Has<BoxBuildFailed>().ShouldBeTrue();
        events.Single<BoxBuildFailed>().ProjectPath.ShouldBe("/project");
        events.Single<BoxBuildFailed>().Error.ShouldBe("build failed");
    }

    [Fact]
    public async Task BuildAsync_emits_artifact_events_on_success_with_artifacts()
    {
        var artifacts = new List<VosArtifact> { new("vagrant", "alpine.box", "/output/alpine.box") };
        var backend = new ConfigurableBuildBackend(new VosImageBuildResult(true, "built", null, artifacts));
        var emitter = new VosEventEmitter();
        var events = new TestVosEventCollector();
        events.Subscribe(emitter);
        var svc = new VosPackerService(backend, emitter, NullLogger<VosPackerService>.Instance);

        var result = await svc.BuildAsync("/project");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Success.ShouldBeTrue();
        events.Has<BoxArtifactProduced>().ShouldBeTrue();
        events.Has<BoxBuilt>().ShouldBeTrue();
        events.Single<BoxBuilt>().ArtifactCount.ShouldBe(1);
        var artifact = events.Single<BoxArtifactProduced>();
        artifact.BuilderType.ShouldBe("vagrant");
        artifact.Name.ShouldBe("alpine.box");
    }

    [Fact]
    public async Task BuildAsync_failed_build_uses_output_when_error_is_null()
    {
        var backend = new ConfigurableBuildBackend(new VosImageBuildResult(false, "build output", null, []));
        var emitter = new VosEventEmitter();
        var events = new TestVosEventCollector();
        events.Subscribe(emitter);
        var svc = new VosPackerService(backend, emitter, NullLogger<VosPackerService>.Instance);

        var result = await svc.BuildAsync("/project");

        result.Value!.Success.ShouldBeFalse();
        events.Single<BoxBuildFailed>().Error.ShouldBe("build output");
    }

    /// <summary>
    /// A backend that returns a configurable build result while delegating everything else.
    /// </summary>
    private sealed class ConfigurableBuildBackend(VosImageBuildResult buildResult) : IVosBackend
    {
        private readonly FakeVosBackend _inner = new();
        public string Name => _inner.Name;
        public IReadOnlySet<string> SupportedActions => _inner.SupportedActions;
        public Task<VosActionResult> UpAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.UpAsync(instance, ct);
        public Task<VosActionResult> HaltAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default) => _inner.HaltAsync(instance, force, ct);
        public Task<VosActionResult> DestroyAsync(ResolvedInstance instance, bool force = false, CancellationToken ct = default) => _inner.DestroyAsync(instance, force, ct);
        public Task<VosActionResult> ReloadAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.ReloadAsync(instance, ct);
        public Task<VosActionResult> ProvisionAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.ProvisionAsync(instance, ct);
        public Task<VosActionResult> StatusAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.StatusAsync(instance, ct);
        public Task<VosActionResult> SuspendAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.SuspendAsync(instance, ct);
        public Task<VosActionResult> ResumeAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.ResumeAsync(instance, ct);
        public Task<VosActionResult> InitAsync(string box, string outputPath, string? boxVersion = null, CancellationToken ct = default) => _inner.InitAsync(box, outputPath, boxVersion, ct);
        public Task<VosActionResult> SshAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.SshAsync(instance, ct);
        public Task<VosActionResult> SshCommandAsync(ResolvedInstance instance, string command, CancellationToken ct = default) => _inner.SshCommandAsync(instance, command, ct);
        public Task<VosActionResult> SshConfigAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.SshConfigAsync(instance, ct);
        public Task<VosActionResult> RdpAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.RdpAsync(instance, ct);
        public Task<VosActionResult> PowershellAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.PowershellAsync(instance, ct);
        public Task<VosActionResult> WinrmAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.WinrmAsync(instance, ct);
        public Task<VosActionResult> WinrmConfigAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.WinrmConfigAsync(instance, ct);
        public Task<VosActionResult> UploadAsync(ResolvedInstance instance, string source, string destination, CancellationToken ct = default) => _inner.UploadAsync(instance, source, destination, ct);
        public Task<VosActionResult> SnapshotSaveAsync(ResolvedInstance instance, string name, CancellationToken ct = default) => _inner.SnapshotSaveAsync(instance, name, ct);
        public Task<VosActionResult> SnapshotRestoreAsync(ResolvedInstance instance, string name, CancellationToken ct = default) => _inner.SnapshotRestoreAsync(instance, name, ct);
        public Task<VosActionResult> SnapshotDeleteAsync(ResolvedInstance instance, string name, CancellationToken ct = default) => _inner.SnapshotDeleteAsync(instance, name, ct);
        public Task<VosActionResult> SnapshotListAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.SnapshotListAsync(instance, ct);
        public Task<VosActionResult> SnapshotPushAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.SnapshotPushAsync(instance, ct);
        public Task<VosActionResult> SnapshotPopAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.SnapshotPopAsync(instance, ct);
        public Task<VosActionResult> ImageAddAsync(string name, CancellationToken ct = default) => _inner.ImageAddAsync(name, ct);
        public Task<VosActionResult> ImageRemoveAsync(string name, CancellationToken ct = default) => _inner.ImageRemoveAsync(name, ct);
        public Task<VosActionResult> ImageListAsync(CancellationToken ct = default) => _inner.ImageListAsync(ct);
        public Task<VosActionResult> ImageUpdateAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.ImageUpdateAsync(instance, ct);
        public Task<VosActionResult> ImagePruneAsync(CancellationToken ct = default) => _inner.ImagePruneAsync(ct);
        public Task<VosActionResult> ImageOutdatedAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.ImageOutdatedAsync(instance, ct);
        public Task<VosActionResult> ImageRepackageAsync(string name, string provider, string version, CancellationToken ct = default) => _inner.ImageRepackageAsync(name, provider, version, ct);
        public Task<VosActionResult> ImageCreateAsync(string boxName, string outputPath, CancellationToken ct = default) => _inner.ImageCreateAsync(boxName, outputPath, ct);
        public Task<VosImageBuildResult> ImageBuildAsync(string projectPath, IReadOnlyDictionary<string, string>? variables = null, bool force = false, CancellationToken ct = default)
            => Task.FromResult(buildResult);
        public Task<VosActionResult> PortAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.PortAsync(instance, ct);
        public Task<VosActionResult> GlobalStatusAsync(CancellationToken ct = default) => _inner.GlobalStatusAsync(ct);
        public Task<VosActionResult> PackageAsync(ResolvedInstance instance, CancellationToken ct = default) => _inner.PackageAsync(instance, ct);
        public Task<VosActionResult> ValidateAsync(CancellationToken ct = default) => _inner.ValidateAsync(ct);
        public Task<VosActionResult> VersionAsync(CancellationToken ct = default) => _inner.VersionAsync(ct);
    }
}
