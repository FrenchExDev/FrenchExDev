using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using FrenchExDev.Net.Result;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Shared async runtime for all FSM tiers.
/// Implements the FireAsync lifecycle: guard → exit → transition action → enter → notify.
/// </summary>
public class StateMachineEngine<TState, TEvent> : IStateMachine<TState, TEvent>
{
    private readonly IEqualityComparer<TState> _stateComparer;
    private readonly IEqualityComparer<TEvent> _eventComparer;
    private readonly List<IStateMachineListener<TState, TEvent>> _listeners = new List<IStateMachineListener<TState, TEvent>>();
    private readonly SemaphoreSlim? _semaphore;
    private readonly StateHierarchy<TState>? _hierarchy;
    private TState _currentState;

    public StateMachineEngine(
        IStateMachineDefinition<TState, TEvent> definition,
        ConcurrencyMode concurrency = ConcurrencyMode.None,
        IEqualityComparer<TState>? stateComparer = null,
        IEqualityComparer<TEvent>? eventComparer = null,
        StateHierarchy<TState>? hierarchy = null)
    {
        Definition = definition;
        _currentState = definition.InitialState;
        _stateComparer = stateComparer ?? EqualityComparer<TState>.Default;
        _eventComparer = eventComparer ?? EqualityComparer<TEvent>.Default;
        _semaphore = concurrency == ConcurrencyMode.Semaphore ? new SemaphoreSlim(1, 1) : null;
        _hierarchy = hierarchy;
    }

    public StateHierarchy<TState>? Hierarchy => _hierarchy;

    public TState CurrentState => _currentState;
    public IStateMachineDefinition<TState, TEvent> Definition { get; }

    public void AddListener(IStateMachineListener<TState, TEvent> listener)
    {
        _listeners.Add(listener);
    }

    public void RemoveListener(IStateMachineListener<TState, TEvent> listener)
    {
        _listeners.Remove(listener);
    }

    public async Task<Result<Transition<TState>>> FireAsync(TEvent @event, CancellationToken ct = default)
    {
        if (_semaphore != null)
        {
            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await FireCoreAsync(@event, ct).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        return await FireCoreAsync(@event, ct).ConfigureAwait(false);
    }

    public async Task<bool> CanFireAsync(TEvent @event, CancellationToken ct = default)
    {
        var transitions = FindTransitions(_currentState, @event);
        if (transitions.Count == 0) return false;

        foreach (var transition in transitions)
        {
            if (await EvaluateGuardsAsync(transition, ct).ConfigureAwait(false))
                return true;
        }

        return false;
    }

    public Task<IReadOnlyList<TEvent>> GetPermittedEventsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TEvent> result = Definition.GetPermittedEvents(_currentState);
        return Task.FromResult(result);
    }

    private async Task<Result<Transition<TState>>> FireCoreAsync(TEvent @event, CancellationToken ct)
    {
        var current = _currentState;
        var (matched, denialReason) = await FindMatchedTransitionAsync(current, @event, ct).ConfigureAwait(false);

        if (matched == null)
        {
            await NotifyDeniedAsync(current, @event, denialReason!, ct).ConfigureAwait(false);
            return Result<Transition<TState>>.Failure(new ValidationResult(denialReason!));
        }

        var target = matched.Target;
        var isReentrant = _stateComparer.Equals(current, target);

        // Notify: transitioning (before)
        await NotifyTransitioningAsync(current, @event, target, ct).ConfigureAwait(false);

        if (!matched.IsInternal)
        {
            await ExecuteExitAsync(current, @event, ct).ConfigureAwait(false);
        }

        // Execute transition actions
        foreach (var action in matched.Actions)
        {
            await action.ExecuteAsync(current, @event, target, ct).ConfigureAwait(false);
        }

        // Update state
        _currentState = target;

        if (!matched.IsInternal)
        {
            await ExecuteEntryAsync(target, @event, ct).ConfigureAwait(false);
        }

        // Notify: transitioned (after)
        await NotifyTransitionedAsync(current, @event, target, ct).ConfigureAwait(false);

        return Result<Transition<TState>>.Success(
            new Transition<TState>(current, target, isReentrant));
    }

    private async Task<(TransitionDefinition<TState, TEvent>? matched, string? denialReason)> FindMatchedTransitionAsync(
        TState current, TEvent @event, CancellationToken ct)
    {
        var transitions = FindTransitions(current, @event);

        if (transitions.Count == 0)
            return (null, $"No transition from '{current}' on event '{@event}'");

        foreach (var transition in transitions)
        {
            if (await EvaluateGuardsAsync(transition, ct).ConfigureAwait(false))
                return (transition, null);
        }

        return (null, $"All guards denied transition from '{current}' on event '{@event}'");
    }

    private async Task ExecuteExitAsync(TState current, TEvent @event, CancellationToken ct)
    {
        await NotifyStateExitingAsync(current, @event, ct).ConfigureAwait(false);
        foreach (var action in Definition.GetExitActions(current))
        {
            await action.ExecuteAsync(current, @event, ct).ConfigureAwait(false);
        }
        await NotifyStateExitedAsync(current, @event, ct).ConfigureAwait(false);
    }

    private async Task ExecuteEntryAsync(TState target, TEvent @event, CancellationToken ct)
    {
        await NotifyStateEnteringAsync(target, @event, ct).ConfigureAwait(false);
        foreach (var action in Definition.GetEntryActions(target))
        {
            await action.ExecuteAsync(target, @event, ct).ConfigureAwait(false);
        }
        await NotifyStateEnteredAsync(target, @event, ct).ConfigureAwait(false);
    }

    private List<TransitionDefinition<TState, TEvent>> FindTransitions(TState from, TEvent @event)
    {
        var result = new List<TransitionDefinition<TState, TEvent>>();

        // Search current state first
        foreach (var t in Definition.Transitions)
        {
            if (_stateComparer.Equals(t.Source, from) && _eventComparer.Equals(t.Event, @event))
                result.Add(t);
        }

        // If no transition found and hierarchy exists, fall back to parent states
        if (result.Count == 0 && _hierarchy != null)
        {
            FindTransitionsInHierarchy(from, @event, result);
        }

        return result;
    }

    private void FindTransitionsInHierarchy(TState from, TEvent @event, List<TransitionDefinition<TState, TEvent>> result)
    {
        var state = from;
        while (result.Count == 0 && _hierarchy!.TryGetParent(state, out var parent))
        {
            foreach (var t in Definition.Transitions)
            {
                if (_stateComparer.Equals(t.Source, parent) && _eventComparer.Equals(t.Event, @event))
                    result.Add(t);
            }
            state = parent;
        }
    }

    private async Task<bool> EvaluateGuardsAsync(TransitionDefinition<TState, TEvent> transition, CancellationToken ct)
    {
        foreach (var guard in transition.Guards)
        {
            if (!await guard.EvaluateAsync(transition.Source, transition.Event, transition.Target, ct).ConfigureAwait(false))
                return false;
        }
        return true;
    }

    private async Task NotifyTransitioningAsync(TState from, TEvent @event, TState to, CancellationToken ct)
    {
        foreach (var listener in _listeners)
            await listener.OnTransitioningAsync(from, @event, to, ct).ConfigureAwait(false);
    }

    private async Task NotifyTransitionedAsync(TState from, TEvent @event, TState to, CancellationToken ct)
    {
        foreach (var listener in _listeners)
            await listener.OnTransitionedAsync(from, @event, to, ct).ConfigureAwait(false);
    }

    private async Task NotifyStateExitingAsync(TState state, TEvent @event, CancellationToken ct)
    {
        foreach (var listener in _listeners)
            await listener.OnStateExitingAsync(state, @event, ct).ConfigureAwait(false);
    }

    private async Task NotifyStateExitedAsync(TState state, TEvent @event, CancellationToken ct)
    {
        foreach (var listener in _listeners)
            await listener.OnStateExitedAsync(state, @event, ct).ConfigureAwait(false);
    }

    private async Task NotifyStateEnteringAsync(TState state, TEvent @event, CancellationToken ct)
    {
        foreach (var listener in _listeners)
            await listener.OnStateEnteringAsync(state, @event, ct).ConfigureAwait(false);
    }

    private async Task NotifyStateEnteredAsync(TState state, TEvent @event, CancellationToken ct)
    {
        foreach (var listener in _listeners)
            await listener.OnStateEnteredAsync(state, @event, ct).ConfigureAwait(false);
    }

    private async Task NotifyDeniedAsync(TState from, TEvent @event, string reason, CancellationToken ct)
    {
        foreach (var listener in _listeners)
            await listener.OnTransitionDeniedAsync(from, @event, reason, ct).ConfigureAwait(false);
    }
}
