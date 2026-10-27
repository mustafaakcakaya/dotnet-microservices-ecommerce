namespace BuildingBlocks.Messaging.Events.Ordering.V2;

/// <summary>
/// Published when a new order has been persisted in the Ordering service.
/// Payload intentionally contains only the fields other services need;
/// addresses and payment details are never included.
/// </summary>
/// <remarks>
/// v2 dropped OrderName: an order is identified by OrderId alone. v1 was removed
/// rather than kept alongside, as nothing consumed it.
/// </remarks>
[IntegrationEventContract("ordering.order-created", schemaVersion: 2)]
public sealed record OrderCreatedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string Status { get; init; } = default!;
    public decimal TotalPrice { get; init; }
    public IReadOnlyList<OrderItemLine> Items { get; init; } = [];
}

public sealed record OrderItemLine(Guid ProductId, int Quantity, decimal Price);
