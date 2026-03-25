using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrenchExDev.Net.Result;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// A running state machine instance.
/// </summary>
public interface IStateMachine<TState, TEvent>
{
    TState CurrentState { get; }
    IStateMachineDefinition<TState, TEvent> Definition { get; }

    Task<Result<Transition<TState>>> FireAsync(TEvent @event, CancellationToken ct = default);
    Task<bool> CanFireAsync(TEvent @event, CancellationToken ct = default);
    Task<IReadOnlyList<TEvent>> GetPermittedEventsAsync(CancellationToken ct = default);

    void AddListener(IStateMachineListener<TState, TEvent> listener);
    void RemoveListener(IStateMachineListener<TState, TEvent> listener);
}
