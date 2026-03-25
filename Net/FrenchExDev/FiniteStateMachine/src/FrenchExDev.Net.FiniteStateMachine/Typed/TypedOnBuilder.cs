using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine.Typed;

/// <summary>
/// Builder step: configures what happens when an event is received in a specific state.
/// </summary>
public sealed class TypedOnBuilder<TState, TEvent>
    where TState : struct, Enum
    where TEvent : struct, Enum
{
    private readonly TypedStateMachineBuilder<TState, TEvent> _parent;
    private readonly TState _source;
    private readonly TEvent _event;
    private readonly List<IGuard<TState, TEvent>> _guards = new List<IGuard<TState, TEvent>>();
    private readonly List<ITransitionAction<TState, TEvent>> _actions = new List<ITransitionAction<TState, TEvent>>();
    private TState _target;
    private bool _isInternal;
    private bool _targetSet;

    internal TypedOnBuilder(TypedStateMachineBuilder<TState, TEvent> parent, TState source, TEvent @event)
    {
        _parent = parent;
        _source = source;
        _event = @event;
    }

    public TypedOnBuilder<TState, TEvent> TransitionTo(TState target)
    {
        _target = target;
        _targetSet = true;
        return this;
    }

    public TypedOnBuilder<TState, TEvent> InternalTransition()
    {
        _target = _source;
        _targetSet = true;
        _isInternal = true;
        return this;
    }

    public TypedOnBuilder<TState, TEvent> WithGuard(IGuard<TState, TEvent> guard)
    {
        _guards.Add(guard);
        return this;
    }

    public TypedOnBuilder<TState, TEvent> WithGuard(Func<TState, TEvent, TState, CancellationToken, Task<bool>> guard)
    {
        _guards.Add(new DelegateGuard<TState, TEvent>(guard));
        return this;
    }

    public TypedOnBuilder<TState, TEvent> WithAction(ITransitionAction<TState, TEvent> action)
    {
        _actions.Add(action);
        return this;
    }

    public TypedOnBuilder<TState, TEvent> WithAction(Func<TState, TEvent, TState, CancellationToken, Task> action)
    {
        _actions.Add(new DelegateTransitionAction<TState, TEvent>(action));
        return this;
    }

    /// <summary>Start configuring another event for the same source state.</summary>
    public TypedOnBuilder<TState, TEvent> On(TEvent @event)
    {
        Flush();
        return new TypedOnBuilder<TState, TEvent>(_parent, _source, @event);
    }

    /// <summary>Start configuring a different source state.</summary>
    public TypedWhenBuilder<TState, TEvent> When(TState state)
    {
        Flush();
        return _parent.When(state);
    }

    /// <summary>Build the definition.</summary>
    public FrenchExDev.Net.Result.Result<TypedStateMachineDefinition<TState, TEvent>> Build()
    {
        Flush();
        return _parent.Build();
    }

    internal void Flush()
    {
        if (!_targetSet) return;

        _parent.AddTransition(new TransitionDefinition<TState, TEvent>(
            _source, _event, _target, _guards, _actions, _isInternal));
        _targetSet = false;
    }
}

