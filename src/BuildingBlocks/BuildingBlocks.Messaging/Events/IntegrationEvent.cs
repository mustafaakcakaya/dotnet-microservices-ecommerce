namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Base contract for messages published to the message broker.
/// Integration events are explicit, versioned contracts between services;
/// they must never expose domain entities, EF entities or sensitive data
/// (card numbers, CVV, etc.).
/// </summary>
public abstract record IntegrationEvent
{
    /// <summary>
    /// Stable unique id of the event. It is generated once (when the event is
    /// mapped from a domain event), stored in the outbox and used as the broker
    /// MessageId, so consumers can deduplicate at-least-once deliveries.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;

    public string? CorrelationId { get; init; }
}
