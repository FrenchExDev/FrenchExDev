using FrenchExDev.Net.FiniteStateMachine.Typed;
using FrenchExDev.Net.FiniteStateMachine.Tests.Typed;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for TypedOnBuilder InternalTransition and TransitionTable.
/// </summary>
public class CoverageTests_Typed
{
    // ── TypedOnBuilder.InternalTransition + On chaining ──
    [Fact]
    public async Task TypedOnBuilder_InternalTransition_DoesNotTriggerExitEntry()
    {
        var definition = new TypedStateMachineBuilder<DoorState, DoorEvent>()
            .InitialState(DoorState.Closed)
            .When(DoorState.Closed)
                .On(DoorEvent.Lock).InternalTransition()
                    .WithAction((f, e, t, ct) => Task.CompletedTask)
            .Build();

        var machine = definition.Value!.CreateMachine();
        var listener = new ExitTracker();
        machine.AddListener(listener);

        await machine.FireAsync(DoorEvent.Lock);
        Assert.False(listener.ExitCalled);
        Assert.Equal(DoorState.Closed, machine.CurrentState);
    }

    private sealed class ExitTracker : StateMachineListenerBase<DoorState, DoorEvent>
    {
        public bool ExitCalled { get; private set; }
        public override Task OnStateExitingAsync(DoorState state, DoorEvent @event, CancellationToken ct)
        {
            ExitCalled = true;
            return Task.CompletedTask;
        }
    }

    // ── TransitionTable GetPermittedEvents + AllTransitions ──
    [Fact]
    public void TransitionTable_AllTransitions_ReturnsAll()
    {
        var table = new TransitionTable<DoorState, DoorEvent>();
        table.Add(new TransitionDefinition<DoorState, DoorEvent>(
            DoorState.Closed, DoorEvent.Open, DoorState.Open,
            Array.Empty<IGuard<DoorState, DoorEvent>>(),
            Array.Empty<ITransitionAction<DoorState, DoorEvent>>()));

        var all = table.AllTransitions.ToList();
        Assert.Single(all);
    }
}
