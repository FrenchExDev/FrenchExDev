using FrenchExDev.Net.Vos.Lib.Services;
using FrenchExDev.Net.Vos.Lib.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class VosMachineServiceTests
{
    private readonly FakeVosFileReader _reader = new();
    private readonly FakeVosFileWriter _writer = new();
    private readonly VosEventEmitter _emitter = new();
    private readonly TestVosEventCollector _events = new();
    private readonly VosMachineService _svc;

    public VosMachineServiceTests()
    {
        _events.Subscribe(_emitter);
        _svc = new VosMachineService(_reader, _writer, _emitter, NullLogger<VosMachineService>.Instance);
    }

    private VosConfig ConfigWithType() => new()
    {
        MachineTypes = new() { ["alpine"] = new VosMachineType { Box = "alpine/3.21" } },
        Machines = new()
    };

    [Fact]
    public async Task Add_creates_machine_with_instances()
    {
        _reader.SetConfig("c.yaml", ConfigWithType());
        var result = await _svc.AddAsync("c.yaml", "web", "alpine", 2);

        result.IsSuccess.ShouldBeTrue();
        var saved = _writer.Written["c.yaml"];
        saved.Machines.ShouldContainKey("web");
        saved.Machines["web"].Instances.Count.ShouldBe(2);
        _events.Has<MachineAdded>().ShouldBeTrue();
    }

    [Fact]
    public async Task Remove_removes_machine()
    {
        var config = ConfigWithType();
        config.Machines["web"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "web-01" }] };
        _reader.SetConfig("c.yaml", config);
        var result = await _svc.RemoveAsync("c.yaml", "web");

        result.IsSuccess.ShouldBeTrue();
        _writer.Written["c.yaml"].Machines.ShouldNotContainKey("web");
    }

    [Fact]
    public async Task Enable_disable_toggles_machine()
    {
        var config = ConfigWithType();
        config.Machines["web"] = new VosMachine { MachineTypeName = "alpine", IsEnabled = false };
        _reader.SetConfig("c.yaml", config);

        await _svc.EnableAsync("c.yaml", "web");
        _events.Has<MachineEnabled>().ShouldBeTrue();
    }

    [Fact]
    public async Task List_returns_machines()
    {
        var config = ConfigWithType();
        config.Machines["web"] = new VosMachine { MachineTypeName = "alpine" };
        _reader.SetConfig("c.yaml", config);

        var result = await _svc.ListAsync("c.yaml");
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
    }
}
