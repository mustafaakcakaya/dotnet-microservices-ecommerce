using BuildingBlocks.Messaging.Events.Ordering.V1;
using BuildingBlocks.Messaging.Serialization;
using Ordering.Application.Orders.IntegrationEvents;
using Ordering.Domain.Abstractions;
using Ordering.Domain.Enums;
using Ordering.Domain.Events;
using Ordering.Domain.Models;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application.UnitTests.Orders.IntegrationEvents;

public sealed class OrderIntegrationEventMapperTests
{
    private const string CardNumber = "5555555555554444";
    private const string Cvv = "123";

    [Fact]
    public void Map_OrderCreatedEvent_MapsToOrderCreatedIntegrationEvent()
    {
        var order = CreateOrder();

        var mapped = OrderIntegrationEventMapper.Map(new OrderCreatedEvent(order), "correlation-1");

        Assert.NotNull(mapped);
        var integrationEvent = Assert.IsType<OrderCreatedIntegrationEvent>(mapped.Event);
        Assert.Equal(order.Id.Value.ToString(), mapped.AggregateId);
        Assert.Equal(order.Id.Value, integrationEvent.OrderId);
        Assert.Equal(order.CustomerId.Value, integrationEvent.CustomerId);
        Assert.Equal("ORD_1", integrationEvent.OrderName);
        Assert.Equal(nameof(OrderStatus.Pending), integrationEvent.Status);
        Assert.Equal(order.TotalPrice, integrationEvent.TotalPrice);
        Assert.Equal("correlation-1", integrationEvent.CorrelationId);

        var item = Assert.Single(integrationEvent.Items);
        var orderItem = Assert.Single(order.OrderItems);
        Assert.Equal(orderItem.ProductId.Value, item.ProductId);
        Assert.Equal(orderItem.Quantity, item.Quantity);
        Assert.Equal(orderItem.Price, item.Price);
    }

    [Fact]
    public void Map_OrderUpdatedEvent_MapsToOrderUpdatedIntegrationEvent()
    {
        var order = CreateOrder();

        var mapped = OrderIntegrationEventMapper.Map(new OrderUpdatedEvent(order));

        Assert.NotNull(mapped);
        var integrationEvent = Assert.IsType<OrderUpdatedIntegrationEvent>(mapped.Event);
        Assert.Equal(order.Id.Value, integrationEvent.OrderId);
        Assert.Equal(order.TotalPrice, integrationEvent.TotalPrice);
    }

    [Fact]
    public void Map_UnknownDomainEvent_ReturnsNull()
    {
        Assert.Null(OrderIntegrationEventMapper.Map(new UnknownDomainEvent()));
    }

    [Fact]
    public void Map_OrderCreatedEvent_PayloadContainsNoPaymentData()
    {
        var order = CreateOrder();

        var mapped = OrderIntegrationEventMapper.Map(new OrderCreatedEvent(order));

        var payload = IntegrationEventSerializer.Serialize(mapped!.Event);
        Assert.DoesNotContain(CardNumber, payload);
        Assert.DoesNotContain("cardNumber", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cvv", payload, StringComparison.OrdinalIgnoreCase);
        // Addresses (PII) are not part of the contract either.
        Assert.DoesNotContain("mustafa@example.com", payload);
    }

    private sealed record UnknownDomainEvent : IDomainEvent;

    private static Order CreateOrder()
    {
        var address = Address.Of(
            "Mustafa",
            "Akcakaya",
            "mustafa@example.com",
            "Test Address",
            "Turkey",
            "Istanbul",
            "34000");

        var payment = Payment.Of("Mustafa Akcakaya", CardNumber, "12/28", Cvv, 1);

        var order = Order.Create(
            OrderId.Of(Guid.NewGuid()),
            CustomerId.Of(Guid.NewGuid()),
            OrderName.Of("ORD_1"),
            address,
            address,
            payment);

        order.Add(ProductId.Of(Guid.NewGuid()), quantity: 2, price: 500);

        return order;
    }
}
