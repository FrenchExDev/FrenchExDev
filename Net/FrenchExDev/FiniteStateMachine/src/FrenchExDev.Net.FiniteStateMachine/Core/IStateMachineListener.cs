using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Observer for state machine lifecycle events. All methods are async.
/// On net10.0, default interface methods provide no-op defaults.
/// On netstandard2.0, use <see cref="StateMachineListenerBase{TState,TEvent}"/>.
/// </summary>
public interface IStateMachineListener<TState, TEvent>
{
#if NET10_0_OR_GREATER
    [ExcludeFromCodeCoverage]
    Task OnTransitioningAsync(TState from, TEvent @event, TState to, CancellationToken ct) => Task.CompletedTask;
    [ExcludeFromCodeCoverage]
    Task OnTransitionedAsync(TState from, TEvent @event, TState to, CancellationToken ct) => Task.CompletedTask;
    [ExcludeFromCodeCoverage]
    Task OnStateEnteringAsync(TState state, TEvent triggeringEvent, CancellationToken ct) => Task.CompletedTask;
    [ExcludeFromCodeCoverage]
    Task OnStateEnteredAsync(TState state, TEvent triggeringEvent, CancellationToken ct) => Task.CompletedTask;
    [ExcludeFromCodeCoverage]
    Task OnStateExitingAsync(TState state, TEvent @event, CancellationToken ct) => Task.CompletedTask;
    [ExcludeFromCodeCoverage]
    Task OnStateExitedAsync(TState state, TEvent @event, CancellationToken ct) => Task.CompletedTask;
    [ExcludeFromCodeCoverage]
    Task OnTransitionDeniedAsync(TState from, TEvent @event, string reason, CancellationToken ct) => Task.CompletedTask;
    [ExcludeFromCodeCoverage]
    Task OnErrorAsync(Exception exception, TState state, CancellationToken ct) => Task.CompletedTask;
#else
    Task OnTransitioningAsync(TState from, TEvent @event, TState to, CancellationToken ct);
    Task OnTransitionedAsync(TState from, TEvent @event, TState to, CancellationToken ct);
    Task OnStateEnteringAsync(TState state, TEvent triggeringEvent, CancellationToken ct);
    Task OnStateEnteredAsync(TState state, TEvent triggeringEvent, CancellationToken ct);
    Task OnStateExitingAsync(TState state, TEvent @event, CancellationToken ct);
    Task OnStateExitedAsync(TState state, TEvent @event, CancellationToken ct);
    Task OnTransitionDeniedAsync(TState from, TEvent @event, string reason, CancellationToken ct);
    Task OnErrorAsync(Exception exception, TState state, CancellationToken ct);
#endif
}
