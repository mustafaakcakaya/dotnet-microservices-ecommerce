using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Models;
using Ordering.Domain.ValueObjects;
using Ordering.Infrastructure.Data;

namespace Ordering.IntegrationTests;

internal static class TestOrders
{
    public const string CardNumber = "5555555555554444";

    public static async Task<(Customer Customer, Product Product)> SeedReferenceDataAsync(
        ApplicationDbContext context)
    {
        var customer = Customer.Create(
            CustomerId.Of(Guid.NewGuid()), "Test Customer", $"customer-{Guid.NewGuid():N}@test.local");
        var product = Product.Create(ProductId.Of(Guid.NewGuid()), "Test Product", 350);

        context.Customers.Add(customer);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        return (customer, product);
    }

    public static Order NewOrder(CustomerId customerId, ProductId productId)
    {
        var address = Address.Of(
            "Mustafa", "Akcakaya", "mustafa@example.com", "Test Address", "Turkey", "Istanbul", "34000");
        var payment = Payment.Of("Mustafa Akcakaya", CardNumber, "12/28", "123", 1);

        var order = Order.Create(
            OrderId.Of(Guid.NewGuid()),
            customerId,
            OrderName.Of("ORD_1"),
            address,
            address,
            payment);

        order.Add(productId, quantity: 2, price: 350);

        return order;
    }

    public static async Task CleanupAsync(ApplicationDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM dbo.OrderItems;
            DELETE FROM dbo.Orders;
            DELETE FROM dbo.OutboxMessages;
            DELETE FROM dbo.Products;
            DELETE FROM dbo.Customers;
            DELETE FROM dbo.OutboxPublishFailures;
            """);
    }
}
