namespace BuildingBlocks.Messaging.Events.Ordering.V1;

/// <summary>
/// Published when an existing order has been updated in the Ordering service.
/// </summary>
[IntegrationEventContract("ordering.order-updated", schemaVersion: 1)]
public sealed record OrderUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string OrderName { get; init; } = default!;
    public string Status { get; init; } = default!;
    public decimal TotalPrice { get; init; }
}
