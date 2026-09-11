using FrenchExDev.Net.Vos.Lib.Services;
using FrenchExDev.Net.Vos.Lib.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class VosNetworkServiceTests
{
    private readonly FakeVosFileReader _reader = new();
    private readonly FakeVosFileWriter _writer = new();
    private readonly VosEventEmitter _emitter = new();
    private readonly TestVosEventCollector _events = new();
    private readonly VosNetworkService _svc;

    public VosNetworkServiceTests()
    {
        _events.Subscribe(_emitter);
        _svc = new VosNetworkService(_reader, _writer, _emitter, NullLogger<VosNetworkService>.Instance);
    }

    private static VosConfig ConfigWithInstances() => new()
    {
        MachineTypes = new() { ["alpine"] = new VosMachineType { Box = "alpine/3.21" } },
        Machines = new()
        {
            ["web"] = new VosMachine
            {
                MachineTypeName = "alpine",
                Instances = [new VosInstance { Name = "web-01" }, new VosInstance { Name = "web-02" }]
            }
        }
    };

    [Fact]
    public async Task GenerateAsync_assigns_ips_and_writes_config()
    {
        _reader.SetConfig("c.yaml", ConfigWithInstances());

        var result = await _svc.GenerateAsync("c.yaml");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        _writer.Written.ShouldContainKey("c.yaml");
        _events.Has<NetworkGenerating>().ShouldBeTrue();
        _events.Has<NetworkGenerated>().ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.GenerateAsync("missing.yaml");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateAsync_writes_to_local_when_local_flag_is_true()
    {
        _reader.SetConfig("c.yaml", ConfigWithInstances());

        var result = await _svc.GenerateAsync("c.yaml", local: true);

        result.IsSuccess.ShouldBeTrue();
        _writer.LocalWritten.ShouldContainKey("c.yaml");
        _writer.Written.ShouldNotContainKey("c.yaml");
    }

    [Fact]
    public async Task GenerateAsync_emits_conflict_events_for_duplicate_ips()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = "b" } },
            Machines = new()
            {
                ["a"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = [new VosInstance { Name = "i1", Ip = "192.168.56.10" }]
                },
                ["b"] = new VosMachine
                {
                    MachineTypeName = "t",
                    Instances = [new VosInstance { Name = "i2", Ip = "192.168.56.10" }]
                }
            }
        };
        _reader.SetConfig("c.yaml", config);

        await _svc.GenerateAsync("c.yaml");

        _events.Has<NetworkConflictDetected>().ShouldBeTrue();
    }

    [Fact]
    public async Task ShowAsync_returns_all_instance_network_assignments()
    {
        var config = ConfigWithInstances();
        config.Machines["web"].Instances[0].Ip = "192.168.56.10";
        config.Machines["web"].Instances[0].Hostname = "web-01.local";
        _reader.SetConfig("c.yaml", config);

        var result = await _svc.ShowAsync("c.yaml");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(2);
        _events.Has<NetworkShowing>().ShouldBeTrue();
        _events.Has<NetworkShown>().ShouldBeTrue();
    }

    [Fact]
    public async Task ShowAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.ShowAsync("missing.yaml");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateAsync_with_custom_subnet_and_startAt()
    {
        _reader.SetConfig("c.yaml", ConfigWithInstances());

        var result = await _svc.GenerateAsync("c.yaml", subnet: "10.0.0.0/24", startAt: 100);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        var saved = _writer.Written["c.yaml"];
        saved.Machines["web"].Instances[0].Ip.ShouldStartWith("10.0.0.");
    }
}
