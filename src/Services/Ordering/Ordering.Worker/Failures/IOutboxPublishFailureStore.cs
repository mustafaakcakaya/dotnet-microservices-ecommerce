namespace Ordering.Worker.Failures;

public sealed record OutboxPublishFailure(int Attempts, DateTime? PoisonedOnUtc)
{
    public bool IsPoisoned => PoisonedOnUtc is not null;
}

/// <summary>
/// Tracks publish attempts per outbox message so retries survive worker
/// restarts, and records poison messages: after the configured number of
/// failed attempts a message is marked poisoned and skipped, so one broken
/// message cannot block the outbox stream forever. The original payload stays
/// in dbo.OutboxMessages for manual inspection/replay.
/// </summary>
public interface IOutboxPublishFailureStore
{
    Task<OutboxPublishFailure?> GetAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>Records one failed attempt and returns the new attempt count.</summary>
    Task<int> RecordFailureAsync(Guid messageId, string error, CancellationToken cancellationToken);

    Task MarkPoisonedAsync(Guid messageId, CancellationToken cancellationToken);
}
