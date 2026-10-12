using System;

namespace FrenchExDev.Net.FiniteStateMachine.Rich;

/// <summary>
/// Builder step: configures transitions from a specific state type.
/// </summary>
public sealed class RichWhenBuilder<TState, TEvent, TSourceState>
    where TState : class, IState
    where TEvent : class, IEvent
    where TSourceState : class, TState
{
    private readonly RichStateMachineBuilder<TState, TEvent> _parent;

    internal RichWhenBuilder(RichStateMachineBuilder<TState, TEvent> parent)
    {
        _parent = parent;
    }

    public RichOnBuilder<TState, TEvent, TConcreteEvent> On<TConcreteEvent>()
        where TConcreteEvent : class, TEvent
    {
        return new RichOnBuilder<TState, TEvent, TConcreteEvent>(_parent, typeof(TSourceState));
    }
}
