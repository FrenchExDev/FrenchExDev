using System.Collections.Generic;
using System.Linq;

namespace FrenchExDev.Net.FiniteStateMachine.Graph;

/// <summary>
/// Static introspection of an FSM graph. Computes reachable, unreachable, and dead-end states.
/// </summary>
public sealed class StateGraph<TState, TEvent>
{
    private readonly List<(TState From, TEvent Event, TState To)> _transitions = new List<(TState, TEvent, TState)>();
    private readonly IEqualityComparer<TState> _stateComparer;

    public StateGraph(TState initialState, IEqualityComparer<TState>? stateComparer = null)
    {
        InitialState = initialState;
        _stateComparer = stateComparer ?? EqualityComparer<TState>.Default;
    }

    public TState InitialState { get; }

    public void AddTransition(TState from, TEvent @event, TState to)
    {
        _transitions.Add((from, @event, to));
    }

    public IReadOnlyCollection<TState> AllStates
    {
        get
        {
            var set = new HashSet<TState>(_stateComparer);
            set.Add(InitialState);
            foreach (var (from, _, to) in _transitions)
            {
                set.Add(from);
                set.Add(to);
            }
            return set;
        }
    }

    public IReadOnlyCollection<TEvent> AllEvents
    {
        get
        {
            var set = new HashSet<TEvent>();
            foreach (var (_, evt, _) in _transitions)
                set.Add(evt);
            return set;
        }
    }

    public IReadOnlyCollection<TState> ReachableStates
    {
        get
        {
            var visited = new HashSet<TState>(_stateComparer) { InitialState };
            var queue = new Queue<TState>();
            queue.Enqueue(InitialState);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var (from, _, to) in _transitions)
                {
                    if (_stateComparer.Equals(from, current) && visited.Add(to))
                        queue.Enqueue(to);
                }
            }
            return visited;
        }
    }

    public IReadOnlyCollection<TState> UnreachableStates
    {
        get
        {
            var all = AllStates;
            var reachable = ReachableStates;
            var result = new HashSet<TState>(_stateComparer);
            foreach (var s in all)
            {
                if (!reachable.Contains(s))
                    result.Add(s);
            }
            return result;
        }
    }

    public IReadOnlyCollection<TState> DeadEndStates
    {
        get
        {
            var hasOutgoing = new HashSet<TState>(_stateComparer);
            foreach (var (from, _, _) in _transitions)
                hasOutgoing.Add(from);

            var result = new HashSet<TState>(_stateComparer);
            foreach (var s in AllStates)
            {
                if (!hasOutgoing.Contains(s))
                    result.Add(s);
            }
            return result;
        }
    }

    public IReadOnlyList<TEvent> GetPermittedEvents(TState from)
    {
        var result = new List<TEvent>();
        foreach (var (f, evt, _) in _transitions)
        {
            if (_stateComparer.Equals(f, from))
                result.Add(evt);
        }
        return result;
    }

    public IReadOnlyCollection<TState> GetReachableFrom(TState from)
    {
        var visited = new HashSet<TState>(_stateComparer);
        var queue = new Queue<TState>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var (f, _, to) in _transitions)
            {
                if (_stateComparer.Equals(f, current) && visited.Add(to))
                    queue.Enqueue(to);
            }
        }
        return visited;
    }

    public IReadOnlyList<(TState From, TEvent Event, TState To)> Transitions => _transitions;
}
