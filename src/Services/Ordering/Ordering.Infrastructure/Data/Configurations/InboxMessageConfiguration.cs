using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Infrastructure.Inbox;

namespace Ordering.Infrastructure.Data.Configurations;

public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");

        // One row per (message, consumer): the same event may be handled by
        // several endpoints, each deduplicating independently.
        builder.HasKey(message => new { message.MessageId, message.ConsumerName });

        builder.Property(message => message.ConsumerName).HasMaxLength(200);

        builder.Property(message => message.ReceivedOnUtc).IsRequired();
    }
}
