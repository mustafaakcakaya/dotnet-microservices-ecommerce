using System.Text;
using BuildingBlocks.Messaging.Configuration;
using BuildingBlocks.Messaging.Events;
using BuildingBlocks.Messaging.Publishing;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Testcontainers.Kafka;

namespace Ordering.IntegrationTests;

/// <summary>
/// Proves the Kafka provider works against a real broker: switching
/// MessageBroker:Provider to Kafka is enough, no code change. Verifies the
/// envelope mapping the consumer side depends on — key, headers and raw payload.
/// </summary>
public sealed class KafkaPublisherTests : IAsyncLifetime
{
    private readonly KafkaContainer _kafka = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.6.1")
        .Build();

    private IProducer<string, string> _producer = default!;

    public async Task InitializeAsync()
    {
        await _kafka.StartAsync();

        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _kafka.GetBootstrapAddress(),
            EnableIdempotence = true,
            Acks = Acks.All
        }).Build();
    }

    public async Task DisposeAsync()
    {
        _producer?.Dispose();
        await _kafka.DisposeAsync();
    }

    [Fact]
    public async Task PublishAsync_WritesEnvelopeToTopic_WithMessageIdHeaderAndAggregateKey()
    {
        var options = Options.Create(new MessageBrokerOptions
        {
            Provider = MessageBrokerProvider.Kafka,
            Kafka = new KafkaOptions { BootstrapServers = _kafka.GetBootstrapAddress() }
        });

        var publisher = new KafkaIntegrationEventPublisher(_producer, options);

        var envelope = new IntegrationEventEnvelope(
            Id: Guid.NewGuid(),
            EventType: "ordering.order-created",
            SchemaVersion: 1,
            AggregateId: Guid.NewGuid().ToString(),
            CorrelationId: "corr-7",
            OccurredOnUtc: DateTime.UtcNow,
            Payload: """{"orderName":"ORD_1","totalPrice":1000}""");

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await publisher.PublishAsync(envelope, timeout.Token);

        var consumed = Consume(envelope.EventType, timeout.Token);

        Assert.Equal(envelope.AggregateId, consumed.Message.Key);
        Assert.Equal(envelope.Payload, consumed.Message.Value);
        Assert.Equal(
            envelope.Id.ToString(),
            HeaderValue(consumed.Message.Headers, KafkaIntegrationEventPublisher.MessageIdHeader));
        Assert.Equal(
            envelope.EventType,
            HeaderValue(consumed.Message.Headers, KafkaIntegrationEventPublisher.EventTypeHeader));
        Assert.Equal(
            "1",
            HeaderValue(consumed.Message.Headers, KafkaIntegrationEventPublisher.SchemaVersionHeader));
        Assert.Equal(
            "corr-7",
            HeaderValue(consumed.Message.Headers, KafkaIntegrationEventPublisher.CorrelationIdHeader));
    }

    private ConsumeResult<string, string> Consume(string topic, CancellationToken cancellationToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _kafka.GetBootstrapAddress(),
            GroupId = $"test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();

        consumer.Subscribe(topic);

        try
        {
            return consumer.Consume(cancellationToken);
        }
        finally
        {
            consumer.Close();
        }
    }

    private static string? HeaderValue(Headers headers, string key) =>
        headers.TryGetLastBytes(key, out var value) ? Encoding.UTF8.GetString(value) : null;
}
