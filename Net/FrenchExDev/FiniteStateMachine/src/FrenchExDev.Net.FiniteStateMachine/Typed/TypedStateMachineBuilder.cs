using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FrenchExDev.Net.FiniteStateMachine.Typed;

/// <summary>
/// Fluent builder for enum-based state machine definitions.
/// </summary>
public sealed class TypedStateMachineBuilder<TState, TEvent>
    where TState : struct, Enum
    where TEvent : struct, Enum
{
    private TState? _initialState;
    private readonly HashSet<TState> _states = new HashSet<TState>();
    private readonly HashSet<TState> _finalStates = new HashSet<TState>();
    private readonly List<TransitionDefinition<TState, TEvent>> _transitions = new List<TransitionDefinition<TState, TEvent>>();
    private readonly Dictionary<TState, List<IStateAction<TState, TEvent>>> _entryActions = new Dictionary<TState, List<IStateAction<TState, TEvent>>>();
    private readonly Dictionary<TState, List<IStateAction<TState, TEvent>>> _exitActions = new Dictionary<TState, List<IStateAction<TState, TEvent>>>();

    public TypedStateMachineBuilder<TState, TEvent> InitialState(TState state)
    {
        _initialState = state;
        _states.Add(state);
        return this;
    }

    public TypedStateMachineBuilder<TState, TEvent> FinalState(TState state)
    {
        _finalStates.Add(state);
        _states.Add(state);
        return this;
    }

    public TypedWhenBuilder<TState, TEvent> When(TState state)
    {
        _states.Add(state);
        return new TypedWhenBuilder<TState, TEvent>(this, state);
    }

    internal void AddTransition(TransitionDefinition<TState, TEvent> transition)
    {
        _transitions.Add(transition);
        _states.Add(transition.Source);
        _states.Add(transition.Target);
    }

    public Result.Result<TypedStateMachineDefinition<TState, TEvent>> Build()
    {
        var errors = new List<ValidationResult>();

        if (_initialState == null)
        {
            errors.Add(new ValidationResult("InitialState must be set"));
        }

        if (errors.Count > 0)
        {
            return Result.Result<TypedStateMachineDefinition<TState, TEvent>>.Failure(errors[0]);
        }

        var table = new TransitionTable<TState, TEvent>();
        foreach (var t in _transitions)
        {
            table.Add(t);
        }

        var definition = new TypedStateMachineDefinition<TState, TEvent>(
            _initialState!.Value,
            _states,
            _finalStates,
            table,
            _entryActions,
            _exitActions);

        return Result.Result<TypedStateMachineDefinition<TState, TEvent>>.Success(definition);
    }
}
