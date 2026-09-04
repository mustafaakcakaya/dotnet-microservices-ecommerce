using System.Reflection;
using BuildingBlocks.Messaging.Events;

namespace BuildingBlocks.Messaging.Publishing;

/// <summary>
/// Resolves the CLR contract type for an (EventType, SchemaVersion) pair stored
/// in the outbox envelope. Types are discovered via
/// <see cref="IntegrationEventContractAttribute"/>, never through Type.GetType
/// on a persisted string, so contract renames don't break stored messages.
/// </summary>
public sealed class IntegrationEventTypeRegistry
{
    private readonly IReadOnlyDictionary<(string EventType, int SchemaVersion), Type> _types;

    public IntegrationEventTypeRegistry()
        : this(typeof(IntegrationEvent).Assembly)
    {
    }

    public IntegrationEventTypeRegistry(params Assembly[] assemblies)
    {
        _types = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract && type.IsAssignableTo(typeof(IntegrationEvent)))
            .Select(type => (Type: type, Contract: type.GetCustomAttribute<IntegrationEventContractAttribute>()))
            .Where(entry => entry.Contract is not null)
            .ToDictionary(
                entry => (entry.Contract!.EventType, entry.Contract.SchemaVersion),
                entry => entry.Type);
    }

    public Type Resolve(string eventType, int schemaVersion)
    {
        if (!_types.TryGetValue((eventType, schemaVersion), out var type))
        {
            throw new InvalidOperationException(
                $"No integration event contract registered for '{eventType}' v{schemaVersion}. " +
                "Deploy a version that knows this contract, or the message will be poisoned after retries.");
        }

        return type;
    }
}
