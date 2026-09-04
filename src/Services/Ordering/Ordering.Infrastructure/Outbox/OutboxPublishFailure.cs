namespace Ordering.Infrastructure.Outbox;

/// <summary>
/// Publish attempt counter and poison marker for a single outbox message.
/// The payload itself stays in <see cref="OutboxMessage"/> for inspection/replay.
/// </summary>
public class OutboxPublishFailure
{
    public Guid OutboxMessageId { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime LastAttemptOnUtc { get; set; }

    /// <summary>Set once the message exceeded MaxPublishAttempts and is skipped.</summary>
    public DateTime? PoisonedOnUtc { get; set; }
}
