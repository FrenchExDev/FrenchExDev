using FrenchExDev.Net.FiniteStateMachine.Tests.Engine.RichSimple;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for Rich tier RemoveListener.
/// </summary>
public class CoverageTests_Rich
{
    // ── Rich tier: RemoveListener ──
    [Fact]
    public async Task RichMachine_RemoveListener_StopsNotifications()
    {
        var definition = new FrenchExDev.Net.FiniteStateMachine.Rich.RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .When<StateA>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateB())
            .Build().Value!;

        var listener = new CountingListener();
        definition.AddListener(listener);
        await definition.FireAsync(new GoEvent());
        Assert.Equal(1, listener.Count);

        definition.RemoveListener(listener);
    }

    private sealed class CountingListener : StateMachineListenerBase<ISimpleState, ISimpleEvent>
    {
        public int Count { get; private set; }
        public override Task OnTransitionedAsync(ISimpleState from, ISimpleEvent @event, ISimpleState to, CancellationToken ct)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}
