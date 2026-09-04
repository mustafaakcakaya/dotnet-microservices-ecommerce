using BuildingBlocks.Messaging.Events;
using BuildingBlocks.Messaging.Events.Ordering.V1;
using Ordering.Domain.Abstractions;
using Ordering.Domain.Events;
using Ordering.Domain.Models;

namespace Ordering.Application.Orders.IntegrationEvents;

/// <summary>
/// An integration event bound to the aggregate that produced it,
/// ready to be persisted as an outbox message.
/// </summary>
public sealed record OutboxBoundIntegrationEvent(IntegrationEvent Event, string AggregateId);

/// <summary>
/// Maps domain events to their outward-facing integration event contracts.
/// Domain events stay inside the Domain layer; only these explicit, versioned
/// contracts leave the service. Payment details are deliberately never mapped.
/// </summary>
public static class OrderIntegrationEventMapper
{
    public static OutboxBoundIntegrationEvent? Map(IDomainEvent domainEvent, string? correlationId = null) =>
        domainEvent switch
        {
            OrderCreatedEvent created => new OutboxBoundIntegrationEvent(
                new OrderCreatedIntegrationEvent
                {
                    CorrelationId = correlationId,
                    OrderId = created.order.Id.Value,
                    CustomerId = created.order.CustomerId.Value,
                    OrderName = created.order.OrderName.Value,
                    Status = created.order.Status.ToString(),
                    TotalPrice = created.order.TotalPrice,
                    Items = MapItems(created.order)
                },
                created.order.Id.Value.ToString()),

            OrderUpdatedEvent updated => new OutboxBoundIntegrationEvent(
                new OrderUpdatedIntegrationEvent
                {
                    CorrelationId = correlationId,
                    OrderId = updated.order.Id.Value,
                    CustomerId = updated.order.CustomerId.Value,
                    OrderName = updated.order.OrderName.Value,
                    Status = updated.order.Status.ToString(),
                    TotalPrice = updated.order.TotalPrice
                },
                updated.order.Id.Value.ToString()),

            _ => null
        };

    private static List<OrderItemLine> MapItems(Order order) =>
        order.OrderItems
            .Select(item => new OrderItemLine(item.ProductId.Value, item.Quantity, item.Price))
            .ToList();
}
