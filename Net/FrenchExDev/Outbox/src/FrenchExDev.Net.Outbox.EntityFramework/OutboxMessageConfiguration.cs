using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FrenchExDev.Net.Outbox.EntityFramework;

/// <summary>
/// EF Core entity configuration for <see cref="OutboxMessage"/>.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.ProcessedAt);

        builder.Property(m => m.Attempts)
            .IsRequired();

        builder.Property(m => m.LastError)
            .HasMaxLength(4000);
    }
}
