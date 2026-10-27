using Ordering.Application.Exceptions;
using Ordering.Application.Orders.Queries.GetOrderById;

namespace Ordering.IntegrationTests;

/// <summary>An order is found by its id; there is no other name for it.</summary>
[Collection(nameof(SqlServerCdcCollection))]
public sealed class GetOrderByIdTests(SqlServerCdcFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var context = fixture.CreateDbContext();
        await TestOrders.CleanupAsync(context);
    }

    [Fact]
    public async Task GetOrderById_ReturnsTheOrderWithItsLines()
    {
        await using var seed = fixture.CreateDbContext();
        var (customer, product) = await TestOrders.SeedReferenceDataAsync(seed);
        var order = TestOrders.NewOrder(customer.Id, product.Id);
        seed.Orders.Add(order);
        await seed.SaveChangesAsync();

        await using var context = fixture.CreateDbContext();
        var result = await new GetOrderByIdHandler(context)
            .Handle(new GetOrderByIdQuery(order.Id.Value), CancellationToken.None);

        Assert.Equal(order.Id.Value, result.Order.Id);
        var line = Assert.Single(result.Order.OrderItems);
        Assert.Equal((product.Id.Value, 2, 350m), (line.ProductId, line.Quantity, line.Price));
    }

    [Fact]
    public async Task GetOrderById_ForAnUnknownId_IsNotFound()
    {
        await using var context = fixture.CreateDbContext();

        await Assert.ThrowsAsync<OrderNotFoundException>(() => new GetOrderByIdHandler(context)
            .Handle(new GetOrderByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
