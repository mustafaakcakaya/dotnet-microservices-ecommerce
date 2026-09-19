using Basket.API.Data;
using Basket.API.Exceptions;
using Basket.API.Models;
using BuildingBlocks.Messaging.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Basket.API.UnitTests.Data;

public sealed class CachedBasketRepositoryCheckoutTests
{
    private const string UserName = "mustafa";

    private readonly FakeBasketRepository _inner = new();
    private readonly FakeDistributedCache _cache = new();

    private CachedBasketRepository CreateRepository() => new(
        _inner,
        _cache,
        TimeSpan.FromMinutes(5),
        NullLogger<CachedBasketRepository>.Instance);

    [Fact]
    public async Task CheckoutBasket_EvictsCachedBasket()
    {
        var repository = CreateRepository();
        _inner.Seed(BasketWith(UserName, price: 100));
        await repository.GetBasket(UserName); // populates the cache

        await repository.CheckoutBasket(new BasketCheckoutEvent { UserName = UserName });

        Assert.False(_cache.Contains(UserName));
    }

    [Fact]
    public async Task CheckoutBasket_ReturnsEventWithBasketTotal()
    {
        _inner.Seed(BasketWith(UserName, price: 250));

        var checkoutEvent = await CreateRepository()
            .CheckoutBasket(new BasketCheckoutEvent { UserName = UserName });

        Assert.Equal(250, checkoutEvent.TotalPrice);
    }

    // The checkout has already committed when eviction runs, so a Redis failure
    // must not be reported as a failed checkout.
    [Fact]
    public async Task CheckoutBasket_WhenEvictionFails_StillSucceeds()
    {
        _inner.Seed(BasketWith(UserName, price: 100));
        _cache.FailRemovals = true;

        var checkoutEvent = await CreateRepository()
            .CheckoutBasket(new BasketCheckoutEvent { UserName = UserName });

        Assert.Equal(UserName, checkoutEvent.UserName);
        Assert.Single(_inner.CheckedOut);
    }

    [Fact]
    public async Task CheckoutBasket_WhenNoBasket_Throws()
    {
        await Assert.ThrowsAsync<BasketNotFoundException>(
            () => CreateRepository().CheckoutBasket(new BasketCheckoutEvent { UserName = UserName }));
    }

    private static ShoppingCart BasketWith(string userName, decimal price) => new(userName)
    {
        Items =
        [
            new ShoppingCartItem
            {
                ProductId = Guid.NewGuid(),
                ProductName = "iPhone X",
                Quantity = 1,
                Price = price,
                Color = "black"
            }
        ]
    };
}
