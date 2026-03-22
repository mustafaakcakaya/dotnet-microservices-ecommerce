using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Discount.Grpc.Data;

public static class Extensions
{
    public static IApplicationBuilder UseMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DiscountContext>>();
        using var dbContext = factory.CreateDbContext();
        dbContext.Database.Migrate();

        return app;
    }
}