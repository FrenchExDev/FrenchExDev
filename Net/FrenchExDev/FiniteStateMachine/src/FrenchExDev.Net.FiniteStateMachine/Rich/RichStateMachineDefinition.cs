using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using FrenchExDev.Net.Result;

namespace FrenchExDev.Net.FiniteStateMachine.Rich;

/// <summary>
/// Definition and runtime for interface-based rich FSMs.
/// Uses type-discriminated matching for transitions.
/// </summary>
public sealed class RichStateMachineDefinition<TState, TEvent> : IStateMachine<TState, TEvent>
    where TState : class, IState
    where TEvent : class, IEvent
{
    private readonly IReadOnlyList<RichTransitionDefinition<TState, TEvent>> _richTransitions;
    private readonly List<IStateMachineListener<TState, TEvent>> _listeners = new List<IStateMachineListener<TState, TEvent>>();
    private TState _currentState;

    internal RichStateMachineDefinition(
        TState initialState,
        IReadOnlyList<RichTransitionDefinition<TState, TEvent>> transitions,
        HashSet<Type> finalStateTypes)
    {
        _currentState = initialState;
        _richTransitions = transitions;
        FinalStateTypes = finalStateTypes;
    }

    // IStateMachine<TState, TEvent> — not a definition+machine split for Rich tier.
    // The Rich tier is simpler: the definition IS the machine (computed target states require instance).
    IStateMachineDefinition<TState, TEvent> IStateMachine<TState, TEvent>.Definition =>
        throw new NotSupportedException("Rich FSMs do not expose a separate definition. Use the machine directly.");

    public TState CurrentState => _currentState;
    internal HashSet<Type> FinalStateTypes { get; }

    public void AddListener(IStateMachineListener<TState, TEvent> listener) => _listeners.Add(listener);
    public void RemoveListener(IStateMachineListener<TState, TEvent> listener) => _listeners.Remove(listener);

    public async Task<Result<Transition<TState>>> FireAsync(TEvent @event, CancellationToken ct = default)
    {
        var current = _currentState;
        var matched = FindMatchingTransition(current, @event);

        if (matched == null)
        {
            var reason = $"No transition from '{current.Name}' on event '{@event.Name}'";
            foreach (var l in _listeners)
                await l.OnTransitionDeniedAsync(current, @event, reason, ct).ConfigureAwait(false);
            return Result<Transition<TState>>.Failure(new ValidationResult(reason));
        }

        // Compute target state
        var target = matched.TargetFactory!(@event, current);
        var isReentrant = ReferenceEquals(current, target);

        // Lifecycle
        foreach (var l in _listeners)
            await l.OnTransitioningAsync(current, @event, target, ct).ConfigureAwait(false);

        foreach (var l in _listeners)
            await l.OnStateExitingAsync(current, @event, ct).ConfigureAwait(false);

        _currentState = target;

        foreach (var l in _listeners)
            await l.OnStateEnteredAsync(target, @event, ct).ConfigureAwait(false);

        foreach (var l in _listeners)
            await l.OnTransitionedAsync(current, @event, target, ct).ConfigureAwait(false);

        return Result<Transition<TState>>.Success(new Transition<TState>(current, target, isReentrant));
    }

    public Task<bool> CanFireAsync(TEvent @event, CancellationToken ct = default)
    {
        var current = _currentState;
        return Task.FromResult(FindMatchingTransition(current, @event) != null);
    }

    private RichTransitionDefinition<TState, TEvent>? FindMatchingTransition(TState current, TEvent @event)
    {
        foreach (var t in _richTransitions)
        {
            if (!t.SourceType.IsInstanceOfType(current) || !t.EventType.IsInstanceOfType(@event))
                continue;

            var allPass = true;
            foreach (var guard in t.Guards)
            {
                if (!guard(@event, current))
                {
                    allPass = false;
                    break;
                }
            }

            if (allPass)
                return t;
        }

        return null;
    }

    public Task<IReadOnlyList<TEvent>> GetPermittedEventsAsync(CancellationToken ct = default)
    {
        // Rich tier can't enumerate events without instances — return empty
        IReadOnlyList<TEvent> empty = Array.Empty<TEvent>();
        return Task.FromResult(empty);
    }
}
