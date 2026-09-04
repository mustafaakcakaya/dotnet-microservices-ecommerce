using BuildingBlocks.Messaging.Events;
using BuildingBlocks.Messaging.Events.Ordering.V1;
using BuildingBlocks.Messaging.Publishing;
using BuildingBlocks.Messaging.Serialization;
using Ordering.Application.Orders.IntegrationEvents;
using Ordering.Domain.Events;
using Ordering.Domain.Models;
using Ordering.Domain.ValueObjects;
using Ordering.Infrastructure.Outbox;

namespace Ordering.Worker.UnitTests.Publishing;

/// <summary>
/// Covers the full serialization pipeline: domain event -> integration event ->
/// outbox envelope (writer side) -> CDC record -> resolved contract type ->
/// deserialized event (worker side), asserting the event id survives unchanged.
/// </summary>
public sealed class OutboxRoundTripTests
{
    private readonly IntegrationEventTypeRegistry _registry = new();

    [Fact]
    public void OutboxMessage_RoundTrip_PreservesEventIdAndPayload()
    {
        var order = CreateOrder();
        var mapped = OrderIntegrationEventMapper.Map(new OrderCreatedEvent(order), "corr-42")!;
        var original = (OrderCreatedIntegrationEvent)mapped.Event;

        // Writer side: envelope staged in the same transaction as the order.
        var outboxMessage = OutboxMessage.From(mapped.Event, mapped.AggregateId);

        Assert.Equal(original.Id, outboxMessage.Id);
        Assert.Equal("ordering.order-created", outboxMessage.EventType);
        Assert.Equal(1, outboxMessage.SchemaVersion);
        Assert.Equal(order.Id.Value.ToString(), outboxMessage.AggregateId);
        Assert.Equal("corr-42", outboxMessage.CorrelationId);

        // Worker side: the CDC record carries the same envelope columns.
        var record = new IntegrationEventEnvelope(
            outboxMessage.Id,
            outboxMessage.EventType,
            outboxMessage.SchemaVersion,
            outboxMessage.AggregateId,
            outboxMessage.CorrelationId,
            outboxMessage.OccurredOnUtc,
            outboxMessage.Payload);

        var contractType = _registry.Resolve(record.EventType, record.SchemaVersion);
        var roundTripped = Assert.IsType<OrderCreatedIntegrationEvent>(
            IntegrationEventSerializer.Deserialize(record.Payload, contractType));

        // Same event id end-to-end: it becomes the broker MessageId.
        Assert.Equal(original.Id, roundTripped.Id);
        Assert.Equal(original.OrderId, roundTripped.OrderId);
        Assert.Equal(original.CustomerId, roundTripped.CustomerId);
        Assert.Equal(original.OrderName, roundTripped.OrderName);
        Assert.Equal(original.Status, roundTripped.Status);
        Assert.Equal(original.TotalPrice, roundTripped.TotalPrice);
        Assert.Equal(original.Items, roundTripped.Items);
        Assert.Equal(original.CorrelationId, roundTripped.CorrelationId);
    }

    [Fact]
    public void OutboxMessage_Payload_DoesNotContainPaymentData()
    {
        var order = CreateOrder();
        var mapped = OrderIntegrationEventMapper.Map(new OrderCreatedEvent(order))!;

        var outboxMessage = OutboxMessage.From(mapped.Event, mapped.AggregateId);

        Assert.DoesNotContain("5555555555554444", outboxMessage.Payload);
        Assert.DoesNotContain("cardNumber", outboxMessage.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cvv", outboxMessage.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Registry_UnknownContract_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _registry.Resolve("ordering.unknown", 1));
        Assert.Throws<InvalidOperationException>(() => _registry.Resolve("ordering.order-created", 999));
    }

    [Fact]
    public void Registry_ResolvesAllOrderingContracts()
    {
        Assert.Equal(typeof(OrderCreatedIntegrationEvent), _registry.Resolve("ordering.order-created", 1));
        Assert.Equal(typeof(OrderUpdatedIntegrationEvent), _registry.Resolve("ordering.order-updated", 1));
    }

    private static Order CreateOrder()
    {
        var address = Address.Of(
            "Mustafa", "Akcakaya", "mustafa@example.com", "Test Address", "Turkey", "Istanbul", "34000");
        var payment = Payment.Of("Mustafa Akcakaya", "5555555555554444", "12/28", "123", 1);

        var order = Order.Create(
            OrderId.Of(Guid.NewGuid()),
            CustomerId.Of(Guid.NewGuid()),
            OrderName.Of("ORD_1"),
            address,
            address,
            payment);

        order.Add(ProductId.Of(Guid.NewGuid()), quantity: 1, price: 350);

        return order;
    }
}
