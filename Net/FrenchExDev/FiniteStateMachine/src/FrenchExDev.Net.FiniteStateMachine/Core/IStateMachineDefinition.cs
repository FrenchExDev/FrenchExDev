using System.Collections.Generic;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Immutable, validated definition of a state machine graph.
/// Created by a builder, consumed by a runtime engine and visualization.
/// </summary>
public interface IStateMachineDefinition<TState, TEvent>
{
    TState InitialState { get; }
    IReadOnlyCollection<TState> States { get; }
    IReadOnlyCollection<TState> FinalStates { get; }
    IReadOnlyList<TransitionDefinition<TState, TEvent>> Transitions { get; }

    IReadOnlyList<TEvent> GetPermittedEvents(TState state);
    bool CanFire(TState from, TEvent @event);

    IReadOnlyList<IStateAction<TState, TEvent>> GetEntryActions(TState state);
    IReadOnlyList<IStateAction<TState, TEvent>> GetExitActions(TState state);

    IStateMachine<TState, TEvent> CreateMachine(ConcurrencyMode concurrency = ConcurrencyMode.None);
}
