using BuildingBlocks.Messaging.Events;
using BuildingBlocks.Messaging.Serialization;
using MassTransit;

namespace BuildingBlocks.Messaging.Publishing;

/// <summary>
/// Publishes through MassTransit/RabbitMQ. The payload is deserialized back into
/// its contract type so MassTransit publishes to the contract's exchange and
/// consumers bind by message type as usual.
/// </summary>
public sealed class RabbitMqIntegrationEventPublisher(
    IBus bus,
    IntegrationEventTypeRegistry typeRegistry) : IIntegrationEventPublisher
{
    public async Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var contractType = typeRegistry.Resolve(envelope.EventType, envelope.SchemaVersion);
        var integrationEvent = IntegrationEventSerializer.Deserialize(envelope.Payload, contractType);

        await bus.Publish(
            integrationEvent,
            contractType,
            context =>
            {
                // Carried end to end so consumers can deduplicate (at-least-once delivery).
                context.MessageId = envelope.Id;

                if (Guid.TryParse(envelope.CorrelationId, out var correlationId))
                {
                    context.CorrelationId = correlationId;
                }

                context.Headers.Set("schema-version", envelope.SchemaVersion);
                context.Headers.Set("aggregate-id", envelope.AggregateId);
            },
            cancellationToken);
    }
}
