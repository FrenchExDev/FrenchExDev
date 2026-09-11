using FrenchExDev.Net.FiniteStateMachine.Attributes;
using TransitionAttr = FrenchExDev.Net.FiniteStateMachine.Attributes.TransitionAttribute;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.SourceGenerator;

public class GenerationTests
{
    [Fact]
    public void Generated_InitialState_IsClosed()
    {
        var machine = new DoorStateMachine();
        Assert.Equal(SgDoorState.Closed, machine.CurrentState);
    }

    [Fact]
    public async Task Generated_FireOpenAsync_TransitionsToClosed()
    {
        var machine = new DoorStateMachine();

        var result = await machine.FireOpenAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(SgDoorState.Open, machine.CurrentState);
    }

    [Fact]
    public async Task Generated_CanFireOpenAsync_ReturnsTrue()
    {
        var machine = new DoorStateMachine();
        Assert.True(await machine.CanFireOpenAsync());
    }

    [Fact]
    public async Task Generated_CanFireCloseAsync_ReturnsFalse_WhenClosed()
    {
        var machine = new DoorStateMachine();
        Assert.False(await machine.CanFireCloseAsync());
    }

    [Fact]
    public async Task Generated_FullTraversal_Works()
    {
        var machine = new DoorStateMachine();

        await machine.FireOpenAsync();
        Assert.Equal(SgDoorState.Open, machine.CurrentState);

        await machine.FireCloseAsync();
        Assert.Equal(SgDoorState.Closed, machine.CurrentState);

        await machine.FireLockAsync();
        Assert.Equal(SgDoorState.Locked, machine.CurrentState);

        await machine.FireUnlockAsync();
        Assert.Equal(SgDoorState.Closed, machine.CurrentState);
    }

    [Fact]
    public void Generated_MermaidGraph_IsNotEmpty()
    {
        Assert.Contains("stateDiagram-v2", DoorStateMachineGraph.MermaidGraph);
        Assert.Contains("Closed --> Open : Open", DoorStateMachineGraph.MermaidGraph);
    }

    [Fact]
    public void Generated_DotGraph_IsNotEmpty()
    {
        Assert.Contains("digraph", DoorStateMachineGraph.DotGraph);
        Assert.Contains("Closed -> Open", DoorStateMachineGraph.DotGraph);
    }

    [Fact]
    public void Generated_PermittedEventsFrom_ReturnsCorrect()
    {
        var events = DoorStateMachineGraph.PermittedEventsFrom(SgDoorState.Closed);
        Assert.Contains(SgDoorEvent.Open, events);
        Assert.Contains(SgDoorEvent.Lock, events);
    }

    [Fact]
    public void Generated_ToString_ContainsState()
    {
        var machine = new DoorStateMachine();
        Assert.Contains("Closed", machine.ToString());
        Assert.Contains("DoorStateMachine", machine.ToString());
    }

    [Fact]
    public async Task Generated_EntryExitHooks_AreCalled()
    {
        var machine = new TrackingDoorStateMachine();

        await machine.FireOpenAsync();

        Assert.Contains("ExitClosed", machine.Hooks);
        Assert.Contains("EntryOpen", machine.Hooks);
    }

    [Fact]
    public async Task Generated_TransitionHooks_AreCalled()
    {
        var machine = new TrackingDoorStateMachine();

        await machine.FireOpenAsync();

        Assert.Contains("TransitionClosedToOpen", machine.Hooks);
    }
}

[StateMachine(typeof(SgDoorState), typeof(SgDoorEvent), InitialState = nameof(SgDoorState.Closed))]
public partial class TrackingDoorStateMachine
{
    public List<string> Hooks { get; } = new();

    [TransitionAttr(SgDoorState.Closed, SgDoorEvent.Open, SgDoorState.Open)]
    [TransitionAttr(SgDoorState.Open, SgDoorEvent.Close, SgDoorState.Closed)]
    private static partial void DefineTransitions();

    protected virtual Task OnEntryOpenAsync(System.Threading.CancellationToken ct)
    {
        Hooks.Add("EntryOpen");
        return Task.CompletedTask;
    }

    protected virtual Task OnExitClosedAsync(System.Threading.CancellationToken ct)
    {
        Hooks.Add("ExitClosed");
        return Task.CompletedTask;
    }

    protected virtual Task OnTransitionClosedToOpenAsync(System.Threading.CancellationToken ct)
    {
        Hooks.Add("TransitionClosedToOpen");
        return Task.CompletedTask;
    }
}
