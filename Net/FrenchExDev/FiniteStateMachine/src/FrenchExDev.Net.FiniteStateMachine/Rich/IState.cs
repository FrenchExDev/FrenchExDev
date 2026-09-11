namespace FrenchExDev.Net.FiniteStateMachine.Rich;

/// <summary>
/// Marker interface for state types in rich FSMs.
/// Users define their own domain interface (e.g., IOrderState : IState)
/// and implement it with records/classes.
/// </summary>
public interface IState
{
    string Name { get; }
}
