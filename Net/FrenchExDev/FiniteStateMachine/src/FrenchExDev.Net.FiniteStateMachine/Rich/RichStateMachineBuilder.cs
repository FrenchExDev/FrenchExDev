using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FrenchExDev.Net.FiniteStateMachine.Rich;

/// <summary>
/// Fluent builder for interface-based rich state machine definitions.
/// </summary>
public sealed class RichStateMachineBuilder<TState, TEvent>
    where TState : class, IState
    where TEvent : class, IEvent
{
    private TState? _initialState;
    private readonly HashSet<Type> _finalStateTypes = new HashSet<Type>();
    private readonly List<RichTransitionDefinition<TState, TEvent>> _transitions = new List<RichTransitionDefinition<TState, TEvent>>();

    public RichStateMachineBuilder<TState, TEvent> InitialState(TState state)
    {
        _initialState = state;
        return this;
    }

    public RichStateMachineBuilder<TState, TEvent> FinalState<TFinalState>()
        where TFinalState : class, TState
    {
        _finalStateTypes.Add(typeof(TFinalState));
        return this;
    }

    public RichWhenBuilder<TState, TEvent, TSourceState> When<TSourceState>()
        where TSourceState : class, TState
    {
        return new RichWhenBuilder<TState, TEvent, TSourceState>(this);
    }

    internal void AddRichTransition(RichTransitionDefinition<TState, TEvent> transition)
    {
        _transitions.Add(transition);
    }

    public Result.Result<RichStateMachineDefinition<TState, TEvent>> Build()
    {
        if (_initialState == null)
        {
            return Result.Result<RichStateMachineDefinition<TState, TEvent>>.Failure(
                new ValidationResult("InitialState must be set"));
        }

        var definition = new RichStateMachineDefinition<TState, TEvent>(
            _initialState,
            _transitions,
            _finalStateTypes);

        return Result.Result<RichStateMachineDefinition<TState, TEvent>>.Success(definition);
    }
}
