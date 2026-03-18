using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Basket.API.Data;

public class CachedBasketRepository
    (IBasketRepository basketRepository, IDistributedCache cache, ILogger<CachedBasketRepository> logger)
    : IBasketRepository
{
    public async Task<ShoppingCart> GetBasket(string userName, CancellationToken cancellationToken = default)
    {
        var cachedBasket = await cache.GetStringAsync(userName, cancellationToken);
        if (!string.IsNullOrEmpty(cachedBasket))
        {
            logger.LogInformation("Cache HIT for basket {UserName}", userName);
            return JsonSerializer.Deserialize<ShoppingCart>(cachedBasket)!;
        }
        
        logger.LogInformation("Cache MISS for basket {UserName}", userName);
        
        var basket = await basketRepository.GetBasket(userName, cancellationToken);
        await cache.SetStringAsync(userName, JsonSerializer.Serialize(basket), cancellationToken);
        logger.LogInformation("Cache SET for basket {UserName}", userName);

        return basket;
    }

    public async Task<ShoppingCart> StoreBasket(ShoppingCart basket, CancellationToken cancellationToken = default)
    {
        await basketRepository.StoreBasket(basket, cancellationToken);

        await cache.SetStringAsync(basket.UserName, JsonSerializer.Serialize(basket), cancellationToken);
        logger.LogInformation("Cache SET for basket {UserName}", basket.UserName);

        return basket;
    }

    public async Task<bool> DeleteBasket(string userName, CancellationToken cancellationToken = default)
    {
        await basketRepository.DeleteBasket(userName, cancellationToken);
        
        await cache.RemoveAsync(userName, cancellationToken);
        logger.LogInformation("Cache REMOVE for basket {UserName}", userName);

        return true;
    }
}