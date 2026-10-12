using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrenchExDev.Net.Outbox.EntityFramework;

/// <summary>
/// A <see cref="SaveChangesInterceptor"/> that collects domain events from tracked entities
/// implementing <see cref="IHasDomainEvents"/> and stores them as <see cref="OutboxMessage"/> records.
/// </summary>
public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ConvertDomainEventsToOutboxMessages(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ConvertDomainEventsToOutboxMessages(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private static void ConvertDomainEventsToOutboxMessages(DbContext context)
    {
        var entities = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var messages = new List<OutboxMessage>();

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                var outboxMessage = new OutboxMessage
                {
                    Type = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().FullName ?? domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                };
                messages.Add(outboxMessage);
            }

            entity.ClearDomainEvents();
        }

        context.Set<OutboxMessage>().AddRange(messages);
    }
}
