using System;
using System.Collections.Generic;

namespace FrenchExDev.Net.FiniteStateMachine.Rich;

/// <summary>
/// A transition definition for rich FSMs where the target state
/// can be computed from the event payload.
/// </summary>
public sealed class RichTransitionDefinition<TState, TEvent>
    where TState : class, IState
    where TEvent : class, IEvent
{
    public RichTransitionDefinition(
        Type sourceType,
        Type eventType,
        Type targetType,
        Func<TEvent, TState, TState>? targetFactory,
        IReadOnlyList<Func<TEvent, TState, bool>> guards)
    {
        SourceType = sourceType;
        EventType = eventType;
        TargetType = targetType;
        TargetFactory = targetFactory;
        Guards = guards;
    }

    public Type SourceType { get; }
    public Type EventType { get; }
    public Type TargetType { get; }
    public Func<TEvent, TState, TState>? TargetFactory { get; }
    public IReadOnlyList<Func<TEvent, TState, bool>> Guards { get; }
}
