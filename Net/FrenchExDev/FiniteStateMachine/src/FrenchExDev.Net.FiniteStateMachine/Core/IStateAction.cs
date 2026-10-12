using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// An action executed on state entry or exit.
/// </summary>
public interface IStateAction<TState, TEvent>
{
    Task ExecuteAsync(TState state, TEvent @event, CancellationToken ct);
}
