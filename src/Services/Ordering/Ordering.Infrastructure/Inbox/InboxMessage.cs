namespace Ordering.Infrastructure.Inbox;

/// <summary>
/// Consumer-side deduplication record. Delivery is at-least-once, so a consumer
/// records every message id it has processed and skips repeats.
/// Written through BuildingBlocks.Messaging' SqlServerInboxStore.
/// </summary>
public class InboxMessage
{
    public Guid MessageId { get; set; }

    /// <summary>Endpoint/queue that consumed the message; a message may be handled by several consumers.</summary>
    public string ConsumerName { get; set; } = default!;

    public DateTime ReceivedOnUtc { get; set; }

    /// <summary>Null while a claim is in flight; set once handling finished successfully.</summary>
    public DateTime? ProcessedOnUtc { get; set; }
}
