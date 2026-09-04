using System.Text.Json;
using BuildingBlocks.Messaging.Events;

namespace BuildingBlocks.Messaging.Serialization;

/// <summary>
/// Single place that defines how integration events are serialized into the
/// outbox payload. The outbox writer and the CDC worker must use the same
/// options so payloads round-trip without loss.
/// </summary>
public static class IntegrationEventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), Options);

    public static IntegrationEvent Deserialize(string payload, Type eventType)
    {
        var integrationEvent = JsonSerializer.Deserialize(payload, eventType, Options)
            ?? throw new JsonException($"Payload deserialized to null for {eventType.FullName}.");

        return (IntegrationEvent)integrationEvent;
    }
}
