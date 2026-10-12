using System;
using System.Collections.Generic;

namespace FrenchExDev.Net.FiniteStateMachine.Rich;

/// <summary>
/// Builder step: configures what happens when a specific event type is received.
/// </summary>
public sealed class RichOnBuilder<TState, TEvent, TConcreteEvent>
    where TState : class, IState
    where TEvent : class, IEvent
    where TConcreteEvent : class, TEvent
{
    private readonly RichStateMachineBuilder<TState, TEvent> _parent;
    private readonly Type _sourceType;
    private Func<TEvent, TState, TState>? _targetFactory;
    private Type? _targetType;
    private readonly List<Func<TEvent, TState, bool>> _guards = new List<Func<TEvent, TState, bool>>();

    internal RichOnBuilder(RichStateMachineBuilder<TState, TEvent> parent, Type sourceType)
    {
        _parent = parent;
        _sourceType = sourceType;
    }

    public RichOnBuilder<TState, TEvent, TConcreteEvent> TransitionTo<TTargetState>(
        Func<TConcreteEvent, TState, TTargetState> factory)
        where TTargetState : class, TState
    {
        _targetType = typeof(TTargetState);
        _targetFactory = (evt, current) => factory((TConcreteEvent)evt, current);
        return this;
    }

    public RichOnBuilder<TState, TEvent, TConcreteEvent> WithGuard(
        Func<TConcreteEvent, TState, bool> guard)
    {
        _guards.Add((evt, state) => guard((TConcreteEvent)evt, state));
        return this;
    }

    public RichStateMachineBuilder<TState, TEvent> Done()
    {
        Flush();
        return _parent;
    }

    public RichWhenBuilder<TState, TEvent, TSource> When<TSource>()
        where TSource : class, TState
    {
        Flush();
        return _parent.When<TSource>();
    }

    public Result.Result<RichStateMachineDefinition<TState, TEvent>> Build()
    {
        Flush();
        return _parent.Build();
    }

    internal void Flush()
    {
        if (_targetType == null || _targetFactory == null) return;

        _parent.AddRichTransition(new RichTransitionDefinition<TState, TEvent>(
            _sourceType,
            typeof(TConcreteEvent),
            _targetType,
            _targetFactory,
            new List<Func<TEvent, TState, bool>>(_guards)));

        _targetType = null;
        _targetFactory = null;
        _guards.Clear();
    }
}
