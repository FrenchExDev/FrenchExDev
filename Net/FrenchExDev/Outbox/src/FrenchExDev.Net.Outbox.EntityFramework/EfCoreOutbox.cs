using Microsoft.EntityFrameworkCore;

namespace FrenchExDev.Net.Outbox.EntityFramework;

/// <summary>
/// EF Core implementation of <see cref="IOutbox"/> that stores messages via <see cref="DbContext"/>.
/// </summary>
public sealed class EfCoreOutbox : IOutbox
{
    private readonly DbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreOutbox"/> class.
    /// </summary>
    /// <param name="dbContext">The database context to store outbox messages in.</param>
    public EfCoreOutbox(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task StoreAsync(OutboxMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _dbContext.Set<OutboxMessage>().AddAsync(message, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
