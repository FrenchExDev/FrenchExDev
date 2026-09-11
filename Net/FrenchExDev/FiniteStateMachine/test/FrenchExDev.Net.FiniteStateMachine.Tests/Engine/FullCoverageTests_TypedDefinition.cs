using FrenchExDev.Net.FiniteStateMachine.Tests.Typed;
using FrenchExDev.Net.FiniteStateMachine.Typed;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for TypedStateMachineDefinition.
/// </summary>
public class FullCoverageTests_TypedDefinition
{
    [Fact]
    public async Task TypedDefinition_GetEntryActions_ReturnsRegisteredActions()
    {
        var entryCalled = false;
        var entryAction = new DelegateStateAction<DoorState, DoorEvent>((s, e, ct) =>
        {
            entryCalled = true;
            return Task.CompletedTask;
        });

        var table = new TransitionTable<DoorState, DoorEvent>();
        table.Add(new TransitionDefinition<DoorState, DoorEvent>(
            DoorState.Closed, DoorEvent.Open, DoorState.Open,
            Array.Empty<IGuard<DoorState, DoorEvent>>(),
            Array.Empty<ITransitionAction<DoorState, DoorEvent>>()));

        var entryActions = new Dictionary<DoorState, List<IStateAction<DoorState, DoorEvent>>>
        {
            { DoorState.Open, new List<IStateAction<DoorState, DoorEvent>> { entryAction } }
        };

        var definition = new TypedStateMachineDefinition<DoorState, DoorEvent>(
            DoorState.Closed,
            new HashSet<DoorState> { DoorState.Closed, DoorState.Open },
            new HashSet<DoorState>(),
            table,
            entryActions,
            new Dictionary<DoorState, List<IStateAction<DoorState, DoorEvent>>>());

        // Verify GetEntryActions returns the action
        var actions = definition.GetEntryActions(DoorState.Open);
        Assert.Single(actions);

        // Verify it executes via engine
        var machine = definition.CreateMachine();
        await machine.FireAsync(DoorEvent.Open);
        Assert.True(entryCalled);
    }

    [Fact]
    public async Task TypedDefinition_GetExitActions_ReturnsRegisteredActions()
    {
        var exitCalled = false;
        var exitAction = new DelegateStateAction<DoorState, DoorEvent>((s, e, ct) =>
        {
            exitCalled = true;
            return Task.CompletedTask;
        });

        var table = new TransitionTable<DoorState, DoorEvent>();
        table.Add(new TransitionDefinition<DoorState, DoorEvent>(
            DoorState.Closed, DoorEvent.Open, DoorState.Open,
            Array.Empty<IGuard<DoorState, DoorEvent>>(),
            Array.Empty<ITransitionAction<DoorState, DoorEvent>>()));

        var exitActions = new Dictionary<DoorState, List<IStateAction<DoorState, DoorEvent>>>
        {
            { DoorState.Closed, new List<IStateAction<DoorState, DoorEvent>> { exitAction } }
        };

        var definition = new TypedStateMachineDefinition<DoorState, DoorEvent>(
            DoorState.Closed,
            new HashSet<DoorState> { DoorState.Closed, DoorState.Open },
            new HashSet<DoorState>(),
            table,
            new Dictionary<DoorState, List<IStateAction<DoorState, DoorEvent>>>(),
            exitActions);

        var machine = definition.CreateMachine();
        await machine.FireAsync(DoorEvent.Open);
        Assert.True(exitCalled);
    }

    [Fact]
    public void TypedDefinition_GetEntryActions_NoActionsForState_ReturnsEmpty()
    {
        var table = new TransitionTable<DoorState, DoorEvent>();
        var definition = new TypedStateMachineDefinition<DoorState, DoorEvent>(
            DoorState.Closed,
            new HashSet<DoorState> { DoorState.Closed },
            new HashSet<DoorState>(),
            table,
            new Dictionary<DoorState, List<IStateAction<DoorState, DoorEvent>>>(),
            new Dictionary<DoorState, List<IStateAction<DoorState, DoorEvent>>>());

        Assert.Empty(definition.GetEntryActions(DoorState.Open));
        Assert.Empty(definition.GetExitActions(DoorState.Open));
    }

    // ═══════════════════════════════════════════════════════
    // Helper types
    // ═══════════════════════════════════════════════════════

    private sealed class DelegateStateAction<TState, TEvent> : IStateAction<TState, TEvent>
    {
        private readonly Func<TState, TEvent, CancellationToken, Task> _func;
        public DelegateStateAction(Func<TState, TEvent, CancellationToken, Task> func) => _func = func;
        public Task ExecuteAsync(TState state, TEvent @event, CancellationToken ct) => _func(state, @event, ct);
    }
}
