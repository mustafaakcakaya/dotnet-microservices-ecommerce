using System.Text;
using Basket.API.Data;
using Basket.API.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace Basket.API.UnitTests.Data;

/// <summary>In-memory stand-in for the Postgres-backed repository.</summary>
internal sealed class FakeBasketRepository : IBasketRepository
{
    private readonly Dictionary<string, ShoppingCart> _baskets = [];

    public int StoreCalls { get; private set; }
    public int DeleteCalls { get; private set; }
    public int GetCalls { get; private set; }

    public void Seed(ShoppingCart basket) => _baskets[basket.UserName] = basket;

    public bool Contains(string userName) => _baskets.ContainsKey(userName);

    public Task<ShoppingCart> GetBasket(string userName, CancellationToken cancellationToken = default)
    {
        GetCalls++;
        return Task.FromResult(_baskets.TryGetValue(userName, out var basket)
            ? basket
            : new ShoppingCart(userName));
    }

    public Task<ShoppingCart> StoreBasket(ShoppingCart basket, CancellationToken cancellationToken = default)
    {
        StoreCalls++;
        _baskets[basket.UserName] = basket;
        return Task.FromResult(basket);
    }

    public Task<bool> DeleteBasket(string userName, CancellationToken cancellationToken = default)
    {
        DeleteCalls++;
        _baskets.Remove(userName);
        return Task.FromResult(true);
    }
}

/// <summary>
/// Distributed cache whose failures can be turned on per operation, so the
/// decorator's behaviour during a Redis outage is testable without Redis.
/// </summary>
internal sealed class FakeDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _entries = [];

    public bool FailReads { get; set; }
    public bool FailWrites { get; set; }
    public bool FailRemovals { get; set; }

    public DistributedCacheEntryOptions? LastWriteOptions { get; private set; }
    public int WriteCalls { get; private set; }

    public void SeedRaw(string key, string value) => _entries[key] = Encoding.UTF8.GetBytes(value);

    public bool Contains(string key) => _entries.ContainsKey(key);

    public byte[]? Get(string key)
    {
        if (FailReads)
        {
            throw new InvalidOperationException("cache unavailable (simulated)");
        }

        return _entries.TryGetValue(key, out var value) ? value : null;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        WriteCalls++;

        if (FailWrites)
        {
            throw new InvalidOperationException("cache unavailable (simulated)");
        }

        LastWriteOptions = options;
        _entries[key] = value;
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key)
    {
        if (FailRemovals)
        {
            throw new InvalidOperationException("cache unavailable (simulated)");
        }

        _entries.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}
