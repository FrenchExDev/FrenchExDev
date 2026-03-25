using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrenchExDev.Net.Result;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Manages deferred events — events queued when received in a state that doesn't handle them.
/// Replays queued events in FIFO order when transitioning to a state that handles them.
/// </summary>
public sealed class DeferredEventQueue<TState, TEvent>
{
    private readonly Queue<TEvent> _queue = new Queue<TEvent>();
    private readonly HashSet<(TState, TEvent)> _deferredPairs;
    private readonly IEqualityComparer<TState> _stateComparer;
    private readonly IEqualityComparer<TEvent> _eventComparer;
    private readonly int _maxReplayDepth;

    public DeferredEventQueue(
        IEqualityComparer<TState>? stateComparer = null,
        IEqualityComparer<TEvent>? eventComparer = null,
        int maxReplayDepth = 100)
    {
        _stateComparer = stateComparer ?? EqualityComparer<TState>.Default;
        _eventComparer = eventComparer ?? EqualityComparer<TEvent>.Default;
        _deferredPairs = new HashSet<(TState, TEvent)>(new TupleComparer(_stateComparer, _eventComparer));
        _maxReplayDepth = maxReplayDepth;
    }

    /// <summary>Register that a specific event should be deferred in a specific state.</summary>
    public void Defer(TState state, TEvent @event) => _deferredPairs.Add((state, @event));

    /// <summary>Check if the event should be deferred in the given state.</summary>
    public bool IsDeferred(TState state, TEvent @event) => _deferredPairs.Contains((state, @event));

    /// <summary>Queue an event for later replay.</summary>
    public void Enqueue(TEvent @event) => _queue.Enqueue(@event);

    /// <summary>Number of queued events.</summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Replay any queued events that the machine can now handle.
    /// Returns the number of events replayed.
    /// </summary>
    public async Task<int> ReplayAsync(
        IStateMachine<TState, TEvent> machine,
        CancellationToken ct = default)
    {
        var replayed = 0;
        var depth = 0;

        while (_queue.Count > 0 && depth < _maxReplayDepth)
        {
            var evt = _queue.Peek();

            // Skip if still deferred in current state
            if (IsDeferred(machine.CurrentState, evt))
                break;

            _queue.Dequeue();
            var result = await machine.FireAsync(evt, ct).ConfigureAwait(false);
            if (result.IsSuccess)
                replayed++;
            depth++;
        }

        return replayed;
    }

    private sealed class TupleComparer : IEqualityComparer<(TState, TEvent)>
    {
        private readonly IEqualityComparer<TState> _sc;
        private readonly IEqualityComparer<TEvent> _ec;
        public TupleComparer(IEqualityComparer<TState> sc, IEqualityComparer<TEvent> ec) { _sc = sc; _ec = ec; }
        public bool Equals((TState, TEvent) x, (TState, TEvent) y) => _sc.Equals(x.Item1, y.Item1) && _ec.Equals(x.Item2, y.Item2);
        public int GetHashCode((TState, TEvent) obj) => (_sc.GetHashCode(obj.Item1!) * 397) ^ _ec.GetHashCode(obj.Item2!);
    }
}
