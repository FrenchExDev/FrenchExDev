using System.Collections.Generic;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Defines a parent-child relationship between states.
/// </summary>
public sealed class HierarchyDefinition<TState>
{
    public HierarchyDefinition(TState parent, TState child)
    {
        Parent = parent;
        Child = child;
    }

    public TState Parent { get; }
    public TState Child { get; }
}

/// <summary>
/// History mode for re-entering a parent state.
/// </summary>
public enum HistoryMode
{
    /// <summary>No history — always enter the initial sub-state.</summary>
    None,

    /// <summary>Shallow history — resume at the last direct child state.</summary>
    Shallow,

    /// <summary>Deep history — resume at the last leaf state.</summary>
    Deep
}

/// <summary>
/// Manages state hierarchy (parent/child relationships) for hierarchical state machines.
/// </summary>
#pragma warning disable CS8714 // TState may be nullable but Dictionary requires notnull — safe because FSM states are never null
public sealed class StateHierarchy<TState>
{
    private readonly Dictionary<TState, TState> _parentOf;
    private readonly Dictionary<TState, List<TState>> _childrenOf;
    private readonly Dictionary<TState, TState> _initialChildOf;
    private readonly IEqualityComparer<TState> _comparer;

    public StateHierarchy(IEqualityComparer<TState>? comparer = null)
    {
        _comparer = comparer ?? EqualityComparer<TState>.Default;
        _parentOf = new Dictionary<TState, TState>(_comparer);
        _childrenOf = new Dictionary<TState, List<TState>>(_comparer);
        _initialChildOf = new Dictionary<TState, TState>(_comparer);
    }
#pragma warning restore CS8714

    public void AddChild(TState parent, TState child, bool isInitial = false)
    {
        _parentOf[child] = parent;
        if (!_childrenOf.TryGetValue(parent, out var children))
        {
            children = new List<TState>();
            _childrenOf[parent] = children;
        }
        children.Add(child);

        if (isInitial)
            _initialChildOf[parent] = child;
    }

    public bool HasHierarchy => _parentOf.Count > 0;

    public bool TryGetParent(TState state, out TState parent)
    {
        return _parentOf.TryGetValue(state, out parent!);
    }

    public IReadOnlyList<TState> GetChildren(TState state)
    {
        if (_childrenOf.TryGetValue(state, out var children))
            return children;
        return System.Array.Empty<TState>();
    }

    public bool TryGetInitialChild(TState parentState, out TState child)
    {
        return _initialChildOf.TryGetValue(parentState, out child!);
    }

    /// <summary>
    /// Returns true if <paramref name="current"/> equals <paramref name="query"/>
    /// or is a descendant of <paramref name="query"/>.
    /// </summary>
    public bool IsInState(TState current, TState query)
    {
        if (_comparer.Equals(current, query))
            return true;

        var state = current;
        while (_parentOf.TryGetValue(state, out var parent))
        {
            if (_comparer.Equals(parent, query))
                return true;
            state = parent;
        }
        return false;
    }

    /// <summary>
    /// Computes the Least Common Ancestor of two states.
    /// Returns false if they share no ancestor.
    /// </summary>
    public bool TryGetLCA(TState a, TState b, out TState lca)
    {
        var ancestorsOfA = new HashSet<TState>(_comparer);
        var state = a;
        ancestorsOfA.Add(state);
        while (_parentOf.TryGetValue(state, out var parent))
        {
            ancestorsOfA.Add(parent);
            state = parent;
        }

        state = b;
        if (ancestorsOfA.Contains(state))
        {
            lca = state;
            return true;
        }
        while (_parentOf.TryGetValue(state, out var parent))
        {
            if (ancestorsOfA.Contains(parent))
            {
                lca = parent;
                return true;
            }
            state = parent;
        }

        lca = default!;
        return false;
    }
}
