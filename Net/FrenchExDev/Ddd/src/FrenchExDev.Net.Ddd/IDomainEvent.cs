namespace FrenchExDev.Net.Ddd
{
    public interface IDomainEvent
    {
        System.DateTimeOffset OccurredAt { get; }
    }
}
