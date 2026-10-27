using BuildingBlocks.CQRS;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Data;
using Ordering.Application.Dtos;
using Ordering.Domain.Models;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application.Orders.Commands.CreateOrder;

public class CreateOrderHandler(IApplicationDbContext dbContext)
    : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    public async Task<CreateOrderResult> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = CreateNewOrder(command.Order);

        await AddUnknownProducts(command.Products, cancellationToken);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult(order.Id.Value);
    }

    private async Task AddUnknownProducts(
        IReadOnlyList<ProductSnapshotDto>? products,
        CancellationToken cancellationToken)
    {
        if (products is null || products.Count == 0)
        {
            return;
        }

        var ids = products.Select(product => ProductId.Of(product.ProductId)).Distinct().ToList();
        var known = await dbContext.Products
            .Where(product => ids.Contains(product.Id))
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        // A product already known keeps its stored name and price; the snapshot
        // only fills a gap, it does not overwrite Ordering's reference data.
        foreach (var product in products.DistinctBy(product => product.ProductId))
        {
            var id = ProductId.Of(product.ProductId);
            if (!known.Contains(id))
            {
                dbContext.Products.Add(Product.Create(id, product.Name, product.Price));
            }
        }
    }

    private static Order CreateNewOrder(OrderDto orderDto)
    {
        var shippingAddress = Address.Of(
            orderDto.ShippingAddress.FirstName,
            orderDto.ShippingAddress.LastName,
            orderDto.ShippingAddress.EmailAddress,
            orderDto.ShippingAddress.AddressLine,
            orderDto.ShippingAddress.Country,
            orderDto.ShippingAddress.State,
            orderDto.ShippingAddress.ZipCode);

        var billingAddress = Address.Of(
            orderDto.BillingAddress.FirstName,
            orderDto.BillingAddress.LastName,
            orderDto.BillingAddress.EmailAddress,
            orderDto.BillingAddress.AddressLine,
            orderDto.BillingAddress.Country,
            orderDto.BillingAddress.State,
            orderDto.BillingAddress.ZipCode);

        var newOrder = Order.Create(
            id: OrderId.Of(Guid.NewGuid()),
            customerId: CustomerId.Of(orderDto.CustomerId),
            orderName: OrderName.Of(orderDto.OrderName),
            shippingAddress: shippingAddress,
            billingAddress: billingAddress,
            payment: Payment.Of(
                orderDto.Payment.PaymentToken,
                orderDto.Payment.CardLast4,
                orderDto.Payment.PaymentMethod));

        foreach (var orderItemDto in orderDto.OrderItems)
        {
            newOrder.Add(
                ProductId.Of(orderItemDto.ProductId),
                orderItemDto.Quantity,
                orderItemDto.Price);
        }

        return newOrder;
    }
}
