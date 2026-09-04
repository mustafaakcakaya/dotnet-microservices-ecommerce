using System.Text;
using BuildingBlocks.Messaging.Configuration;
using BuildingBlocks.Messaging.Events;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging.Publishing;

/// <summary>
/// Publishes to Kafka. The stored JSON payload is written as-is (no re-serialize),
/// the envelope metadata travels in headers, and the key is the aggregate id so
/// all events of one aggregate land on the same partition and stay ordered.
/// </summary>
public sealed class KafkaIntegrationEventPublisher(
    IProducer<string, string> producer,
    IOptions<MessageBrokerOptions> options) : IIntegrationEventPublisher
{
    public const string MessageIdHeader = "message-id";
    public const string EventTypeHeader = "event-type";
    public const string SchemaVersionHeader = "schema-version";
    public const string CorrelationIdHeader = "correlation-id";
    public const string OccurredOnHeader = "occurred-on-utc";

    private readonly KafkaOptions _kafka = options.Value.Kafka;

    public async Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var headers = new Headers
        {
            // Consumers deduplicate on this id: delivery is at-least-once.
            { MessageIdHeader, Encode(envelope.Id.ToString()) },
            { EventTypeHeader, Encode(envelope.EventType) },
            { SchemaVersionHeader, Encode(envelope.SchemaVersion.ToString()) },
            { OccurredOnHeader, Encode(envelope.OccurredOnUtc.ToString("O")) }
        };

        if (envelope.CorrelationId is not null)
        {
            headers.Add(CorrelationIdHeader, Encode(envelope.CorrelationId));
        }

        var message = new Message<string, string>
        {
            Key = envelope.AggregateId,
            Value = envelope.Payload,
            Headers = headers
        };

        await producer.ProduceAsync(_kafka.Topic(envelope.EventType), message, cancellationToken);
    }

    private static byte[] Encode(string value) => Encoding.UTF8.GetBytes(value);
}
