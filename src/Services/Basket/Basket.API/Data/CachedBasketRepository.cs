using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Basket.API.Data;

public class CachedBasketRepository
    (IBasketRepository basketRepository, IDistributedCache cache, TimeSpan ttl, ILogger<CachedBasketRepository> logger)
    : IBasketRepository
{
    /// <summary>
    /// Bounds how long a cached basket may live. Without an expiry an abandoned
    /// basket stays in Redis forever, and an entry that drifts out of step with
    /// the database only heals on the next write for that user - which never
    /// comes for an abandoned cart. A TTL turns both into a bounded window.
    /// </summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

    // Absolute, measured from the write, so a hot basket cannot keep a stale
    // entry alive indefinitely by being read.
    private readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = ttl > TimeSpan.Zero ? ttl : DefaultTtl
    };

    /// <summary>
    /// Serves from the cache when possible and populates it on a miss.
    /// Cache failures are logged and swallowed here: the database still holds
    /// the answer, so a Redis outage must not turn reads into errors.
    /// </summary>
    public async Task<ShoppingCart> GetBasket(string userName, CancellationToken cancellationToken = default)
    {
        var cachedBasket = await TryGetCachedAsync(userName, cancellationToken);
        if (cachedBasket is not null)
        {
            logger.LogInformation("Cache HIT for basket {UserName}", userName);
            return cachedBasket;
        }

        var basket = await basketRepository.GetBasket(userName, cancellationToken);

        // Populating the cache is best effort for the same reason.
        try
        {
            await cache.SetStringAsync(userName, JsonSerializer.Serialize(basket), _cacheOptions, cancellationToken);
            logger.LogInformation("Cache SET for basket {UserName}", userName);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Cache SET failed for basket {UserName}", userName);
        }

        return basket;
    }

    /// <summary>
    /// Writes through to the database and then the cache. A cache failure is
    /// NOT swallowed: the database has already changed, so a surviving stale
    /// entry would serve the wrong basket until the TTL expires. Storing is an
    /// upsert, so the caller can safely retry.
    /// </summary>
    public async Task<ShoppingCart> StoreBasket(ShoppingCart basket, CancellationToken cancellationToken = default)
    {
        await basketRepository.StoreBasket(basket, cancellationToken);

        await cache.SetStringAsync(basket.UserName, JsonSerializer.Serialize(basket), _cacheOptions, cancellationToken);
        logger.LogInformation("Cache SET for basket {UserName}", basket.UserName);

        return basket;
    }

    /// <summary>
    /// Same reasoning as <see cref="StoreBasket"/>: a failed eviction would keep
    /// serving a basket that no longer exists, so the error surfaces.
    /// </summary>
    public async Task<bool> DeleteBasket(string userName, CancellationToken cancellationToken = default)
    {
        await basketRepository.DeleteBasket(userName, cancellationToken);

        await cache.RemoveAsync(userName, cancellationToken);
        logger.LogInformation("Cache REMOVE for basket {UserName}", userName);

        return true;
    }

    private async Task<ShoppingCart?> TryGetCachedAsync(string userName, CancellationToken cancellationToken)
    {
        string? cachedBasket;
        try
        {
            cachedBasket = await cache.GetStringAsync(userName, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Cache GET failed for basket {UserName}", userName);
            return null;
        }

        if (string.IsNullOrEmpty(cachedBasket))
        {
            logger.LogInformation("Cache MISS for basket {UserName}", userName);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ShoppingCart>(cachedBasket);
        }
        catch (JsonException exception)
        {
            // A corrupt entry must not fail the request; fall through to the database.
            logger.LogWarning(exception, "Discarding unreadable cached basket for {UserName}", userName);
            return null;
        }
    }
}
