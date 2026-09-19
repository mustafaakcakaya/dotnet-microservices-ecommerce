using Basket.API.Data;
using Basket.API.Exceptions;
using Basket.API.Models;
using Basket.API.Outbox;
using BuildingBlocks.Messaging.Events;
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
            .CheckoutBasket(new BasketCheckoutEvent { UserName = "mustafa" });

        await using var verify = fixture.Store.QuerySession();
        Assert.Null(await verify.LoadAsync<ShoppingCart>("mustafa"));

        var outbox = Assert.Single(await verify.Query<BasketOutboxMessage>().ToListAsync());
        Assert.Equal(checkoutEvent.Id, outbox.Id);
        Assert.Equal(checkoutEvent.Id, outbox.Message.Id);
        Assert.Equal(900, outbox.Message.TotalPrice);
        Assert.Equal(0, outbox.Attempts);
        Assert.Null(outbox.PoisonedOnUtc);
    }

    [Fact]
    public async Task Checkout_WithoutBasket_WritesNothing()
    {
        await using var session = fixture.Store.LightweightSession();

        await Assert.ThrowsAsync<BasketNotFoundException>(() =>
            new BasketRepository(session, TimeProvider.System)
                .CheckoutBasket(new BasketCheckoutEvent { UserName = "nobody" }));

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
            .CheckoutBasket(new BasketCheckoutEvent { UserName = "mustafa" });

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

    private async Task<BasketCheckoutEvent> CheckoutAsync(string userName)
    {
        await using var session = fixture.Store.LightweightSession();
        return await new BasketRepository(session, TimeProvider.System)
            .CheckoutBasket(new BasketCheckoutEvent { UserName = userName });
    }

    private async Task<ShoppingCart> StoreBasketAsync(string userName, decimal price, int quantity)
    {
        var basket = new ShoppingCart(userName)
        {
            Items =
            [
                new ShoppingCartItem
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "iPhone X",
                    Quantity = quantity,
                    Price = price,
                    Color = "black"
                }
            ]
        };

        await using var session = fixture.Store.LightweightSession();
        session.Store(basket);
        await session.SaveChangesAsync();
        return basket;
    }
}
