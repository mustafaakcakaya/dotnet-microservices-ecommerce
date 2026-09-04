using BuildingBlocks.Messaging.Events;

namespace BuildingBlocks.Messaging.Publishing;

/// <summary>
/// Publishes an outbox envelope to the configured message broker. The only
/// broker-aware seam in the publish path: everything upstream (CDC reading,
/// checkpointing, retry) works against this interface.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken);
}
