using FrenchExDev.Net.Vos.Lib.Services;
using FrenchExDev.Net.Vos.Lib.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class VosBoxServiceTests
{
    private readonly FakeVosBackend _backend = new();
    private readonly FakeVosFileReader _reader = new();
    private readonly VosEventEmitter _emitter = new();
    private readonly TestVosEventCollector _events = new();
    private readonly VosBoxService _svc;

    public VosBoxServiceTests()
    {
        _events.Subscribe(_emitter);
        _svc = new VosBoxService(_backend, _reader, _emitter, NullLogger<VosBoxService>.Instance);
    }

    private static VosConfig ConfigWithInstance() => new()
    {
        MachineTypes = new() { ["alpine"] = new VosMachineType { Box = "alpine/3.21" } },
        Machines = new()
        {
            ["web"] = new VosMachine
            {
                MachineTypeName = "alpine",
                Instances = [new VosInstance { Name = "web-01" }]
            }
        }
    };

    [Fact]
    public async Task ListAsync_returns_success_and_emits_events()
    {
        var result = await _svc.ListAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Success.ShouldBeTrue();
        _events.Has<BoxListing>().ShouldBeTrue();
        _events.Has<BoxListed>().ShouldBeTrue();
    }

    [Fact]
    public async Task AddAsync_returns_success_and_emits_events()
    {
        var result = await _svc.AddAsync("alpine/3.21");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<BoxAdding>().ShouldBeTrue();
        _events.Has<BoxAdded>().ShouldBeTrue();
        _events.Single<BoxAdding>().Name.ShouldBe("alpine/3.21");
    }

    [Fact]
    public async Task RemoveAsync_returns_success_and_emits_events()
    {
        var result = await _svc.RemoveAsync("alpine/3.21");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<BoxRemoving>().ShouldBeTrue();
        _events.Has<BoxRemoved>().ShouldBeTrue();
    }

    [Fact]
    public async Task PruneAsync_returns_success_and_emits_events()
    {
        var result = await _svc.PruneAsync();

        result.IsSuccess.ShouldBeTrue();
        _events.Has<BoxPruning>().ShouldBeTrue();
        _events.Has<BoxPruned>().ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAsync_returns_success_for_valid_config()
    {
        _reader.SetConfig("c.yaml", ConfigWithInstance());

        var result = await _svc.UpdateAsync("c.yaml", "web-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<BoxUpdating>().ShouldBeTrue();
        _events.Has<BoxUpdated>().ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.UpdateAsync("missing.yaml", "web-01");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task OutdatedAsync_returns_success_for_valid_config()
    {
        _reader.SetConfig("c.yaml", ConfigWithInstance());

        var result = await _svc.OutdatedAsync("c.yaml", "web-01");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<BoxOutdatedChecking>().ShouldBeTrue();
        _events.Has<BoxOutdatedChecked>().ShouldBeTrue();
    }

    [Fact]
    public async Task OutdatedAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.OutdatedAsync("missing.yaml", "web-01");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task RepackageAsync_returns_success_and_emits_events()
    {
        var result = await _svc.RepackageAsync("alpine/3.21", "virtualbox", "1.0.0");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<BoxRepackaging>().ShouldBeTrue();
        _events.Has<BoxRepackaged>().ShouldBeTrue();
        _events.Single<BoxRepackaging>().Name.ShouldBe("alpine/3.21");
        _events.Single<BoxRepackaging>().Provider.ShouldBe("virtualbox");
        _events.Single<BoxRepackaging>().Version.ShouldBe("1.0.0");
    }
}
