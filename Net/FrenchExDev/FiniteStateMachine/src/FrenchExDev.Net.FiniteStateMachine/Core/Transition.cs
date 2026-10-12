namespace FrenchExDev.Net.FiniteStateMachine;

/// <summary>
/// Success payload of a state transition.
/// </summary>
public sealed class Transition<TState>
{
    public Transition(TState from, TState to, bool isReentrant)
    {
        From = from;
        To = to;
        IsReentrant = isReentrant;
    }

    public TState From { get; }
    public TState To { get; }
    public bool IsReentrant { get; }
}
