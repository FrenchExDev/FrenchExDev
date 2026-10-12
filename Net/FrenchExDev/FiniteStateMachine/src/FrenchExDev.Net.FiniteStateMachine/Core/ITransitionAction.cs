using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// An action executed during a state transition (between exit and entry).
/// </summary>
public interface ITransitionAction<TState, TEvent>
{
    Task ExecuteAsync(TState from, TEvent @event, TState to, CancellationToken ct);
}
