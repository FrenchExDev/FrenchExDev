namespace FrenchExDev.Net.Outbox.EntityFramework;

/// <summary>
/// Implemented by entities that raise domain events to be captured by the outbox.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Gets the domain events raised by this entity.</summary>
    IReadOnlyList<object> DomainEvents { get; }

    /// <summary>Clears all domain events after they have been collected.</summary>
    void ClearDomainEvents();
}
