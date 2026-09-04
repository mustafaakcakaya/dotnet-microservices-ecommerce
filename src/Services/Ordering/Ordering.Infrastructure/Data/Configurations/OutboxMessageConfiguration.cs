using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Infrastructure.Outbox;

namespace Ordering.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);

        // The worker never reads this table through EF; the PK is enough.
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.EventType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.SchemaVersion)
            .IsRequired();

        builder.Property(m => m.AggregateId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.CorrelationId)
            .HasMaxLength(100);

        builder.Property(m => m.OccurredOnUtc)
            .IsRequired();

        builder.Property(m => m.Payload)
            .IsRequired();
    }
}
