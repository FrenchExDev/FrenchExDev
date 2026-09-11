using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrenchExDev.Net.Result;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// A named region within a composite state machine.
/// Each region is an independent FSM running concurrently.
/// </summary>
public interface IRegion<TState, TEvent>
{
    string Name { get; }
    IStateMachine<TState, TEvent> Machine { get; }
}

/// <summary>
/// A composite state machine with multiple parallel regions.
/// Events can be broadcast to all regions simultaneously.
/// </summary>
public interface ICompositeStateMachine<TState, TEvent>
{
    IReadOnlyList<IRegion<TState, TEvent>> Regions { get; }
    bool AllRegionsTerminal { get; }

    Task<IReadOnlyList<Result<Transition<TState>>>> FireAllAsync(
        TEvent @event, CancellationToken ct = default);

    IRegion<TState, TEvent>? GetRegion(string name);
}
