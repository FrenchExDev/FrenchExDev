using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenchExDev.Net.FiniteStateMachine.Typed;

/// <summary>
/// Immutable definition for enum-based state machines.
/// Uses <see cref="TransitionTable{TState,TEvent}"/> for O(1) lookup.
/// </summary>
public sealed class TypedStateMachineDefinition<TState, TEvent> : IStateMachineDefinition<TState, TEvent>
    where TState : struct, Enum
    where TEvent : struct, Enum
{
    private readonly TransitionTable<TState, TEvent> _table;
    private readonly Dictionary<TState, List<IStateAction<TState, TEvent>>> _entryActions;
    private readonly Dictionary<TState, List<IStateAction<TState, TEvent>>> _exitActions;

    public TypedStateMachineDefinition(
        TState initialState,
        IReadOnlyCollection<TState> states,
        IReadOnlyCollection<TState> finalStates,
        TransitionTable<TState, TEvent> table,
        Dictionary<TState, List<IStateAction<TState, TEvent>>> entryActions,
        Dictionary<TState, List<IStateAction<TState, TEvent>>> exitActions)
    {
        InitialState = initialState;
        States = states;
        FinalStates = finalStates;
        _table = table;
        _entryActions = entryActions;
        _exitActions = exitActions;
    }

    public TState InitialState { get; }
    public IReadOnlyCollection<TState> States { get; }
    public IReadOnlyCollection<TState> FinalStates { get; }

    public IReadOnlyList<TransitionDefinition<TState, TEvent>> Transitions
    {
        get
        {
            var list = new List<TransitionDefinition<TState, TEvent>>();
            foreach (var t in _table.AllTransitions)
                list.Add(t);
            return list;
        }
    }

    public IReadOnlyList<TEvent> GetPermittedEvents(TState state) => _table.GetPermittedEvents(state);

    public bool CanFire(TState from, TEvent @event) => _table.Find(from, @event).Count > 0;

    public IReadOnlyList<IStateAction<TState, TEvent>> GetEntryActions(TState state)
    {
        if (_entryActions.TryGetValue(state, out var actions))
            return actions;
        return Array.Empty<IStateAction<TState, TEvent>>();
    }

    public IReadOnlyList<IStateAction<TState, TEvent>> GetExitActions(TState state)
    {
        if (_exitActions.TryGetValue(state, out var actions))
            return actions;
        return Array.Empty<IStateAction<TState, TEvent>>();
    }

    public IStateMachine<TState, TEvent> CreateMachine(ConcurrencyMode concurrency = ConcurrencyMode.None)
    {
        return new StateMachineEngine<TState, TEvent>(this, concurrency);
    }
}
