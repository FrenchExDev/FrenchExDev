using System;

namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// A historical record of a transition that occurred.
/// </summary>
public sealed class TransitionRecord<TState, TEvent>
{
    public TransitionRecord(TState from, TEvent @event, TState to, DateTimeOffset timestamp)
    {
        From = from;
        Event = @event;
        To = to;
        Timestamp = timestamp;
    }

    public TState From { get; }
    public TEvent Event { get; }
    public TState To { get; }
    public DateTimeOffset Timestamp { get; }
}
