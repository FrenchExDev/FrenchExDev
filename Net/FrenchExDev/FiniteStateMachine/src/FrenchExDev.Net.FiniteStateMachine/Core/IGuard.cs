using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Evaluates whether a transition is allowed.
/// </summary>
public interface IGuard<TState, TEvent>
{
    Task<bool> EvaluateAsync(TState from, TEvent @event, TState to, CancellationToken ct);
}
