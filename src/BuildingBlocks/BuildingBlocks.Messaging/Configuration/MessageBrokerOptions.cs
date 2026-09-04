namespace BuildingBlocks.Messaging.Configuration;

public enum MessageBrokerProvider
{
    RabbitMq = 0,
    Kafka = 1
}

/// <summary>
/// Broker selection is configuration-driven so a service can move between
/// RabbitMQ and Kafka without a code change.
/// </summary>
public class MessageBrokerOptions
{
    public const string SectionName = "MessageBroker";

    public MessageBrokerProvider Provider { get; set; } = MessageBrokerProvider.RabbitMq;

    public RabbitMqOptions RabbitMq { get; set; } = new();

    public KafkaOptions Kafka { get; set; } = new();
}

public class RabbitMqOptions
{
    public string Host { get; set; } = "amqp://localhost:5672";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Prepended to the event type to form the topic name (e.g. "dev.").</summary>
    public string TopicPrefix { get; set; } = string.Empty;

    /// <summary>Producer-side dedup of retries within a session. Independent of consumer idempotency.</summary>
    public bool EnableIdempotence { get; set; } = true;

    /// <summary>"all" waits for all in-sync replicas; anything weaker can lose acknowledged writes.</summary>
    public string Acks { get; set; } = "all";

    public int MessageTimeoutMs { get; set; } = 30_000;

    public string Topic(string eventType) => $"{TopicPrefix}{eventType}";
}
