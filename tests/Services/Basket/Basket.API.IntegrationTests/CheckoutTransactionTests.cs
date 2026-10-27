using Basket.API.Data;
using Basket.API.Exceptions;
using Basket.API.Models;
using Basket.API.Outbox;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Messaging.Events.Basket.V1;
using BuildingBlocks.Messaging.Serialization;
using Marten;

namespace Basket.API.IntegrationTests;

/// <summary>
/// The checkout used to publish and then delete the basket as two separate writes.
/// These tests pin down the replacement: deleting the basket and recording the
/// event happen in one database transaction, and nothing is published here.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class CheckoutTransactionTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.Store.Advanced.Clean.DeleteAllDocumentsAsync();

    public Task DisposeAsync() => fixture.Store.Advanced.Clean.DeleteAllDocumentsAsync();

    [Fact]
    public async Task Checkout_DeletesBasketAndRecordsOneOutboxMessage()
    {
        await StoreBasketAsync("mustafa", price: 450, quantity: 2);

        await using var session = fixture.Store.LightweightSession();
        var checkoutEvent = await new BasketRepository(session, TimeProvider.System)
            .CheckoutBasket(TestCheckouts.Draft("mustafa"));

        await using var verify = fixture.Store.QuerySession();
        Assert.Null(await verify.LoadAsync<ShoppingCart>("mustafa"));

        var outbox = Assert.Single(await verify.Query<BasketOutboxMessage>().ToListAsync());
        Assert.Equal(checkoutEvent.Id, outbox.Id);
        Assert.Equal(checkoutEvent.Id, outbox.Envelope.Id);
        Assert.Equal("basket.checkout-submitted", outbox.Envelope.EventType);
        Assert.Equal(1, outbox.Envelope.SchemaVersion);
        Assert.Equal("mustafa", outbox.Envelope.AggregateId);
        Assert.Equal(0, outbox.Attempts);
        Assert.Null(outbox.PoisonedOnUtc);

        var stored = Deserialize(outbox);
        Assert.Equal(900, stored.TotalPrice);
    }

    // The lines are what Ordering builds the order from, so they must be the
    // stored basket's lines - not whatever the request claimed.
    [Fact]
    public async Task Checkout_CarriesTheBasketLines()
    {
        var basket = await StoreBasketAsync("mustafa", price: 450, quantity: 2);

        await CheckoutAsync("mustafa");

        await using var verify = fixture.Store.QuerySession();
        var stored = Deserialize(Assert.Single(await verify.Query<BasketOutboxMessage>().ToListAsync()));
        var line = Assert.Single(stored.Items);
        Assert.Equal(basket.Items[0].ProductId, line.ProductId);
        Assert.Equal("iPhone X", line.ProductName);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(450, line.UnitPrice);
        Assert.Equal("pt_test", stored.Payment.PaymentToken);
    }

    [Fact]
    public async Task Checkout_OfAnEmptyBasket_IsRejectedAndKeepsTheBasket()
    {
        await using (var session = fixture.Store.LightweightSession())
        {
            session.Store(new ShoppingCart("mustafa"));
            await session.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<EmptyBasketException>(() => CheckoutAsync("mustafa"));

        await using var verify = fixture.Store.QuerySession();
        Assert.NotNull(await verify.LoadAsync<ShoppingCart>("mustafa"));
        Assert.Empty(await verify.Query<BasketOutboxMessage>().ToListAsync());
    }

    [Fact]
    public async Task Checkout_WithoutBasket_WritesNothing()
    {
        await using var session = fixture.Store.LightweightSession();

        await Assert.ThrowsAsync<BasketNotFoundException>(() =>
            new BasketRepository(session, TimeProvider.System)
                .CheckoutBasket(TestCheckouts.Draft("nobody")));

        await using var verify = fixture.Store.QuerySession();
        Assert.Empty(await verify.Query<BasketOutboxMessage>().ToListAsync());
    }

    // The event id comes from the basket version, which is what makes a racing
    // second checkout upsert the same outbox message instead of adding another.
    [Fact]
    public async Task Checkout_EventIdIsDerivedFromTheBasketVersion()
    {
        var basket = await StoreBasketAsync("mustafa", price: 100, quantity: 1);
        Guid version;
        await using (var read = fixture.Store.QuerySession())
        {
            version = (await read.MetadataForAsync(basket))!.CurrentVersion;
        }

        await using var session = fixture.Store.LightweightSession();
        var checkoutEvent = await new BasketRepository(session, TimeProvider.System)
            .CheckoutBasket(TestCheckouts.Draft("mustafa"));

        Assert.Equal(CheckoutId.For("mustafa", version), checkoutEvent.Id);
    }

    [Fact]
    public async Task Checkout_OfARefilledBasket_GetsANewId()
    {
        await StoreBasketAsync("mustafa", price: 100, quantity: 1);
        var first = await CheckoutAsync("mustafa");

        await StoreBasketAsync("mustafa", price: 100, quantity: 1);
        var second = await CheckoutAsync("mustafa");

        Assert.NotEqual(first.Id, second.Id);

        await using var verify = fixture.Store.QuerySession();
        Assert.Equal(2, (await verify.Query<BasketOutboxMessage>().ToListAsync()).Count);
    }

    // Real concurrent requests against one basket. Whichever way they interleave,
    // at most one outbox message may exist and every request either succeeds or
    // finds the basket already gone.
    [Fact]
    public async Task ConcurrentCheckouts_OfTheSameBasket_RecordAtMostOneMessage()
    {
        await StoreBasketAsync("mustafa", price: 100, quantity: 1);

        var attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            try
            {
                await CheckoutAsync("mustafa");
                return "ok";
            }
            catch (BasketNotFoundException)
            {
                return "gone";
            }
        });

        var outcomes = await Task.WhenAll(attempts);

        Assert.Contains("ok", outcomes);
        Assert.All(outcomes, outcome => Assert.Contains(outcome, new[] { "ok", "gone" }));

        await using var verify = fixture.Store.QuerySession();
        Assert.Single(await verify.Query<BasketOutboxMessage>().ToListAsync());
    }

    private static BasketCheckoutSubmittedIntegrationEvent Deserialize(BasketOutboxMessage message) =>
        (BasketCheckoutSubmittedIntegrationEvent)IntegrationEventSerializer.Deserialize(
            message.Envelope.Payload, typeof(BasketCheckoutSubmittedIntegrationEvent));

    private async Task<BasketCheckoutSubmittedIntegrationEvent> CheckoutAsync(string userName)
    {
        await using var session = fixture.Store.LightweightSession();
        return await new BasketRepository(session, TimeProvider.System)
            .CheckoutBasket(TestCheckouts.Draft(userName));
    }

    private async Task<ShoppingCart> StoreBasketAsync(string userName, decimal price, int quantity)
    {
        var basket = new ShoppingCart(userName);
        basket.AddItem(Guid.NewGuid(), "iPhone X", "black", quantity, price);

        await using var session = fixture.Store.LightweightSession();
        session.Store(basket);
        await session.SaveChangesAsync();
        return basket;
    }
}
