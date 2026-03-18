using Microsoft.Extensions.Caching.Distributed;

namespace Basket.API.Diagnostics;

public class DiagnosticsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/diag/services", (IBasketRepository repo, IDistributedCache cache) =>
            Results.Ok(new
            {
                BasketRepository = repo.GetType().FullName,
                DistributedCache = cache.GetType().FullName
            }));
    }
}

