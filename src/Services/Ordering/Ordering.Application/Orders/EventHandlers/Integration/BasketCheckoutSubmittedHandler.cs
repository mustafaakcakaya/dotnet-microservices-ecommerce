using BuildingBlocks.Messaging.Events.Basket.V1;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Ordering.Application.Dtos;
using Ordering.Application.Orders.Commands.CreateOrder;
using Ordering.Domain.Enums;

namespace Ordering.Application.Orders.EventHandlers.Integration;

/// <summary>
/// Turns a submitted checkout into an order, line for line. Duplicate deliveries
/// are dropped before this runs (see the inbox filter).
/// </summary>
public class BasketCheckoutSubmittedHandler(
    ISender sender,
    ILogger<BasketCheckoutSubmittedHandler> logger)
    : IConsumer<BasketCheckoutSubmittedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<BasketCheckoutSubmittedIntegrationEvent> context)
    {
        logger.LogInformation(
            "Integration Event handled: {IntegrationEvent} {MessageId}",
            context.Message.GetType().Name, context.Message.Id);

        var command = MapToCreateOrderCommand(context.Message);
        await sender.Send(command, context.CancellationToken);
    }

    internal static CreateOrderCommand MapToCreateOrderCommand(BasketCheckoutSubmittedIntegrationEvent message)
    {
        if (message.Items.Count == 0)
        {
            throw new InvalidOperationException($"Checkout {message.Id} has no lines.");
        }

        // The total is recomputed rather than trusted. A mismatch means the sender
        // and this service disagree about the order, which must not be guessed away.
        var linesTotal = message.Items.Sum(line => line.UnitPrice * line.Quantity);
        if (linesTotal != message.TotalPrice)
        {
            throw new InvalidOperationException(
                $"Checkout {message.Id} total {message.TotalPrice} does not match its lines ({linesTotal}).");
        }

        var orderId = Guid.NewGuid();

        var orderDto = new OrderDto(
            Id: orderId,
            CustomerId: message.CustomerId,
            ShippingAddress: ToAddressDto(message.ShippingAddress),
            BillingAddress: ToAddressDto(message.BillingAddress),
            Payment: new PaymentDto(
                message.Payment.PaymentToken,
                message.Payment.CardLast4,
                message.Payment.PaymentMethod),
            Status: OrderStatus.Pending,
            OrderItems: message.Items
                .Select(line => new OrderItemDto(orderId, line.ProductId, line.Quantity, line.UnitPrice))
                .ToList());

        var products = message.Items
            .Select(line => new ProductSnapshotDto(line.ProductId, line.ProductName, line.UnitPrice))
            .ToList();

        return new CreateOrderCommand(orderDto, products);
    }

    private static AddressDto ToAddressDto(CheckoutAddress address) => new(
        address.FirstName,
        address.LastName,
        address.EmailAddress,
        address.AddressLine,
        address.Country,
        address.State,
        address.ZipCode);
}
