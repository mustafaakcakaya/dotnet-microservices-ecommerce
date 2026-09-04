namespace Ordering.Infrastructure.Outbox;

/// <summary>
/// Last CDC log sequence number successfully published, per consumer.
/// Owned by Ordering.Worker at runtime (raw ADO.NET), but the schema lives here
/// so migrations remain the single source of truth for OrderDb.
/// </summary>
public class OutboxCdcCheckpoint
{
    public string ConsumerName { get; set; } = default!;

    public byte[] LastProcessedLsn { get; set; } = default!;

    public DateTime UpdatedOnUtc { get; set; }
}
