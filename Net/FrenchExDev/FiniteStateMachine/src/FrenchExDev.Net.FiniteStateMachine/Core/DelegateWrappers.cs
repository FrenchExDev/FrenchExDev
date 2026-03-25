using System;
using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine;

internal sealed class DelegateGuard<TState, TEvent> : IGuard<TState, TEvent>
{
    private readonly Func<TState, TEvent, TState, CancellationToken, Task<bool>> _func;
    public DelegateGuard(Func<TState, TEvent, TState, CancellationToken, Task<bool>> func) => _func = func;
    public Task<bool> EvaluateAsync(TState from, TEvent @event, TState to, CancellationToken ct) => _func(from, @event, to, ct);
}

internal sealed class DelegateTransitionAction<TState, TEvent> : ITransitionAction<TState, TEvent>
{
    private readonly Func<TState, TEvent, TState, CancellationToken, Task> _func;
    public DelegateTransitionAction(Func<TState, TEvent, TState, CancellationToken, Task> func) => _func = func;
    public Task ExecuteAsync(TState from, TEvent @event, TState to, CancellationToken ct) => _func(from, @event, to, ct);
}
