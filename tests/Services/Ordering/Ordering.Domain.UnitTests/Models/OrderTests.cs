using Ordering.Domain.Events;
using Ordering.Domain.Models;
using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.UnitTests.Models;

public sealed class OrderTests
{
    [Fact]
    public void Create_WithValidArguments_RaisesOrderCreatedEvent()
    {
        // Act
        var order = CreateOrder();

        // Assert
        var raisedEvent = Assert.IsType<OrderCreatedEvent>(Assert.Single(order.DomainEvents));
        Assert.Same(order, raisedEvent.order);
    }

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

        var payment = Payment.Of(
            "Mustafa Akcakaya",
            "5555555555554444",
            "12/28",
            "123",
            1);

        return Order.Create(
            OrderId.Of(Guid.NewGuid()),
            CustomerId.Of(Guid.NewGuid()),
            OrderName.Of("ORD_1"),
            address,
            address,
            payment);
    }
}
