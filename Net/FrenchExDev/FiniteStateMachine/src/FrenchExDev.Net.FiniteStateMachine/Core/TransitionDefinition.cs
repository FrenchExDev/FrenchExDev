using System.Collections.Generic;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Immutable definition of a single transition in the state machine graph.
/// </summary>
public sealed class TransitionDefinition<TState, TEvent>
{
    public TransitionDefinition(
        TState source,
        TEvent @event,
        TState target,
        IReadOnlyList<IGuard<TState, TEvent>> guards,
        IReadOnlyList<ITransitionAction<TState, TEvent>> actions,
        bool isInternal = false)
    {
        Source = source;
        Event = @event;
        Target = target;
        Guards = guards;
        Actions = actions;
        IsInternal = isInternal;
    }

    public TState Source { get; }
    public TEvent Event { get; }
    public TState Target { get; }
    public IReadOnlyList<IGuard<TState, TEvent>> Guards { get; }
    public IReadOnlyList<ITransitionAction<TState, TEvent>> Actions { get; }

    /// <summary>
    /// Internal transitions execute actions but do NOT trigger exit/entry.
    /// </summary>
    public bool IsInternal { get; }
}
