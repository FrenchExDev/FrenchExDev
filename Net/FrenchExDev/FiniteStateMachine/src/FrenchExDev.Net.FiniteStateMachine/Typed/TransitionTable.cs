using System;
using System.Collections.Generic;

namespace FrenchExDev.Net.FiniteStateMachine.Typed;

/// <summary>
/// Array-indexed transition lookup for enum-based FSMs.
/// Provides O(1) lookup by (stateIndex, eventIndex).
/// </summary>
public sealed class TransitionTable<TState, TEvent>
    where TState : struct, Enum
    where TEvent : struct, Enum
{
    private readonly Dictionary<(int, int), List<TransitionDefinition<TState, TEvent>>> _table
        = new Dictionary<(int, int), List<TransitionDefinition<TState, TEvent>>>();

    public void Add(TransitionDefinition<TState, TEvent> transition)
    {
        var key = (Convert.ToInt32(transition.Source), Convert.ToInt32(transition.Event));
        if (!_table.TryGetValue(key, out var list))
        {
            list = new List<TransitionDefinition<TState, TEvent>>();
            _table[key] = list;
        }
        list.Add(transition);
    }

    public IReadOnlyList<TransitionDefinition<TState, TEvent>> Find(TState from, TEvent @event)
    {
        var key = (Convert.ToInt32(from), Convert.ToInt32(@event));
        if (_table.TryGetValue(key, out var list))
            return list;
        return Array.Empty<TransitionDefinition<TState, TEvent>>();
    }

    public IReadOnlyList<TEvent> GetPermittedEvents(TState from)
    {
        var fromInt = Convert.ToInt32(from);
        var result = new List<TEvent>();
        foreach (var key in _table.Keys)
        {
            if (key.Item1 == fromInt)
                result.Add((TEvent)Enum.ToObject(typeof(TEvent), key.Item2));
        }
        return result;
    }

    public IEnumerable<TransitionDefinition<TState, TEvent>> AllTransitions
    {
        get
        {
            foreach (var list in _table.Values)
                foreach (var t in list)
                    yield return t;
        }
    }
}
