namespace FrenchExDev.Net.FiniteStateMachine.Rich;

/// <summary>
/// Marker interface for event types in rich FSMs.
/// Users define their own domain interface (e.g., IOrderEvent : IEvent)
/// and implement it with records/classes.
/// </summary>
public interface IEvent
{
    string Name { get; }
}
