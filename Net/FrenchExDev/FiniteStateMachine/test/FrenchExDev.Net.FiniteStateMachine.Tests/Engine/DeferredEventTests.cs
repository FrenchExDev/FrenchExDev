using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

public class DeferredEventTests
{
    [Fact]
    public async Task DeferredEvent_IsQueuedAndReplayed()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Next").TransitionTo("B")
            .When("B")
                .On("Deferred").TransitionTo("C")
            .Build().Value!;

        var machine = definition.CreateMachine();
        var queue = new DeferredEventQueue<string, string>();
        queue.Defer("A", "Deferred"); // Deferred event in state A

        // Fire "Deferred" while in A — should be queued
        Assert.True(queue.IsDeferred("A", "Deferred"));
        queue.Enqueue("Deferred");

        Assert.Equal(1, queue.Count);

        // Transition to B
        await machine.FireAsync("Next");
        Assert.Equal("B", machine.CurrentState);

        // Replay — "Deferred" is now handled in B
        var replayed = await queue.ReplayAsync(machine);

        Assert.Equal(1, replayed);
        Assert.Equal("C", machine.CurrentState);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task DeferredEvent_StaysQueued_IfStillDeferred()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Next").TransitionTo("B")
            .Build().Value!;

        var machine = definition.CreateMachine();
        var queue = new DeferredEventQueue<string, string>();
        queue.Defer("A", "X");
        queue.Defer("B", "X"); // Also deferred in B

        queue.Enqueue("X");
        await machine.FireAsync("Next"); // A → B

        var replayed = await queue.ReplayAsync(machine);

        Assert.Equal(0, replayed); // Still deferred in B
        Assert.Equal(1, queue.Count);
    }
}
