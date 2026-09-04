namespace BuildingBlocks.Messaging.Inbox;

/// <summary>
/// Consumer-side deduplication store (inbox pattern). Publishing is
/// at-least-once by design, so every consumer must run its work through this
/// store keyed on the broker message id.
/// </summary>
public interface IInboxStore
{
    /// <summary>
    /// Claims a message for processing.
    /// Returns <c>false</c> when this consumer already **finished** the message —
    /// the delivery is a duplicate and must be skipped.
    /// Returns <c>true</c> for a message never seen before, and also when a
    /// previous attempt was claimed but never completed (the consumer crashed
    /// mid-flight): re-processing is preferred over silently losing the message.
    /// </summary>
    Task<bool> TryBeginAsync(Guid messageId, string consumerName, CancellationToken cancellationToken);

    /// <summary>Marks the message as fully processed, so later deliveries are skipped.</summary>
    Task CompleteAsync(Guid messageId, string consumerName, CancellationToken cancellationToken);
}
