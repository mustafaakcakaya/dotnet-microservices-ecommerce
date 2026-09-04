namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Transport-level envelope of an integration event, as stored in the outbox and
/// read back by the CDC worker. Broker-agnostic on purpose: the same record is
/// handed to the RabbitMQ and the Kafka publisher.
/// </summary>
/// <param name="Id">
/// Stable event id, carried to the broker as the message id. Delivery is
/// at-least-once, so consumers deduplicate on this value (see the inbox store).
/// </param>
public sealed record IntegrationEventEnvelope(
    Guid Id,
    string EventType,
    int SchemaVersion,
    string AggregateId,
    string? CorrelationId,
    DateTime OccurredOnUtc,
    string Payload);
