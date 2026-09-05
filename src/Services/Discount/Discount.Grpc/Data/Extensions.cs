using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Discount.Grpc.Data;

public static class Extensions
{
    public static IApplicationBuilder UseMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiscountContext>();
        dbContext.Database.Migrate();

        return app;
    }

    /// <summary>
    /// Seeds the sample coupons. Driven by an explicit switch (Discount:Seed, or
    /// the Discount__Seed environment variable) that defaults to the environment,
    /// matching how Catalog and Ordering decide the same thing.
    /// </summary>
    public static IApplicationBuilder UseSeeding(this IApplicationBuilder app, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!(configuration.GetValue<bool?>("Discount:Seed") ?? environment.IsDevelopment()))
        {
            return app;
        }

        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiscountContext>();
        DiscountInitialData.SeedAsync(dbContext).GetAwaiter().GetResult();

        return app;
    }
}
