using System.Diagnostics;
using BuildingBlocks.Messaging.Events;
using BuildingBlocks.Messaging.Serialization;

namespace Ordering.Infrastructure.Outbox;

/// <summary>
/// Transactional outbox envelope. Rows are written in the same SQL transaction
/// as the aggregate changes; SQL Server CDC tracks INSERTs on this table and a
/// separate worker publishes them to the message broker. The table is
/// append-only: processing state lives in the worker's checkpoint tables, so
/// rows are never updated or deleted by the application.
/// </summary>
public class OutboxMessage
{
    /// <summary>Integration event id; used as the broker MessageId.</summary>
    public Guid Id { get; init; }

    /// <summary>Stable wire name of the contract, e.g. "ordering.order-created".</summary>
    public string EventType { get; init; } = default!;

    public int SchemaVersion { get; init; }

    public string AggregateId { get; init; } = default!;

    public string? CorrelationId { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    /// <summary>JSON-serialized integration event (never a domain/EF entity).</summary>
    public string Payload { get; init; } = default!;

    public static OutboxMessage From(IntegrationEvent integrationEvent, string aggregateId)
    {
        var contract = IntegrationEventContract.For(integrationEvent.GetType());

        return new OutboxMessage
        {
            Id = integrationEvent.Id,
            EventType = contract.EventType,
            SchemaVersion = contract.SchemaVersion,
            AggregateId = aggregateId,
            CorrelationId = integrationEvent.CorrelationId ?? Activity.Current?.TraceId.ToString(),
            OccurredOnUtc = integrationEvent.OccurredOnUtc,
            Payload = IntegrationEventSerializer.Serialize(integrationEvent)
        };
    }
}
