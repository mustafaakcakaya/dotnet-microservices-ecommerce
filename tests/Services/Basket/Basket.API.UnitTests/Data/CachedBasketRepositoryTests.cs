using Basket.API.Data;
using Basket.API.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Basket.API.UnitTests.Data;

public sealed class CachedBasketRepositoryTests
{
    private const string UserName = "mustafa";

    private readonly FakeBasketRepository _inner = new();
    private readonly FakeDistributedCache _cache = new();

    private CachedBasketRepository CreateRepository(TimeSpan? ttl = null) => new(
        _inner,
        _cache,
        ttl ?? TimeSpan.FromMinutes(5),
        NullLogger<CachedBasketRepository>.Instance);

    [Fact]
    public async Task GetBasket_OnMiss_ReadsFromDatabaseAndPopulatesCache()
    {
        _inner.Seed(BasketWith(UserName, price: 100));

        var basket = await CreateRepository().GetBasket(UserName);

        Assert.Equal(UserName, basket.UserName);
        Assert.Equal(1, _inner.GetCalls);
        Assert.True(_cache.Contains(UserName));
    }

    [Fact]
    public async Task GetBasket_OnHit_DoesNotTouchDatabase()
    {
        var repository = CreateRepository();
        _inner.Seed(BasketWith(UserName, price: 100));
        await repository.GetBasket(UserName); // populates the cache

        await repository.GetBasket(UserName);

        Assert.Equal(1, _inner.GetCalls);
    }

    // Item 5: entries must expire, otherwise an abandoned basket lives forever.
    [Fact]
    public async Task GetBasket_AppliesConfiguredTtlWhenWritingToCache()
    {
        _inner.Seed(BasketWith(UserName, price: 100));

        await CreateRepository(TimeSpan.FromMinutes(7)).GetBasket(UserName);

        Assert.Equal(TimeSpan.FromMinutes(7), _cache.LastWriteOptions?.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task Constructor_NonPositiveTtl_FallsBackToDefault()
    {
        _inner.Seed(BasketWith(UserName, price: 100));

        await CreateRepository(TimeSpan.Zero).GetBasket(UserName);

        Assert.Equal(CachedBasketRepository.DefaultTtl, _cache.LastWriteOptions?.AbsoluteExpirationRelativeToNow);
    }

    // Item 7, read path: the database still holds the answer, so a Redis outage
    // must not turn a read into an error.
    [Fact]
    public async Task GetBasket_WhenCacheReadFails_StillServesFromDatabase()
    {
        _inner.Seed(BasketWith(UserName, price: 250));
        _cache.FailReads = true;

        var basket = await CreateRepository().GetBasket(UserName);

        Assert.Equal(250, basket.TotalPrice);
        Assert.Equal(1, _inner.GetCalls);
    }

    [Fact]
    public async Task GetBasket_WhenCacheWriteFails_StillReturnsDatabaseResult()
    {
        _inner.Seed(BasketWith(UserName, price: 250));
        _cache.FailWrites = true;

        var basket = await CreateRepository().GetBasket(UserName);

        Assert.Equal(250, basket.TotalPrice);
    }

    // Item 8: a corrupt entry must not fail the request.
    [Fact]
    public async Task GetBasket_WhenCachedEntryIsCorrupt_FallsBackToDatabase()
    {
        _inner.Seed(BasketWith(UserName, price: 300));
        _cache.SeedRaw(UserName, "{ this is not json");

        var basket = await CreateRepository().GetBasket(UserName);

        Assert.Equal(300, basket.TotalPrice);
        Assert.Equal(1, _inner.GetCalls);
    }

    // Item 7, write path: the database already changed, so a surviving stale
    // entry would serve the wrong basket. The caller must learn about it.
    [Fact]
    public async Task StoreBasket_WhenCacheWriteFails_Throws()
    {
        _cache.FailWrites = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateRepository().StoreBasket(BasketWith(UserName, price: 400)));

        // The database write still happened; storing is an upsert, so a retry is safe.
        Assert.Equal(1, _inner.StoreCalls);
        Assert.True(_inner.Contains(UserName));
    }

    [Fact]
    public async Task DeleteBasket_WhenCacheRemovalFails_Throws()
    {
        _inner.Seed(BasketWith(UserName, price: 400));
        _cache.FailRemovals = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateRepository().DeleteBasket(UserName));

        Assert.Equal(1, _inner.DeleteCalls);
        Assert.False(_inner.Contains(UserName));
    }

    [Fact]
    public async Task StoreBasket_WritesThroughToDatabaseAndCache()
    {
        await CreateRepository().StoreBasket(BasketWith(UserName, price: 500));

        Assert.Equal(1, _inner.StoreCalls);
        Assert.True(_cache.Contains(UserName));
    }

    [Fact]
    public async Task DeleteBasket_EvictsCachedEntry()
    {
        var repository = CreateRepository();
        _inner.Seed(BasketWith(UserName, price: 500));
        await repository.GetBasket(UserName); // populates the cache

        await repository.DeleteBasket(UserName);

        Assert.False(_cache.Contains(UserName));
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
