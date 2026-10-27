using Microsoft.EntityFrameworkCore;
using Ordering.Application.Dtos;
using Ordering.Application.Orders.Commands.CreateOrder;
using Ordering.Domain.Enums;
using Ordering.Domain.ValueObjects;

namespace Ordering.IntegrationTests;

/// <summary>
/// Checkout lines refer to catalog products, which Ordering's own Products table
/// may not hold. These run against real SQL Server, where the OrderItems ->
/// Products foreign key would reject such an order.
/// </summary>
[Collection(nameof(SqlServerCdcCollection))]
public sealed class ProductSnapshotTests(SqlServerCdcFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var context = fixture.CreateDbContext();
        await TestOrders.CleanupAsync(context);
    }

    [Fact]
    public async Task CreateOrder_ForAProductOrderingDoesNotKnow_AddsItAndTheOrder()
    {
        var (customer, _) = await SeedAsync();
        var catalogOnly = Guid.NewGuid();

        await CreateOrderAsync(customer, catalogOnly, new ProductSnapshotDto(catalogOnly, "Phone case", 25));

        await using var verify = fixture.CreateDbContext();
        var product = await verify.Products.SingleAsync(p => p.Id == ProductId.Of(catalogOnly));
        Assert.Equal("Phone case", product.Name);
        Assert.Equal(25, product.Price);
        Assert.Single(await verify.Orders.Where(o => o.CustomerId == CustomerId.Of(customer)).ToListAsync());
    }

    // The snapshot fills a gap; it does not overwrite what Ordering already holds.
    [Fact]
    public async Task CreateOrder_ForAKnownProduct_KeepsItsStoredNameAndPrice()
    {
        var (customer, known) = await SeedAsync();

        await CreateOrderAsync(customer, known, new ProductSnapshotDto(known, "Renamed in catalog", 999));

        await using var verify = fixture.CreateDbContext();
        var product = await verify.Products.SingleAsync(p => p.Id == ProductId.Of(known));
        Assert.Equal("Test Product", product.Name);
        Assert.Equal(350, product.Price);
    }

    private async Task<(Guid Customer, Guid Product)> SeedAsync()
    {
        await using var context = fixture.CreateDbContext();
        var (customer, product) = await TestOrders.SeedReferenceDataAsync(context);
        return (customer.Id.Value, product.Id.Value);
    }

    private async Task CreateOrderAsync(Guid customerId, Guid productId, ProductSnapshotDto snapshot)
    {
        var address = new AddressDto(
            "Mustafa", "Akcakaya", "mustafa@example.com", "Test Address", "Turkey", "Istanbul", "34000");
        var order = new OrderDto(
            Id: Guid.NewGuid(),
            CustomerId: customerId,
            ShippingAddress: address,
            BillingAddress: address,
            Payment: new PaymentDto(TestOrders.PaymentToken, "4444", 1),
            Status: OrderStatus.Pending,
            OrderItems: [new OrderItemDto(Guid.Empty, productId, 1, snapshot.Price)]);

        await using var context = fixture.CreateDbContext();
        await new CreateOrderHandler(context)
            .Handle(new CreateOrderCommand(order, [snapshot]), CancellationToken.None);
    }
}
