using FrenchExDev.Net.Vos.Lib.Services;
using FrenchExDev.Net.Vos.Lib.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class VosMachineTypeServiceTests
{
    private readonly FakeVosFileReader _reader = new();
    private readonly FakeVosFileWriter _writer = new();
    private readonly VosEventEmitter _emitter = new();
    private readonly TestVosEventCollector _events = new();
    private readonly VosMachineTypeService _svc;

    public VosMachineTypeServiceTests()
    {
        _events.Subscribe(_emitter);
        _svc = new VosMachineTypeService(_reader, _writer, _emitter, NullLogger<VosMachineTypeService>.Instance);
    }

    private VosConfig CreateConfig() => new()
    {
        MachineTypes = new() { ["alpine"] = new VosMachineType { Box = "alpine/3.21" } },
        Machines = new() { ["main"] = new VosMachine { MachineTypeName = "alpine", Instances = [new VosInstance { Name = "main-01" }] } }
    };

    [Fact]
    public async Task Add_creates_machine_type_and_emits_events()
    {
        _reader.SetConfig("config.yaml", new VosConfig());
        var result = await _svc.AddAsync("config.yaml", "docker", "ubuntu/jammy64");

        result.IsSuccess.ShouldBeTrue();
        _writer.Written.ShouldContainKey("config.yaml");
        _writer.Written["config.yaml"].MachineTypes.ShouldContainKey("docker");
        _events.Has<MachineTypeAdding>().ShouldBeTrue();
        _events.Has<MachineTypeAdded>().ShouldBeTrue();
    }

    [Fact]
    public async Task Add_with_local_flag_writes_to_local()
    {
        _reader.SetConfig("config.yaml", new VosConfig());
        await _svc.AddAsync("config.yaml", "docker", "ubuntu/jammy64", local: true);

        _writer.LocalWritten.ShouldContainKey("config.yaml");
        _writer.Written.ShouldNotContainKey("config.yaml");
    }

    [Fact]
    public async Task List_returns_all_machine_types()
    {
        _reader.SetConfig("config.yaml", CreateConfig());
        var result = await _svc.ListAsync("config.yaml");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value!.ShouldContainKey("alpine");
        _events.Has<MachineTypeListed>().ShouldBeTrue();
    }

    [Fact]
    public async Task Show_returns_specific_machine_type()
    {
        _reader.SetConfig("config.yaml", CreateConfig());
        var result = await _svc.ShowAsync("config.yaml", "alpine");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Box.ShouldBe("alpine/3.21");
    }

    [Fact]
    public async Task Show_fails_for_unknown_type()
    {
        _reader.SetConfig("config.yaml", CreateConfig());
        var result = await _svc.ShowAsync("config.yaml", "nope");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Remove_removes_and_emits_events()
    {
        var config = CreateConfig();
        config.Machines.Clear(); // no machines referencing alpine
        _reader.SetConfig("config.yaml", config);
        var result = await _svc.RemoveAsync("config.yaml", "alpine");

        result.IsSuccess.ShouldBeTrue();
        _events.Has<MachineTypeRemoved>().ShouldBeTrue();
    }

    [Fact]
    public async Task Set_applies_vboxmanage_for_nested_virt()
    {
        _reader.SetConfig("config.yaml", CreateConfig());
        var settings = new Abstractions.Options.MachineTypeSettings { NestedVirt = true };
        var result = await _svc.SetAsync("config.yaml", "alpine", settings);

        result.IsSuccess.ShouldBeTrue();
        var saved = _writer.Written["config.yaml"];
        saved.MachineTypes["alpine"].Provider!.VboxManage.Count.ShouldBeGreaterThan(0);
        _events.Has<MachineTypeUpdated>().ShouldBeTrue();
    }

    [Fact]
    public async Task AddVboxManage_adds_command()
    {
        _reader.SetConfig("config.yaml", CreateConfig());
        var result = await _svc.AddVboxManageAsync("config.yaml", "alpine", ["modifyvm", "{{ .Name }}", "--cpus", "4"]);

        result.IsSuccess.ShouldBeTrue();
        _events.Has<VboxManageCommandAdded>().ShouldBeTrue();
    }

    [Fact]
    public async Task Config_not_found_returns_failure()
    {
        var result = await _svc.ListAsync("nonexistent.yaml");
        result.IsFailure.ShouldBeTrue();
    }
}
