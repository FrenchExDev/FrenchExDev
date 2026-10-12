using System;
using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Base class providing no-op defaults for all listener methods.
/// Use on netstandard2.0 where default interface methods are not available.
/// </summary>
public abstract class StateMachineListenerBase<TState, TEvent> : IStateMachineListener<TState, TEvent>
{
    public virtual Task OnTransitioningAsync(TState from, TEvent @event, TState to, CancellationToken ct) => Task.CompletedTask;
    public virtual Task OnTransitionedAsync(TState from, TEvent @event, TState to, CancellationToken ct) => Task.CompletedTask;
    public virtual Task OnStateEnteringAsync(TState state, TEvent triggeringEvent, CancellationToken ct) => Task.CompletedTask;
    public virtual Task OnStateEnteredAsync(TState state, TEvent triggeringEvent, CancellationToken ct) => Task.CompletedTask;
    public virtual Task OnStateExitingAsync(TState state, TEvent @event, CancellationToken ct) => Task.CompletedTask;
    public virtual Task OnStateExitedAsync(TState state, TEvent @event, CancellationToken ct) => Task.CompletedTask;
    public virtual Task OnTransitionDeniedAsync(TState from, TEvent @event, string reason, CancellationToken ct) => Task.CompletedTask;
    public virtual Task OnErrorAsync(Exception exception, TState state, CancellationToken ct) => Task.CompletedTask;
}
