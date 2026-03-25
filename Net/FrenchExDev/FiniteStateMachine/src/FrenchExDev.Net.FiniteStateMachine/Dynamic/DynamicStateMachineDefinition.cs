using System;
using System.Collections.Generic;

namespace FrenchExDev.Net.FiniteStateMachine.Dynamic;

/// <summary>
/// Immutable definition for string-based dynamic state machines.
/// </summary>
public sealed class DynamicStateMachineDefinition : IStateMachineDefinition<string, string>
{
    private readonly Dictionary<(string, string), List<TransitionDefinition<string, string>>> _lookup;

    internal DynamicStateMachineDefinition(
        string initialState,
        IReadOnlyCollection<string> states,
        IReadOnlyCollection<string> finalStates,
        IReadOnlyList<TransitionDefinition<string, string>> transitions,
        Dictionary<string, List<IStateAction<string, string>>> entryActions,
        Dictionary<string, List<IStateAction<string, string>>> exitActions)
    {
        InitialState = initialState;
        States = states;
        FinalStates = finalStates;
        Transitions = transitions;
        EntryActions = entryActions;
        ExitActions = exitActions;

        _lookup = new Dictionary<(string, string), List<TransitionDefinition<string, string>>>(StringTupleComparer.Instance);
        foreach (var t in transitions)
        {
            var key = (t.Source, t.Event);
            if (!_lookup.TryGetValue(key, out var list))
            {
                list = new List<TransitionDefinition<string, string>>();
                _lookup[key] = list;
            }
            list.Add(t);
        }
    }

    public string InitialState { get; }
    public IReadOnlyCollection<string> States { get; }
    public IReadOnlyCollection<string> FinalStates { get; }
    public IReadOnlyList<TransitionDefinition<string, string>> Transitions { get; }
    internal Dictionary<string, List<IStateAction<string, string>>> EntryActions { get; }
    internal Dictionary<string, List<IStateAction<string, string>>> ExitActions { get; }

    public IReadOnlyList<string> GetPermittedEvents(string state)
    {
        var result = new List<string>();
        foreach (var key in _lookup.Keys)
        {
            if (string.Equals(key.Item1, state, StringComparison.Ordinal))
                result.Add(key.Item2);
        }
        return result;
    }

    public bool CanFire(string from, string @event)
    {
        return _lookup.ContainsKey((from, @event));
    }

    public IReadOnlyList<IStateAction<string, string>> GetEntryActions(string state)
    {
        if (EntryActions.TryGetValue(state, out var actions))
            return actions;
        return Array.Empty<IStateAction<string, string>>();
    }

    public IReadOnlyList<IStateAction<string, string>> GetExitActions(string state)
    {
        if (ExitActions.TryGetValue(state, out var actions))
            return actions;
        return Array.Empty<IStateAction<string, string>>();
    }

    public IStateMachine<string, string> CreateMachine(ConcurrencyMode concurrency = ConcurrencyMode.None)
    {
        return new StateMachineEngine<string, string>(this, concurrency);
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string, string)>
    {
        public static readonly StringTupleComparer Instance = new StringTupleComparer();
        public bool Equals((string, string) x, (string, string) y) =>
            string.Equals(x.Item1, y.Item1, StringComparison.Ordinal) &&
            string.Equals(x.Item2, y.Item2, StringComparison.Ordinal);
        public int GetHashCode((string, string) obj) =>
            (obj.Item1?.GetHashCode() ?? 0) ^ (obj.Item2?.GetHashCode() ?? 0);
    }
}
