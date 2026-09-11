using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Opt-in history tracking implemented as a listener.
/// Records transitions in a ring buffer with configurable max size.
/// </summary>
public sealed class HistoryListener<TState, TEvent> : StateMachineListenerBase<TState, TEvent>
{
    private readonly List<TransitionRecord<TState, TEvent>> _records = new List<TransitionRecord<TState, TEvent>>();
    private readonly int _maxRecords;

    public HistoryListener(int maxRecords = int.MaxValue)
    {
        _maxRecords = maxRecords;
    }

    public IReadOnlyList<TransitionRecord<TState, TEvent>> Records => _records;

    public override Task OnTransitionedAsync(TState from, TEvent @event, TState to, CancellationToken ct)
    {
        var record = new TransitionRecord<TState, TEvent>(from, @event, to, DateTimeOffset.UtcNow);

        if (_records.Count >= _maxRecords)
        {
            _records.RemoveAt(0);
        }

        _records.Add(record);
        return Task.CompletedTask;
    }

    public void Clear() => _records.Clear();
}
