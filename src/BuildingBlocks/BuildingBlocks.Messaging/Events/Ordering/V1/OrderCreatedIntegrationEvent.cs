namespace BuildingBlocks.Messaging.Events.Ordering.V1;

/// <summary>
/// Published when a new order has been persisted in the Ordering service.
/// Payload intentionally contains only the fields other services need;
/// addresses and payment details (card number, CVV) are never included.
/// </summary>
[IntegrationEventContract("ordering.order-created", schemaVersion: 1)]
public sealed record OrderCreatedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string OrderName { get; init; } = default!;
    public string Status { get; init; } = default!;
    public decimal TotalPrice { get; init; }
    public IReadOnlyList<OrderItemLine> Items { get; init; } = [];
}

public sealed record OrderItemLine(Guid ProductId, int Quantity, decimal Price);
