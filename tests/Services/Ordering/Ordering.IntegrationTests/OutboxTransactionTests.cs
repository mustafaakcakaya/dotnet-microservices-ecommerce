using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Ordering.Application.Features;
using Ordering.Domain.ValueObjects;

namespace Ordering.IntegrationTests;

[Collection(nameof(SqlServerCdcCollection))]
public sealed class OutboxTransactionTests(SqlServerCdcFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var context = fixture.CreateDbContext();
        await TestOrders.CleanupAsync(context);
    }

    [Fact]
    public async Task CreatingOrder_WritesOutboxMessageInSameTransaction()
    {
        await using var context = fixture.CreateDbContext();
        var (customer, product) = await TestOrders.SeedReferenceDataAsync(context);

        var order = TestOrders.NewOrder(customer.Id, product.Id);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        await using var verification = fixture.CreateDbContext();
        var outboxMessage = Assert.Single(
            await verification.OutboxMessages
                .Where(m => m.AggregateId == order.Id.Value.ToString())
                .ToListAsync());

        Assert.Equal("ordering.order-created", outboxMessage.EventType);
        Assert.Equal(1, outboxMessage.SchemaVersion);
        Assert.NotEqual(Guid.Empty, outboxMessage.Id);
        Assert.DoesNotContain(TestOrders.CardNumber, outboxMessage.Payload);

        // The order itself was persisted alongside the envelope.
        Assert.NotNull(await verification.Orders.FindAsync(order.Id));
    }

    [Fact]
    public async Task RolledBackTransaction_PersistsNeitherOrderNorOutboxMessage()
    {
        OrderId orderId;

        await using (var context = fixture.CreateDbContext())
        {
            var (customer, product) = await TestOrders.SeedReferenceDataAsync(context);

            await using var transaction = await context.Database.BeginTransactionAsync();
            var order = TestOrders.NewOrder(customer.Id, product.Id);
            orderId = order.Id;

            context.Orders.Add(order);
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        // Atomicity: rolling back the transaction removes both writes together.
        await using var verification = fixture.CreateDbContext();
        Assert.Null(await verification.Orders.FindAsync(orderId));
        Assert.Empty(await verification.OutboxMessages
            .Where(m => m.AggregateId == orderId.Value.ToString())
            .ToListAsync());
    }

    [Fact]
    public async Task CreatingOrder_WhenFulfillmentIsDisabled_DoesNotWriteOrderCreatedOutboxMessage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"FeatureManagement:{OrderingFeatures.OrderFulfillment}"] = "false"
            })
            .Build();

        await using var featureProvider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddFeatureManagement()
            .Services
            .BuildServiceProvider();

        var featureManager = featureProvider.GetRequiredService<IFeatureManager>();

        await using var context = fixture.CreateDbContext(featureManager);
        var (customer, product) = await TestOrders.SeedReferenceDataAsync(context);

        var order = TestOrders.NewOrder(customer.Id, product.Id);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        await using var verification = fixture.CreateDbContext();
        Assert.NotNull(await verification.Orders.FindAsync(order.Id));
        Assert.Empty(await verification.OutboxMessages
            .Where(message => message.AggregateId == order.Id.Value.ToString())
            .ToListAsync());
    }
}
