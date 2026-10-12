using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrenchExDev.Net.FiniteStateMachine;

namespace FrenchExDev.Net.FiniteStateMachine.Testing;

/// <summary>
/// Test assertion helpers for state machines.
/// </summary>
public static class StateMachineAssert
{
    /// <summary>
    /// Fires an event and asserts the transition succeeds, landing on the expected state.
    /// </summary>
    public static async Task TransitionsToAsync<TState, TEvent>(
        IStateMachine<TState, TEvent> machine,
        TEvent @event,
        TState expectedState,
        IEqualityComparer<TState>? comparer = null,
        CancellationToken ct = default)
    {
        comparer ??= EqualityComparer<TState>.Default;
        var result = await machine.FireAsync(@event, ct).ConfigureAwait(false);

        if (result.IsFailure)
            throw new StateMachineAssertException($"Expected transition on '{@event}' to succeed, but it was denied.");

        if (!comparer.Equals(machine.CurrentState, expectedState))
            throw new StateMachineAssertException(
                $"Expected state '{expectedState}' after firing '{@event}', but got '{machine.CurrentState}'.");
    }

    /// <summary>
    /// Fires a sequence of events and asserts each succeeds, ending on the expected final state.
    /// </summary>
    public static async Task PathReachesAsync<TState, TEvent>(
        IStateMachine<TState, TEvent> machine,
        IEnumerable<TEvent> events,
        TState expectedFinalState,
        IEqualityComparer<TState>? comparer = null,
        CancellationToken ct = default)
    {
        comparer ??= EqualityComparer<TState>.Default;
        var step = 0;

        foreach (var evt in events)
        {
            var result = await machine.FireAsync(evt, ct).ConfigureAwait(false);
            step++;

            if (result.IsFailure)
                throw new StateMachineAssertException(
                    $"Path failed at step {step} (event '{evt}'): transition denied from '{machine.CurrentState}'.");
        }

        if (!comparer.Equals(machine.CurrentState, expectedFinalState))
            throw new StateMachineAssertException(
                $"Expected final state '{expectedFinalState}' after {step} steps, but got '{machine.CurrentState}'.");
    }

    /// <summary>
    /// Asserts that a specific event is denied (transition fails) in the current state.
    /// </summary>
    public static async Task IsDeniedAsync<TState, TEvent>(
        IStateMachine<TState, TEvent> machine,
        TEvent @event,
        CancellationToken ct = default)
    {
        var result = await machine.FireAsync(@event, ct).ConfigureAwait(false);

        if (result.IsSuccess)
            throw new StateMachineAssertException(
                $"Expected event '{@event}' to be denied in state '{machine.CurrentState}', but it succeeded.");
    }
}

public sealed class StateMachineAssertException : System.Exception
{
    public StateMachineAssertException(string message) : base(message) { }
}
