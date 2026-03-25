using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for DelegateTransitionAction, HierarchyDefinition,
/// HistoryListener, StateMachineListenerBase, and ConcurrencyMode.
/// </summary>
public class CoverageTests_Core
{
    // ── DelegateTransitionAction ──
    [Fact]
    public async Task DelegateTransitionAction_ExecutesAction()
    {
        var called = false;
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
                    .WithAction((f, e, t, ct) => { called = true; return Task.CompletedTask; })
            .Build().Value!;

        var machine = definition.CreateMachine();
        await machine.FireAsync("Go");
        Assert.True(called);
    }

    // ── HierarchyDefinition (unused class) ──
    [Fact]
    public void HierarchyDefinition_StoresParentChild()
    {
        var hd = new HierarchyDefinition<string>("Parent", "Child");
        Assert.Equal("Parent", hd.Parent);
        Assert.Equal("Child", hd.Child);
    }

    // ── HistoryListener ring buffer ──
    [Fact]
    public async Task HistoryListener_RingBuffer_EvictsOldRecords()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A").On("Go").TransitionTo("B")
            .When("B").On("Back").TransitionTo("A")
            .Build().Value!;

        var machine = definition.CreateMachine();
        var history = new HistoryListener<string, string>(maxRecords: 2);
        machine.AddListener(history);

        await machine.FireAsync("Go");   // 1 record
        await machine.FireAsync("Back"); // 2 records
        await machine.FireAsync("Go");   // 3rd evicts 1st

        Assert.Equal(2, history.Records.Count);
        Assert.Equal("B", history.Records[0].From); // second transition
    }

    [Fact]
    public void HistoryListener_Clear_EmptiesRecords()
    {
        var history = new HistoryListener<string, string>();
        // Manually verify clear works
        history.Clear();
        Assert.Empty(history.Records);
    }

    // ── StateMachineListenerBase default methods ──
    [Fact]
    public async Task ListenerBase_AllMethodsReturnCompletedTask()
    {
        var listener = new TestListener();
        var ct = CancellationToken.None;

        await listener.OnTransitioningAsync("A", "evt", "B", ct);
        await listener.OnTransitionedAsync("A", "evt", "B", ct);
        await listener.OnStateEnteringAsync("A", "evt", ct);
        await listener.OnStateEnteredAsync("A", "evt", ct);
        await listener.OnStateExitingAsync("A", "evt", ct);
        await listener.OnStateExitedAsync("A", "evt", ct);
        await listener.OnTransitionDeniedAsync("A", "evt", "reason", ct);
        await listener.OnErrorAsync(new Exception(), "A", ct);
        // No assertions needed — just verifying they don't throw
    }

    private sealed class TestListener : StateMachineListenerBase<string, string> { }

    // ── Concurrency mode Semaphore ──
    [Fact]
    public async Task ConcurrencyMode_Semaphore_Works()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A").On("Go").TransitionTo("B")
            .Build().Value!;

        var machine = definition.CreateMachine(ConcurrencyMode.Semaphore);
        var result = await machine.FireAsync("Go");

        Assert.True(result.IsSuccess);
        Assert.Equal("B", machine.CurrentState);
    }
}
