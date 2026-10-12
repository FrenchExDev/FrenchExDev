using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// A long-running async service tied to a state's lifecycle.
/// Started on state entry, cancelled on state exit.
/// Completion auto-fires DoneEvent; failure auto-fires ErrorEvent.
/// </summary>
public interface IInvokedService<TEvent>
{
    string Id { get; }
    Task ExecuteAsync(CancellationToken ct);
    TEvent DoneEvent { get; }
    TEvent ErrorEvent { get; }
}
