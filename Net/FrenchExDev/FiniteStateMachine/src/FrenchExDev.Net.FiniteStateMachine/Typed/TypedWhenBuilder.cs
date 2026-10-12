using System;

namespace FrenchExDev.Net.FiniteStateMachine.Typed;

/// <summary>
/// Builder step: configures transitions from a specific source state.
/// </summary>
public sealed class TypedWhenBuilder<TState, TEvent>
    where TState : struct, Enum
    where TEvent : struct, Enum
{
    private readonly TypedStateMachineBuilder<TState, TEvent> _parent;
    private readonly TState _source;

    internal TypedWhenBuilder(TypedStateMachineBuilder<TState, TEvent> parent, TState source)
    {
        _parent = parent;
        _source = source;
    }

    public TypedOnBuilder<TState, TEvent> On(TEvent @event)
    {
        return new TypedOnBuilder<TState, TEvent>(_parent, _source, @event);
    }
}
