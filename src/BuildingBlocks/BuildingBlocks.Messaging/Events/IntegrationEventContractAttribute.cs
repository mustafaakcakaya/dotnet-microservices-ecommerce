using System.Collections.Concurrent;
using System.Reflection;

namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Declares the wire-level identity of an integration event: a stable event type
/// name and a schema version. The pair is stored in the outbox envelope and used
/// by the publisher to resolve the CLR contract type, so contract renames or
/// assembly moves never break already-persisted messages.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventContractAttribute(string eventType, int schemaVersion) : Attribute
{
    public string EventType { get; } = eventType;
    public int SchemaVersion { get; } = schemaVersion;
}

public static class IntegrationEventContract
{
    private static readonly ConcurrentDictionary<Type, IntegrationEventContractAttribute> Cache = new();

    public static IntegrationEventContractAttribute For(Type eventType)
    {
        return Cache.GetOrAdd(eventType, type =>
            type.GetCustomAttribute<IntegrationEventContractAttribute>()
            ?? throw new InvalidOperationException(
                $"{type.FullName} is missing the [IntegrationEventContract] attribute."));
    }
}
