using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Infrastructure.Outbox;

namespace Ordering.Infrastructure.Data.Configurations;

public class OutboxCdcCheckpointConfiguration : IEntityTypeConfiguration<OutboxCdcCheckpoint>
{
    public void Configure(EntityTypeBuilder<OutboxCdcCheckpoint> builder)
    {
        builder.ToTable("OutboxCdcCheckpoints");

        builder.HasKey(checkpoint => checkpoint.ConsumerName);

        builder.Property(checkpoint => checkpoint.ConsumerName)
            .HasMaxLength(100);

        // SQL Server LSNs are binary(10); the column type must match exactly
        // for comparisons against cdc.fn_cdc_* function results.
        builder.Property(checkpoint => checkpoint.LastProcessedLsn)
            .HasColumnType("binary(10)")
            .IsRequired();

        builder.Property(checkpoint => checkpoint.UpdatedOnUtc)
            .IsRequired();
    }
}
