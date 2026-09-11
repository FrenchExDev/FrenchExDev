using FrenchExDev.Net.Vos.Lib.Services;
using FrenchExDev.Net.Vos.Lib.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class VosInstanceServiceTests
{
    private readonly FakeVosFileReader _reader = new();
    private readonly FakeVosFileWriter _writer = new();
    private readonly VosEventEmitter _emitter = new();
    private readonly TestVosEventCollector _events = new();
    private readonly VosInstanceService _svc;

    public VosInstanceServiceTests()
    {
        _events.Subscribe(_emitter);
        _svc = new VosInstanceService(_reader, _writer, _emitter, NullLogger<VosInstanceService>.Instance);
    }

    private static VosConfig ConfigWithMachine() => new()
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
    public async Task AddAsync_adds_instance_and_writes_config()
    {
        _reader.SetConfig("c.yaml", ConfigWithMachine());

        var result = await _svc.AddAsync("c.yaml", "web", "web-02");

        result.IsSuccess.ShouldBeTrue();
        var saved = _writer.Written["c.yaml"];
        saved.Machines["web"].Instances.Count.ShouldBe(2);
        saved.Machines["web"].Instances.ShouldContain(i => i.Name == "web-02");
        _events.Has<InstanceAdding>().ShouldBeTrue();
        _events.Has<InstanceAdded>().ShouldBeTrue();
    }

    [Fact]
    public async Task AddAsync_with_ip_and_resource_overrides()
    {
        _reader.SetConfig("c.yaml", ConfigWithMachine());

        var result = await _svc.AddAsync("c.yaml", "web", "web-02", ip: "10.0.0.5", memory: 4096, cpus: 4);

        result.IsSuccess.ShouldBeTrue();
        var inst = _writer.Written["c.yaml"].Machines["web"].Instances.First(i => i.Name == "web-02");
        inst.Ip.ShouldBe("10.0.0.5");
        inst.Memory.ShouldBe(4096);
        inst.Cpus.ShouldBe(4);
    }

    [Fact]
    public async Task AddAsync_writes_to_local_when_flag_is_true()
    {
        _reader.SetConfig("c.yaml", ConfigWithMachine());

        var result = await _svc.AddAsync("c.yaml", "web", "web-02", local: true);

        result.IsSuccess.ShouldBeTrue();
        _writer.LocalWritten.ShouldContainKey("c.yaml");
        _writer.Written.ShouldNotContainKey("c.yaml");
    }

    [Fact]
    public async Task AddAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.AddAsync("missing.yaml", "web", "web-02");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveAsync_removes_instance_and_writes_config()
    {
        _reader.SetConfig("c.yaml", ConfigWithMachine());

        var result = await _svc.RemoveAsync("c.yaml", "web", "web-01");

        result.IsSuccess.ShouldBeTrue();
        _writer.Written["c.yaml"].Machines["web"].Instances.ShouldNotContain(i => i.Name == "web-01");
        _events.Has<InstanceRemoving>().ShouldBeTrue();
        _events.Has<InstanceRemoved>().ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveAsync_writes_to_local_when_flag_is_true()
    {
        _reader.SetConfig("c.yaml", ConfigWithMachine());

        var result = await _svc.RemoveAsync("c.yaml", "web", "web-01", local: true);

        result.IsSuccess.ShouldBeTrue();
        _writer.LocalWritten.ShouldContainKey("c.yaml");
    }

    [Fact]
    public async Task RemoveAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.RemoveAsync("missing.yaml", "web", "web-01");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAsync_returns_all_instances_across_machines()
    {
        var config = new VosConfig
        {
            MachineTypes = new() { ["t"] = new VosMachineType { Box = "b" } },
            Machines = new()
            {
                ["a"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "a-01" }] },
                ["b"] = new VosMachine { MachineTypeName = "t", Instances = [new VosInstance { Name = "b-01" }, new VosInstance { Name = "b-02" }] }
            }
        };
        _reader.SetConfig("c.yaml", config);

        var result = await _svc.ListAsync("c.yaml");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(3);
        _events.Has<InstanceListed>().ShouldBeTrue();
    }

    [Fact]
    public async Task ListAsync_returns_failure_when_config_not_found()
    {
        var result = await _svc.ListAsync("missing.yaml");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAsync_returns_empty_when_no_machines()
    {
        _reader.SetConfig("c.yaml", new VosConfig());

        var result = await _svc.ListAsync("c.yaml");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
    }
}
