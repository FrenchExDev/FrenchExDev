using System;
using System.Collections.Generic;
using System.Linq;
using FrenchExDev.Net.FiniteStateMachine;

namespace FrenchExDev.Net.FiniteStateMachine.Testing;

/// <summary>
/// A path through a state machine graph: a sequence of (state, event) steps.
/// </summary>
public sealed class StateMachinePath<TState, TEvent>
{
    public StateMachinePath(IReadOnlyList<(TState State, TEvent Event)> steps, TState finalState)
    {
        Steps = steps;
        FinalState = finalState;
    }

    public IReadOnlyList<(TState State, TEvent Event)> Steps { get; }
    public TState FinalState { get; }

    public override string ToString()
    {
        var parts = Steps.Select(s => $"{s.State} --{s.Event}-->").ToList();
        parts.Add(FinalState?.ToString() ?? "?");
        return string.Join(" ", parts);
    }
}

/// <summary>
/// Generates all paths through an FSM graph for model-based testing.
/// </summary>
public static class StateMachinePathGenerator
{
    /// <summary>
    /// Enumerates all paths from the initial state to any dead-end (terminal) state.
    /// Uses DFS with cycle detection.
    /// </summary>
    public static IReadOnlyList<StateMachinePath<TState, TEvent>> AllPaths<TState, TEvent>(
        IStateMachineDefinition<TState, TEvent> definition,
        int maxDepth = 50)
    {
        var paths = new List<StateMachinePath<TState, TEvent>>();
        var comparer = EqualityComparer<TState>.Default;
        var adjacency = BuildAdjacencyMap(definition);

        // DFS
        var stack = new Stack<(TState State, List<(TState State, TEvent Event)> Path)>();
        stack.Push((definition.InitialState, new List<(TState, TEvent)>()));

        while (stack.Count > 0)
        {
            var (current, path) = stack.Pop();

            if (path.Count >= maxDepth)
                continue;

            if (HasCycle(path, current, comparer))
                continue;

            if (!adjacency.TryGetValue(current, out var edges) || edges.Count == 0)
            {
                // Terminal state — record path
                paths.Add(new StateMachinePath<TState, TEvent>(path, current));
                continue;
            }

            foreach (var (evt, target) in edges)
            {
                var newPath = new List<(TState, TEvent)>(path) { (current, evt) };
                stack.Push((target, newPath));
            }
        }

        return paths;
    }

#pragma warning disable CS8714
    private static Dictionary<TState, List<(TEvent Event, TState Target)>> BuildAdjacencyMap<TState, TEvent>(
        IStateMachineDefinition<TState, TEvent> definition)
    {
        var adjacency = new Dictionary<TState, List<(TEvent Event, TState Target)>>();
        foreach (var t in definition.Transitions)
        {
            if (!adjacency.TryGetValue(t.Source, out var list))
            {
                list = new List<(TEvent, TState)>();
                adjacency[t.Source] = list;
            }
            list.Add((t.Event, t.Target));
        }

        return adjacency;
    }
#pragma warning restore CS8714

    private static bool HasCycle<TState, TEvent>(
        List<(TState State, TEvent Event)> path,
        TState current,
        IEqualityComparer<TState> comparer)
    {
        foreach (var step in path)
        {
            if (comparer.Equals(step.State, current))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Finds the shortest path between two states using BFS.
    /// Returns null if no path exists.
    /// </summary>
    public static StateMachinePath<TState, TEvent>? ShortestPath<TState, TEvent>(
        IStateMachineDefinition<TState, TEvent> definition,
        TState from,
        TState to)
    {
        var comparer = EqualityComparer<TState>.Default;
        var visited = new HashSet<TState>(comparer);
        var queue = new Queue<(TState State, List<(TState State, TEvent Event)> Path)>();
        queue.Enqueue((from, new List<(TState, TEvent)>()));

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();

            if (comparer.Equals(current, to))
                return new StateMachinePath<TState, TEvent>(path, to);

            if (!visited.Add(current))
                continue;

            foreach (var t in definition.Transitions)
            {
                if (comparer.Equals(t.Source, current))
                {
                    var newPath = new List<(TState, TEvent)>(path) { (current, t.Event) };
                    queue.Enqueue((t.Target, newPath));
                }
            }
        }

        return null;
    }
}
