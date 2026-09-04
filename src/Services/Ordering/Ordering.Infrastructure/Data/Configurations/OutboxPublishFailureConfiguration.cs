using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Infrastructure.Outbox;

namespace Ordering.Infrastructure.Data.Configurations;

public class OutboxPublishFailureConfiguration : IEntityTypeConfiguration<OutboxPublishFailure>
{
    public void Configure(EntityTypeBuilder<OutboxPublishFailure> builder)
    {
        builder.ToTable("OutboxPublishFailures");

        builder.HasKey(failure => failure.OutboxMessageId);

        builder.Property(failure => failure.OutboxMessageId).ValueGeneratedNever();

        builder.Property(failure => failure.Attempts).IsRequired();

        builder.Property(failure => failure.LastError).HasMaxLength(2000);

        builder.Property(failure => failure.LastAttemptOnUtc).IsRequired();

        // Operators query poisoned messages to decide on manual replay.
        builder.HasIndex(failure => failure.PoisonedOnUtc)
            .HasFilter("[PoisonedOnUtc] IS NOT NULL");
    }
}
