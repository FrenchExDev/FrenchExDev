using FrenchExDev.Net.FiniteStateMachine.Rich;
using FrenchExDev.Net.FiniteStateMachine.Tests.Engine.RichSimple;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for RichStateMachineDefinition.
/// </summary>
public class FullCoverageTests_RichDefinition
{
    [Fact]
    public void RichDefinition_Definition_ThrowsNotSupportedException()
    {
        var machine = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .When<StateA>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateB())
            .Build().Value!;

        IStateMachine<ISimpleState, ISimpleEvent> iMachine = machine;
        Assert.Throws<NotSupportedException>(() => iMachine.Definition);
    }

    [Fact]
    public async Task RichDefinition_RemoveListener_StopsAllNotifications()
    {
        var machine = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .When<StateA>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateB())
            .When<StateB>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateA())
            .Build().Value!;

        var listener = new FullTrackingListener();
        machine.AddListener(listener);
        await machine.FireAsync(new GoEvent());
        Assert.Equal(1, listener.TransitionedCount);

        machine.RemoveListener(listener);
        await machine.FireAsync(new GoEvent());
        Assert.Equal(1, listener.TransitionedCount); // count unchanged
    }

    [Fact]
    public async Task RichDefinition_GetPermittedEventsAsync_ReturnsEmpty()
    {
        var machine = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .When<StateA>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateB())
            .Build().Value!;

        var events = await machine.GetPermittedEventsAsync();
        Assert.Empty(events);
    }

    [Fact]
    public async Task RichDefinition_CanFireAsync_WhenTransitionExists_ReturnsTrue()
    {
        var machine = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .When<StateA>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateB())
            .Build().Value!;

        Assert.True(await machine.CanFireAsync(new GoEvent()));
    }

    [Fact]
    public async Task RichDefinition_CanFireAsync_WhenNoTransitionExists_ReturnsFalse()
    {
        var machine = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .Build().Value!;

        Assert.False(await machine.CanFireAsync(new GoEvent()));
    }

    // ═══════════════════════════════════════════════════════
    // Helper types
    // ═══════════════════════════════════════════════════════

    private sealed class FullTrackingListener : StateMachineListenerBase<ISimpleState, ISimpleEvent>
    {
        public int TransitionedCount { get; private set; }
        public override Task OnTransitionedAsync(ISimpleState from, ISimpleEvent @event, ISimpleState to, CancellationToken ct)
        { TransitionedCount++; return Task.CompletedTask; }
    }
}
