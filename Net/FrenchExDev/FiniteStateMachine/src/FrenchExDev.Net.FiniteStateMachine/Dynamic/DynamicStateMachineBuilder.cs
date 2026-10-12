using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FrenchExDev.Net.FiniteStateMachine.Dynamic;

/// <summary>
/// Fluent builder for string-based dynamic state machine definitions.
/// </summary>
public sealed class DynamicStateMachineBuilder
{
    private string? _initialState;
    private readonly HashSet<string> _states = new HashSet<string>();
    private readonly HashSet<string> _finalStates = new HashSet<string>();
    private readonly List<TransitionDefinition<string, string>> _transitions = new List<TransitionDefinition<string, string>>();
    private readonly Dictionary<string, List<IStateAction<string, string>>> _entryActions = new Dictionary<string, List<IStateAction<string, string>>>();
    private readonly Dictionary<string, List<IStateAction<string, string>>> _exitActions = new Dictionary<string, List<IStateAction<string, string>>>();

    public DynamicStateMachineBuilder InitialState(string state)
    {
        _initialState = state;
        _states.Add(state);
        return this;
    }

    public DynamicStateMachineBuilder FinalState(string state)
    {
        _finalStates.Add(state);
        _states.Add(state);
        return this;
    }

    public DynamicWhenBuilder When(string state)
    {
        _states.Add(state);
        return new DynamicWhenBuilder(this, state);
    }

    internal void AddTransition(TransitionDefinition<string, string> transition)
    {
        _transitions.Add(transition);
        _states.Add(transition.Source);
        _states.Add(transition.Target);
    }

    public Result.Result<DynamicStateMachineDefinition> Build()
    {
        var errors = new List<ValidationResult>();

        if (_initialState == null)
        {
            errors.Add(new ValidationResult("InitialState must be set"));
        }

        if (errors.Count > 0)
        {
            return Result.Result<DynamicStateMachineDefinition>.Failure(errors[0]);
        }

        var definition = new DynamicStateMachineDefinition(
            _initialState!,
            _states,
            _finalStates,
            _transitions,
            _entryActions,
            _exitActions);

        return Result.Result<DynamicStateMachineDefinition>.Success(definition);
    }
}
